using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using JPS4.Editor;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static JPS4.JPS4Utils;

namespace JPS4
{
    /// <summary>
    /// The primary grid manager object for storing collision and generating paths.
    /// 1. Stores a `TileGrid` object to maintain static and dynamic collision matrices
    /// 2. Provides the core API for path requests including a managed job queue.
    /// </summary>
    /// <remarks>
    /// The "grid" position of tiles refers to 2D coordinates (bottom left = 0,0) whereas "world" position refers to 3D continuous positions in the scene.
    /// All Jobs / Burst deal with the 2D grid only where floating point precision is not a concern.
    /// So the `PathfindingGridBuilder` bridges this gap with its own `Transform` centering and collision settings.
    /// </remarks>
    public class PathfindingGridBuilder : GridBuilder
    {
        [Header("Configuration")] 
        [SerializeField, Tooltip("Track additional debug information from every path request.")] 
        private bool enableDebug;

        [SerializeField, Tooltip("Show gizmos for the grid definition.")] 
        private bool showGizmos = true;
        
        [Header("Grid")] 
        [SerializeField, Tooltip("Side length in world units of a grid tile.")] 
        private float cellSize = 1f;
        
        [SerializeField, Tooltip("Dimensions in tiles for the grid.")]
        private int2 gridSize = new int2(10, 10);

        [Header("Collision")]
        [SerializeField, Tooltip("Collision settings when determining static obstacles.")]
        private CollisionSettings collisionSettings;

        public List<JPSDebugStep> LastJPSDebugSteps { get; private set; }
        public bool IsDebuggingEnabled => enableDebug;
        public Vector3 GridCenter => transform.position;
        public override TileGrid Grid => tileGrid;
        public override float CellSize => cellSize;
        public override Vector3 LocalLeftCorner =>new Vector3(-cellSize * gridSize.x / 2f, -cellSize * gridSize.y / 2f,  0f);
        public override CollisionSettings CollisionSettings => collisionSettings;
        
        private TileGrid tileGrid;
        protected GridCollisionHandler collisionHandler;
        protected int buildID; // Count grid generation for job cancellation

        /// <summary>
        /// Build collision from external source. 
        /// </summary>
        /// <param name="gridTraversableIn"> Static collision matrix where true = walkable, false = obstacle. This will be copied, so ensure it's disposed.</param>
        /// <param name="gridSizeIn"> Size of the incoming collision matrix.</param>
        /// <returns>True if configuration is valid and a new <see cref="TileGrid"/> object was assigned to this builder.</returns>
        public bool TryBuildExternal(NativeArray<bool> gridTraversableIn, int2 gridSizeIn)
        {
            buildID++;
            CancelPendingJobs(PathResult.EPathJobStatus.Cancelled_Rebuild);
            
            // Validate size:
            if (gridSizeIn.x * gridSizeIn.y != gridTraversableIn.Length)
            {
                Debug.LogError("Length mismatch.");
                return false;
            }
            
            // Rebuild if existing data:
            tileGrid?.Dispose();

            this.gridSize = new int2(gridSizeIn.x, gridSizeIn.y);
            tileGrid = new TileGrid(gridSize, in gridTraversableIn, Allocator.Persistent);
            return true;
        }
        
        
        /// <summary>
        /// Try to build collision from an external source.
        /// </summary>
        /// <param name="gridTraversableIn"> Static collision matrix where true = walkable, false = obstacle. This will be copied, so ensure it's disposed.</param>
        /// <param name="gridSizeIn"> Size of the incoming collision matrix.</param>
        /// <returns>True if configuration is valid and a new <see cref="TileGrid"/> object was assigned to this builder.</returns>
        public bool TryBuildExternal(NativeArray<bool> gridTraversableIn)
        {
            return TryBuildExternal(gridTraversableIn, gridSize);
        }
        
        /// <summary>
        /// Build the static collision for the current grid, creating a new <see cref="TileGrid"/> object.
        /// Pathfinding is fully dynamic / not cached, so any algorithm can be used on the same grid multiple times.
        /// Build can be called multiple times to re-cache the static collision matrix.
        /// </summary>
        [Button("Build")]
        public void Build()
        {
            buildID++;
            CancelPendingJobs(PathResult.EPathJobStatus.Cancelled_Rebuild); // Cancel all pending, active will be invalidated on completion
            collisionHandler= collisionSettings.CollisionType switch
            {
                ECollisionType.Collider2D => new RayCastCollisionHandler2D(),
                ECollisionType.Collider3D => new RayCastCollisionHandler3D(),
                _ => null // Custom collision
            };
            
            if (collisionHandler == null)
            {
                Debug.LogError("Collision Type Custom: Use Build(...) overload w/ custom grid collision mask.");
                return;
            }
            tileGrid?.Dispose();
            tileGrid = new TileGrid(gridSize, Allocator.Persistent);

            collisionHandler.Build(this);
        }
        

        
        #region PathRequests
        private PathHandle CreatePathHandle(int2 startGrid, int2 targetGrid, Allocator allocator, PathConfig config)
        {
            var path = new NativeList<int2>(allocator);
            var debug = new NativeList<JPSDebugStep>(allocator);

            var handle = SchedulePathJob(startGrid, targetGrid, ref path, ref debug, config);

            return new PathHandle
            {
                Handle = handle,
                Path = path,
                DebugSteps = debug
            };
        }

        /// <summary>
        /// Returns a <see cref="PathHandle"/> for the requested path.
        /// </summary>
        /// <remarks>
        /// FREE VERSION - PathHandle.Handle is always pre-completed (default).
        /// The path is computed synchronously on the main thread before this method returns.
        /// Calling code blocks; no worker thread is ever used.
        /// 
        /// JPS4+ Pro returns a live JobHandle backed by a
        /// [BurstCompile] IJob running on a Unity worker thread:
        /// - Main thread returns immediately - zero blocking
        /// - Burst SIMD delivers up to 10-20x faster query throughput
        /// - NativeArray storage - no GC allocations, no garbage-collector spikes
        /// - Full Job system integration: chain with JobHandle.CombineDependencies,
        ///   batch hundreds of parallel requests per frame, interop with Physics/Animation jobs
        /// Upgrade: https://assetstore.unity.com/packages/slug/jps4-2d-grid-pathfinding-355438
        /// </remarks>
        public PathHandle RequestPath(int2 start, int2 target, PathConfig config, Allocator allocator = Allocator.TempJob)
        {
            if (!IsBuilt) throw new InvalidOperationException("Grid not built.");
            return CreatePathHandle(start, target, allocator, config);
        }
        
        /// <summary>
        /// Get the direct path output in grid-space.
        /// Helpful internal function, but usually you'll want <seealso cref="GetPath"/>
        /// </summary>
        public bool TryGetGridPath(ref NativeList<int2> pathResult, int2 gridStart, int2 gridTarget, PathConfig config)
        {
            if (!pathResult.IsCreated)
            {
                Debug.LogError("Invalid path array.");
                return false;
            }
            var debugSteps = new NativeList<JPSDebugStep>(Allocator.TempJob);

            var handle = SchedulePathJob(gridStart, gridTarget, ref pathResult, ref debugSteps, config);
            
            // Complete immediately for synchronous call
            handle.Complete();

            // Debug from path
            if (enableDebug)
            {
                LastJPSDebugSteps = new List<JPSDebugStep>(debugSteps.Length);
                foreach (var step in debugSteps) LastJPSDebugSteps.Add(step);
            }
            debugSteps.Dispose();
            return true;
        }

        /// <summary>
        /// Get the direct path output in grid-space.
        /// Internal version of <seealso cref="GetPathAsync"/>
        /// </summary>
        private IEnumerator GetGridPathAsync(NativeList<int2> pathResult, int2 gridStart, int2 gridTarget, PathConfig config, Action<bool> onComplete)
        {
            int expectedBuildID = buildID;
            NativeList<JPSDebugStep> debugSteps =  new NativeList<JPSDebugStep>(Allocator.TempJob);
            var handle = SchedulePathJob(gridStart, gridTarget, ref pathResult, ref debugSteps, config);

            try
            {
                while (!handle.IsCompleted) yield return null;
                handle.Complete(); // Redundancy, ensure handle is complete
                
                // Grid was rebuilt - invalid path request
                if (expectedBuildID != buildID)
                {
                    onComplete?.Invoke(false);
                    yield break;
                }

                // Debug from path
                if (enableDebug)
                {
                    LastJPSDebugSteps = new List<JPSDebugStep>(debugSteps.Length);
                    foreach (var step in debugSteps) LastJPSDebugSteps.Add(step);
                }
                onComplete?.Invoke(true);
            }
            finally
            {
                if (debugSteps.IsCreated) debugSteps.Dispose();
            }
        }

        /// <summary>
        /// Get a path between two world space points.
        /// </summary>
        /// <param name="start"> Start position in world space.</param>
        /// <param name="target"> Target/end position in world space.</param>
        /// <param name="collapseSkips"> Collapse jump points? Needed for movement to avoid skipping tiles.</param>
        /// <returns> Resulting path in worldspace </returns>
        public List<Vector3> GetPath(Vector3 start, Vector3 target, PathConfig config)
        {
            if (!IsBuilt)
            {
                Debug.LogError("Grid not built.");
                return null;
            }
            
            int2 startGrid = GetGridPosition(start);
            int2 targetGrid = GetGridPosition(target);
            var pathResult = new NativeList<int2>(Allocator.TempJob);
            
            if (!TryGetGridPath(ref pathResult, startGrid, targetGrid, config))
            {
                pathResult.Dispose();
                return new List<Vector3>(); // Empty path
            }

            GridToWorldPath(in pathResult, out List<Vector3> worldPath, collapseSkips:config.CollapseSkips);
            
            pathResult.Dispose();
            return worldPath;
        }

        /// <summary>
        /// Get a path between two world space points.
        /// </summary>
        /// <param name="start"> Start position in world space.</param>
        /// <param name="target"> Target/end position in world space.</param>
        /// <param name="pathOnComplete"> Resulting path in world space</param>
        /// <param name="collapseSkips"> Collapse jump points? Needed for movement to avoid skipping tiles.</param>
        public IEnumerator GetPathAsync(PathRequest request)
        {
            if (!IsBuilt)
            {
                Debug.LogError("Grid not built.");
                request.OnComplete?.Invoke(new PathResult(){Config = request.Config});
                yield break;
            }

            int2 startGrid = GetGridPosition(request.Start);
            int2 targetGrid = GetGridPosition(request.Target);
            var resultingPath = new NativeList<int2>(Allocator.TempJob);

            PathResult.EPathJobStatus jobStatus = PathResult.EPathJobStatus.Success;
            yield return GetGridPathAsync(resultingPath, startGrid, targetGrid, request.Config, onComplete: (bool res) =>
            {
                if(!res) jobStatus = PathResult.EPathJobStatus.Cancelled_Rebuild;
            });

            List<Vector3> worldPath = null;
            if (jobStatus == PathResult.EPathJobStatus.Success)
            {
                GridToWorldPath(in resultingPath, out worldPath, collapseSkips:request.Config.CollapseSkips);
            }

            var pathResult = new PathResult()
            {
                Path = worldPath,
                Status = jobStatus,
                Config = request.Config
            };
                
            resultingPath.Dispose();
            request.OnComplete?.Invoke(pathResult);
        }
        #endregion // PathRequests
        
        #region Queue
        public struct QueuedPathJob
        {
            public PathRequest Request;
            public PathHandle Handle;
            public int BuildID; // Track ID for grid rebuilding to cancel queued path requests
        }
        
        private readonly Queue<PathRequest> _pendingRequests = new();
        private readonly List<QueuedPathJob> _activeJobs = new();
        [SerializeField] private int maxConcurrentJobs = 8;
        
        public void RequestPathQueued(PathRequest request)
        {
            _pendingRequests.Enqueue(request);
        }

        public IEnumerator WaitForAllJobs()
        {
            // Wait until no pending or active jobs remain
            while (_pendingRequests.Count > 0 || _activeJobs.Count > 0)
            {
                yield return null;
            }
        }
        
        private void Update()
        {
            if (!IsBuilt) return;

            // Schedule new jobs if capacity allows
            while (_pendingRequests.Count > 0 && _activeJobs.Count < maxConcurrentJobs)
            {
                var request = _pendingRequests.Dequeue();
                
                // Schedule Job:
                var handle = CreatePathHandle(
                    GetGridPosition(request.Start),
                    GetGridPosition(request.Target),
                    Allocator.TempJob,
                    request.Config
                );
                _activeJobs.Add(new QueuedPathJob { Request = request, Handle = handle, BuildID = buildID});
            }

            // Check for completed:
            for (int i = _activeJobs.Count - 1; i >= 0; --i)
            {
                var job = _activeJobs[i];
                if (!job.Handle.IsCompleted) continue;
                job.Handle.Complete(); // Ensure completed
                var status = PathResult.EPathJobStatus.Success;
                if (job.BuildID != buildID) status = PathResult.EPathJobStatus.Cancelled_Rebuild;

                List<Vector3> worldPath = null;
                if (status == PathResult.EPathJobStatus.Success)
                {
                    GridToWorldPath(job.Handle.Path, out worldPath, job.Request.Config.CollapseSkips);
                }
                job.Handle.Dispose();
                var result = new PathResult()
                {
                    Path = worldPath,
                    Status = status,
                    Config = job.Request.Config
                };
                job.Request.OnComplete?.Invoke(result);
                _activeJobs.RemoveAt(i);
            }
        }

        private void CancelAllJobs(PathResult.EPathJobStatus reason)
        {
            CancelPendingJobs(reason);
            CancelActiveJobs(reason);
        }
        
        /// <summary>
        /// Immediate cancellation of all jobs.
        /// Generally preferred to let active jobs finish naturally to not block the main thread
        /// </summary>
        /// <param name="reason"></param>
        public void CancelActiveJobs(PathResult.EPathJobStatus reason)
        {
            for (int i = _activeJobs.Count - 1; i >= 0; --i)
            {
                var job = _activeJobs[i];
                job.Handle.Complete();
                job.Handle.Dispose();

                job.Request.OnComplete?.Invoke(new PathResult
                {
                    Status = reason,
                    Path = null,
                    Config = job.Request.Config
                });
            }
            _activeJobs.Clear();
        }
        
        private void CancelPendingJobs(PathResult.EPathJobStatus reason)
        {
            while (_pendingRequests.Count > 0)
            {
                var req = _pendingRequests.Dequeue();
                req.OnComplete?.Invoke(new PathResult
                {
                    Status = reason,
                    Path = null, 
                    Config = req.Config
                });
            }
        }

        private void OnDisable()
        {
            CancelAllJobs(PathResult.EPathJobStatus.Cancelled_Lifetime);
        }

        protected override void OnDestroy()
        {
            CancelAllJobs(PathResult.EPathJobStatus.Cancelled_Lifetime);
            base.OnDestroy();
        }

        #endregion // Queue

        
        #region Helpers
        /// <summary>
        /// Convert 2D discrete grid position to 3D continuous position.
        /// </summary>
        public Vector3 GetWorldPosition(int2 gridPosition)
        {
            Vector3 local = LocalLeftCorner +  new Vector3(cellSize * (gridPosition.x + 0.5f), cellSize * (gridPosition.y + 0.5f),0f);
            return transform.TransformPoint(local);
        }

        /// <summary>
        /// Convert 3D continuous position to 2D discrete grid position.
        /// </summary>
        public int2 GetGridPosition(Vector3 worldPosition)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            localPos -= LocalLeftCorner;

            int x = (int)math.floor(localPos.x / cellSize);
            int y = (int)math.floor(localPos.y / cellSize);

            x = math.clamp(x, 0, gridSize.x - 1);
            y = math.clamp(y, 0, gridSize.y - 1);

            return new int2(x, y);
        }

        private void GridToWorldPath(in NativeList<int2> gridPath, out List<Vector3> worldPath, bool collapseSkips)
        {
            worldPath = new List<Vector3>();
            if (gridPath.Length > 0)
            {
                if (!collapseSkips || (pathingAlgorithm is EPathingAlgorithm.AStar4 or EPathingAlgorithm.AStar8))
                {
                    for (int i = 0; i < gridPath.Length; ++i)
                    {
                        worldPath.Add(GetWorldPosition(gridPath[i]));
                    }
                }
                else{
                    var prev = gridPath[0];
                    worldPath.Add(GetWorldPosition(prev));
                    for (int i = 1; i < gridPath.Length; ++i)
                    {
                        var cur = gridPath[i];
                        var delta = cur - prev;

                        // Ensure axis-aligned
                        if (delta.x != 0 && delta.y != 0) Debug.LogError("Diagonal segment found in path, but only 4-direction movement allowed.");

                        int2 step = new int2(
                            delta.x == 0 ? 0 : math.sign(delta.x),
                            delta.y == 0 ? 0 : math.sign(delta.y)
                        );
                        var p = prev;
                        while (!p.Equals(cur))
                        {
                            p += step;
                            worldPath.Add(GetWorldPosition(p));
                        }
                        prev = cur;
                    }
                }
            }
        }
        #endregion // Helpers
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if (!IsBuilt) return;

            // Visualize raycasts:
            Gizmos.color = Color.blue;
            var corners = new int2[]{new int2(0,0),new int2(gridSize.x-1,0),new int2(0,gridSize.y-1),new int2(gridSize.x-1,gridSize.y-1)} ;
            foreach (var pos in corners)
            {
                Vector3 localCenter = LocalLeftCorner + new Vector3( cellSize * (pos.x + 0.5f), cellSize * (pos.y + 0.5f), 0f);
                Vector3 worldCenter = transform.TransformPoint(localCenter);

                var start = worldCenter + transform.forward * collisionSettings.VerticalOffset;
                var dir = -transform.forward;
                Gizmos.DrawLine(start, start + dir*10f);
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.white;
            var leftCorner = LocalLeftCorner;
            
            for (int x = 0; x < gridSize.x; ++x)
            {
                for (int y = 0; y < gridSize.y; ++y)
                {
                    int idx = y * gridSize.x + x;
                    var center = leftCorner + new Vector3(cellSize / 2f + cellSize * x, cellSize / 2f + cellSize * y, 0);
                    
                    if (!tileGrid.StaticTraversable[idx])
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(center, new Vector3(cellSize * 0.25f, cellSize * 0.25f, 0));
                    }
                    
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireCube(center, new Vector3(cellSize, cellSize, 0));
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
        
        public override void Dispose()
        {
            tileGrid?.Dispose();
        }
    }
}
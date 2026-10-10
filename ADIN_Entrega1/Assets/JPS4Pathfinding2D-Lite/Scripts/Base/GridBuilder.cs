    using System;
    using System.Collections.Generic;
    using JPS4.Editor;
        using JPS4.Optimized;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    using static JPS4.JPS4Utils;

    namespace JPS4
    {
        /// <summary>
        /// Which pathing algorithm to use when generating paths on a <see cref="GridBuilder"/> object.
        /// </summary>
        public enum EPathingAlgorithm
        {
            JPS4_Horizontal, // JPS4 (Jump Point Search 4-connected) with horizontal-first canonical ordering
            JPS4_Vertical,   // JPS4 (Jump Point Search 4-connected) with vertical-first canonical ordering 
            AStar4,          // Traditional A* (4-connected)
            AStar8           // Traditional A* (8-connected)
        }
        
        /// <summary>
        /// What kind of colliders are we ray casting against when building collision for the <see cref="GridBuilder"/> obstacle matrices.
        /// </summary>
        public enum ECollisionType
        {
            Collider2D, // Use Raycast2D per cell
            Collider3D, // Use Raycast3D per cell
            Custom // No collision, manually supplied / rebuilt
        }
        
        /// <summary>
        /// Collision settings for building collision on the <see cref="GridBuilder"/> to generate the <see cref="TileGrid"/> obstacle matrices.
        /// </summary>
        [System.Serializable]
        public struct CollisionSettings
        {
            public float VerticalOffset;    // How far away from grid forward (blue axis) to offset before casting collision ray (1f)
            public LayerMask CollisionMask; // What layers count as obstacles
            public ECollisionType CollisionType; // What type of ray-casting to use
        }

        /// <summary>
        /// Internal wrapper with a pathfinding request handle.
        /// </summary>
        public struct PathHandle : IDisposable
        {
            public JobHandle Handle;      // Default-completed handle for synchronous pathfinding execution.
            public NativeList<int2> Path; // Output Path of grid points (this may not be collapsed for algorithms w/ skip nodes)
            public NativeList<JPSDebugStep> DebugSteps; // Output debug info on completion

            public bool IsCompleted => Handle.IsCompleted; // Status of the job
            public void Complete() => Handle.Complete();   // Immediately complete the JobHandle (synchronous) 

            public void Dispose()
            {
                if (Path.IsCreated) Path.Dispose();
                if (DebugSteps.IsCreated) DebugSteps.Dispose();
            }
        }

        /// <summary>
        /// Wrapper to manage a <see cref="TileGrid"/> collision matrix and provide the API to execute pathfinding queries.
        /// The grid is allocated or replaced on <see cref="PathfindingGridBuilder.Build"/>, and disposed on destroy or re-build.
        /// </summary>
        /// <remarks>
        /// The "grid" position of tiles refers to 2D coordinates (bottom left = 0,0) whereas "world" position refers to 3D continuous positions in the scene.
        /// Pathfinding algorithms operate on the 2D grid only where floating point precision is not a concern.
        /// </remarks>
        public abstract class GridBuilder : MonoBehaviour, IDisposable
        {
            /// <summary> Grid reference, generated on <see cref="PathfindingGridBuilder.Build"/></summary>
            public abstract TileGrid Grid { get; }
            
            /// <summary> Side length of a grid cell / tile in world space. </summary>
            public abstract float CellSize { get; }
            
            /// <summary> World position of the lower left corner of the grid. </summary>
            public abstract Vector3 LocalLeftCorner { get; }
            
            [Header("Pathing")]
            // FREE VERSION: Executes synchronously on the main thread - the selected algorithm
            // blocks the caller until it finishes. No Burst, no worker threads, no NativeArrays.
            // JPS4+ Pro dispatches a [BurstCompile] IJob for every algorithm, running off-thread
            // with SIMD optimization and zero GC allocations:
            // https://assetstore.unity.com/packages/slug/jps4-2d-grid-pathfinding-355438
            [SerializeField, Tooltip("Algorithm used for pathfinding queries.\n\nFREE: Runs synchronously on the main thread (no Burst, no worker threads).\nJPS4+ Pro: Every algorithm runs as a [BurstCompile] IJob on Unity worker threads - up to 10-20x faster, zero GC, fully non-blocking.")]
            protected EPathingAlgorithm pathingAlgorithm;
            
            [SerializeField, ShowIfEnum("pathingAlgorithm", EPathingAlgorithm.AStar8)]
            [Tooltip("For A*, can agents move diagonally between adjacent blocking obstacles?")]
            protected bool allowCutCorners = false;
            
            /// <summary> True if the <see cref="TileGrid"/> object has been built and allocated. </summary>
            public bool IsBuilt => Grid != null && Grid.IsValid;
            
            /// <summary> Collision settings for building collision on the <see cref="GridBuilder"/> to generate the <see cref="TileGrid"/> obstacle matrices. </summary>
            public virtual CollisionSettings CollisionSettings { get; }

            /// <summary>
            /// Core path execution interface. Handles direct execution for the configured algorithm.
            /// </summary>
            /// <param name="gridStart"> Path start tile in grid space.</param>
            /// <param name="gridTarget"> Path end tile in grid space.</param>
            /// <param name="pathResult"> Output path (list of grid points).</param>
            /// <param name="debugSteps"> Output debugging information when preprocessors directives are active for debugging.</param>
            /// <param name="config"> Extra configuration settings for the internal job (collapse-skips, closest-target).</param>
            /// <returns>
            /// FREE VERSION: always returns a default-completed <see cref="JobHandle"/> - no real Job
            /// is ever scheduled. JPS4+ Pro returns a live <see cref="JobHandle"/> from a Burst Job
            /// running on worker threads.
            /// </returns>
            /// <remarks>
            /// <b>FREE VERSION</b> - Executes synchronously on the main thread using standard managed
            /// C# collections. Blocks until the path search finishes; no Burst, no worker threads.
            /// <br/><br/>
            /// <b>JPS4+ Pro</b> schedules a <c>[BurstCompile] IJob</c> here:<br/>
            /// - Worker-thread execution - main thread is never blocked<br/>
            /// - Burst SIMD - up to 10-20x faster per query than this implementation<br/>
            /// - NativeArray storage - zero GC pressure<br/>
            /// - Returns a live <see cref="JobHandle"/> you can dependency-chain or <c>Complete()</c> later<br/>
            /// Upgrade: https://assetstore.unity.com/packages/slug/jps4-2d-grid-pathfinding-355438
            /// </remarks>
            protected JobHandle SchedulePathJob(int2 gridStart, int2 gridTarget, ref NativeList<int2> pathResult, ref NativeList<JPSDebugStep> debugSteps, PathConfig config)
            {
                if (!IsBuilt)
                {
                    Debug.LogError("Invalid grid.");
                    return default;
                }

                // Convert Native collections to standard C# collections
                bool[] gridTraversable = Grid.StaticTraversable.ToArray();
                bool[] dynamicObstacles = (config.QueryDynamic ? Grid.DynamicBlocked : Grid.EmptyMatrix).ToArray();
                byte[] edgeBlocks = config.QueryDynamic ? Grid.DynamicEdges.ToArray() : null; // Only use edge blocks if querying dynamic
                var pathResultList = new List<int2>();
                var debugStepsList = new List<JPS4Utils.JPSDebugStep>();

                if (pathingAlgorithm == EPathingAlgorithm.JPS4_Horizontal)
                {
                    var pathAlgorithm = new JPS4PathfindingAlgorithm
                    {
                        GridSize = Grid.Size,
                        GridTraversable = gridTraversable,
                        DynamicObstacles = dynamicObstacles,
                        DynamicBlockedEdges = edgeBlocks,
                        Start = gridStart,
                        Target = gridTarget,
                        PathResult = pathResultList,
                        DebugSteps = debugStepsList,
                        UseClosestTarget = config.UseClosestTarget,
                        CANONICAL_ORDERING = ECanonicalOrdering.HorizontalFirst
                    };
                    pathAlgorithm.Execute();
                    pathResult.Clear();
                    foreach (var point in pathResultList)
                    {
                        pathResult.Add(point);
                    }
                    debugSteps.Clear();
                    foreach (var step in debugStepsList)
                    {
                        debugSteps.Add(step);
                    }
                }
                else if (pathingAlgorithm == EPathingAlgorithm.JPS4_Vertical)
                {
                    var pathAlgorithm = new JPS4PathfindingAlgorithm
                    {
                        GridSize = Grid.Size,
                        GridTraversable = gridTraversable,
                        DynamicObstacles = dynamicObstacles,
                        DynamicBlockedEdges = edgeBlocks,
                        Start = gridStart,
                        Target = gridTarget,
                        PathResult = pathResultList,
                        DebugSteps = debugStepsList,
                        UseClosestTarget = config.UseClosestTarget,
                        CANONICAL_ORDERING = ECanonicalOrdering.VerticalFirst
                    };
                    pathAlgorithm.Execute();
                    pathResult.Clear();
                    foreach (var point in pathResultList)
                    {
                        pathResult.Add(point);
                    }
                    debugSteps.Clear();
                    foreach (var step in debugStepsList)
                    {
                        debugSteps.Add(step);
                    }
                }
                else if(pathingAlgorithm == EPathingAlgorithm.AStar4)
                {
                    var pathAlgorithm = new AStarPathfindingAlgorithm
                    {
                        GridSize = Grid.Size,
                        GridTraversable = gridTraversable,
                        DynamicObstacles = dynamicObstacles,
                        DynamicBlockedEdges = edgeBlocks,
                        Start = gridStart,
                        Target = gridTarget,
                        PathResult = pathResultList,
                        DebugSteps = debugStepsList, 
                        AllowDiagonal = false,
                        CutCorners = false,
                        UseClosestTarget = config.UseClosestTarget
                    };
                    pathAlgorithm.Execute();
                    pathResult.Clear();
                    foreach (var point in pathResultList)
                    {
                        pathResult.Add(point);
                    }
                    debugSteps.Clear();
                    foreach (var step in debugStepsList)
                    {
                        debugSteps.Add(step);
                    }
                }
                else if(pathingAlgorithm == EPathingAlgorithm.AStar8)
                {
                    var pathAlgorithm = new AStarPathfindingAlgorithm
                    {
                        GridSize = Grid.Size,
                        GridTraversable = gridTraversable,
                        DynamicObstacles = dynamicObstacles,
                        DynamicBlockedEdges = edgeBlocks,
                        Start = gridStart,
                        Target = gridTarget,
                        PathResult = pathResultList,
                        DebugSteps = debugStepsList,
                        AllowDiagonal = true,
                        CutCorners = allowCutCorners,
                        UseClosestTarget = config.UseClosestTarget
                    };
                    pathAlgorithm.Execute();
                    pathResult.Clear();
                    foreach (var point in pathResultList)
                    {
                        pathResult.Add(point);
                    }
                    debugSteps.Clear();
                    foreach (var step in debugStepsList)
                    {
                        debugSteps.Add(step);
                    }
                }
                return default;
            }        
            
            protected virtual void OnDestroy()
            {
                Dispose();
            }

            public virtual void Dispose()
            {
                Debug.Log($"Disposed {gameObject.name}");
            }
        }
    }
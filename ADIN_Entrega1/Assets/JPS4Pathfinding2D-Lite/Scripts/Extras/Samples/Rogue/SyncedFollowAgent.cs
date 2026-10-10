using System;
using System.Collections;
using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// This agent will always pursue the active target.
    /// Movement stops when reaching the target or when blocked.
    /// </summary>
    public class SyncedFollowAgent : SyncedAgent
    {
        [SerializeField, Tooltip("Target this agent is trying to follow, can be static or dynamic.")]
        protected Transform target;
        
        [SerializeField, Tooltip("How many tiles away from the target should the agent stop using the static path?")] 
        private int targetRadius = 4;
        
        private PathfindingDynamicGridBuilder _grid;
        private Vector3 _lastPosition;
        private PathInstance _debugPath; // Tracked separate to avoid flickering
        private bool _useStrictPath = false; // Force dynamic target to be the next available node along the strict path?

        /// <summary>
        /// Initialize once with a grid, acts as an external mono-constructor.
        /// </summary>
        /// <param name="gridIn"> Grid reference this agent is querying against.</param>
        public void Initialize(PathfindingDynamicGridBuilder gridIn)
        {
            _grid = gridIn;
        }

        /// <summary>
        /// Execute the movement animation for this agent.
        /// </summary>
        public override IEnumerator StepTowardsTarget()
        {
            _lastPosition = transform.position;
            if (!isActiveAndEnabled || CurPath == null || CurPath.IsEmpty() || !CurPath.HasNext(out int nextIdx, mutate:true))
            {
                yield break;
            }
            _debugPath = CurPath;
            var nextPos = CurPath.Nodes[nextIdx];
            
            // Prevent movement that would collider with a dynamic-blocked position.
            if (_grid.IsDynamicBlocked(nextPos))
            {
                yield break;
            }

            yield return base.ExecuteMovement(nextPos);
        }

        /// <summary>
        /// Check if a new path can be recalculated and create a <see cref="PathRequest"/> for scheduling.
        /// Does NOT queue the request.
        /// </summary>
        /// <param name="request">A handle that can be queued into a <see cref="GridBuilder"/></param>
        /// <param name="queryDynamic"> Check for dynamic obstacles?</param>
        public bool TryCalculatePath(out PathRequest request, bool queryDynamic=false)
        {
            request = null;
            if (!target || !isActiveAndEnabled) return false;
            
            var startPos = transform.position;
            var targetPos = target.position;

            if (Vector3.Distance(startPos, targetPos) <= 0.0001f) return false; // Already reached target

            //bool hasMoved = Vector3.Distance(lastPosition, transform.position) > 0.001f;
            request = new PathRequest( transform.position, target.position, RefreshPath, collapseSkips:true, queryDynamic:queryDynamic);
            return true;
        }
        
        /// <summary>
        /// Check if a new path can be recalculated and create a <see cref="PathRequest"/> for scheduling.
        /// Does NOT queue the request.
        /// </summary>
        /// <param name="request">A handle that can be queued into a <see cref="GridBuilder"/></param>
        /// <param name="queryDynamic"> Check for dynamic obstacles?</param>
        public bool TryRecalculateAlongPath(out PathRequest request, bool queryDynamic=true)
        {
            // Optional override from IsStaticPathValid, use the actual target instead of along the static path
            if (!_useStrictPath)
            {
                return TryCalculatePath(out request, queryDynamic);
            }
            
            // Validate and check along the current (static) path
            request = null;
            if (!target || !isActiveAndEnabled || CurPath == null) return false;
            
            var startPos = transform.position;
            var targetPos = target.position;
            if (Vector3.Distance(startPos, targetPos) <= 0.0001f) return false; // Already reached target

            // Get the next available node along the path
            // If the path is fully blocked, use the actual target 
            int idx =0;
            for (idx = CurPath.CurIndex; idx < CurPath.Nodes.Count; idx++)
            {
                if (_grid.IsDynamicBlocked(CurPath.Nodes[idx])) continue;
                else break;
            }
            if (idx < CurPath.Nodes.Count) targetPos = CurPath.Nodes[idx];

            request = new PathRequest(transform.position, targetPos, RefreshPath, collapseSkips:true, queryDynamic:queryDynamic);
            return true;
        }

        /// <summary>
        /// Check if the currently queued static path should be kept or if we should recalculate with a path taking into account dynamic obstacles.
        /// </summary>
        /// <returns> Should the static path be kept this next movement step? </returns>
        public bool IsStaticPathValid()
        {
            _useStrictPath = true;
            if (TryGetNextPosition(out var nextStaticPos) && Vector3.Distance(nextStaticPos, _lastPosition) < 0.001f)
            {
                // Don't allow following a static path to move to the spot we just came from (or static will often override dynamic)
                _useStrictPath = false;
                return false;
            }
            if (CurPath != null && Vector3.Distance(target.position, transform.position) <= 0.001f+_grid.CellSize*targetRadius)
            {
                // Use a fully dynamic path when in radius
                _useStrictPath = false; 
                return false;
            }
            
            // Keep the static path as long as the next node isn't blocked:
            if (TryGetNextPosition(out var next))
            {
                return !_grid.IsDynamicBlocked(next);
            }
            return false;
        }
        
        protected override void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (_debugPath == null) return;
            Gizmos.color =  gizmoColor;
            for (var i = _debugPath.CurIndex; i < _debugPath.Nodes.Count-1; i++)
            {
                Gizmos.DrawLine(_debugPath.Nodes[i], _debugPath.Nodes[i+1]);
            }            
        }
    }
}
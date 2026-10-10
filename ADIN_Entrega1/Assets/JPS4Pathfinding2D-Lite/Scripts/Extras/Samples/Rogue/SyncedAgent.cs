using System.Collections;
using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// An agent that is controlled externally in synced, discrete "steps"
    /// </summary>
    public class SyncedAgent : PathingAgentBase
    {
        [SerializeField] protected bool drawGizmos = true;
        [SerializeField] protected Color gizmoColor = Color.yellow;

        public PathInstance CurPath { get; protected set; } // Current path being used (for movement)
        
        protected PathingMovementComponent movementComponent;
        
        protected virtual void Awake()
        {
            movementComponent ??= GetComponent<PathingMovementComponent>();
        }
        
        /// <summary>
        /// Try to get the next position on the current path.
        /// </summary>
        /// <param name="nextPos"> Position in world space this agent can move to next.</param>
        /// <returns>Success of the query.</returns>
        public bool TryGetNextPosition(out Vector3 nextPos)
        {
            nextPos = Vector3.zero;
            if (!isActiveAndEnabled || CurPath == null || CurPath.IsEmpty() || !CurPath.HasNext(out int nextIdx, mutate:false))
            {
                return false;
            }
            nextPos = CurPath.Nodes[nextIdx];
            return true;
        }

        /// <summary>
        /// Check if a new path can be recalculated and create a <see cref="PathRequest"/> for scheduling.
        /// Does NOT queue the request.
        /// </summary>
        /// <param name="request">A handle that can be queued into a <see cref="GridBuilder"/></param>
        /// <param name="targetPos">Target world position for the path.</param>
        /// <param name="queryDynamic"> Check for dynamic obstacles?</param>
        public bool TryCalculatePath(out PathRequest request, Vector3 targetPos, bool queryDynamic=false)
        {
            request = null;
            if (!isActiveAndEnabled) return false;
            
            var startPos = transform.position;

            if (Vector3.Distance(startPos, targetPos) <= 0.0001f) return false; // Already reached target

            request = new PathRequest( transform.position, targetPos, RefreshPath, collapseSkips:true, queryDynamic:queryDynamic);
            return true;
        }
        
        /// <summary>
        /// Internal callback on path complete, set the active Path.
        /// </summary>
        protected void RefreshPath(PathResult result)
        {
            if (result.Status == PathResult.EPathJobStatus.Success)
            {
                //Debug.Log($"Path generated with length {result.Path.Count}");
                CurPath = new(result);
            }
        }
        
        /// <summary>
        /// Execute the movement animation for this agent, moving along the current path toward its target.
        /// </summary>
        public virtual IEnumerator StepTowardsTarget()
        {
            if (!isActiveAndEnabled || CurPath == null || CurPath.IsEmpty() || !CurPath.HasNext(out int nextIdx, mutate:true))
            {
                yield break;
            }
            var nextPos = CurPath.Nodes[nextIdx];
            yield return ExecuteMovement(nextPos);
        }

        protected virtual IEnumerator ExecuteMovement(Vector3 nextPos)
        {
            yield return movementComponent.MoveToAsync(nextPos);
        }
        
        protected virtual void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (CurPath == null) return;
            Gizmos.color =  gizmoColor;
            for (var i = CurPath.CurIndex; i < CurPath.Nodes.Count-1; i++)
            {
                Gizmos.DrawLine(CurPath.Nodes[i], CurPath.Nodes[i+1]);
            }            
        }
    }
}
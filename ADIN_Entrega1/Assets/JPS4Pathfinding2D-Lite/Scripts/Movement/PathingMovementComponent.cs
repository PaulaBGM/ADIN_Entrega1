using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// Used by a <see cref="DemoPathingAgent"/> to move to another position 
    /// </summary>
    public class PathingMovementComponent : MonoBehaviour
    {
        protected DemoPathingAgent owningAI; // AI component that issues commands
        public bool IsMoving { get; protected set; }

        /// <summary>
        /// Assuming we start at the first node of the path, move in discrete steps along the path.
        /// </summary>
        public virtual void MoveAlongPath(DemoPathingAgent pawnAI)
        {
            owningAI = pawnAI;
        }

        /// <summary>
        /// Move from current position to the target (usually for a single tile distance)
        /// </summary>
        public virtual IEnumerator MoveToAsync(Vector3 target)
        {
            yield break;
        }

        /// <summary>
        /// Stop movement, can be resumed with <see cref="Continue"/> or started with a new path using <see cref="MoveAlongPath"/>
        /// </summary>
        public virtual void Stop()
        {
            
        }

        /// <summary>
        /// Continue moving along the current path for the given pawn. 
        /// </summary>
        public virtual void Continue(DemoPathingAgent pawnAI)
        {
            owningAI = pawnAI;
        }
        
        protected virtual void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
using System.Collections.Generic;
using System.Net.Mail;
using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// A completed path instance assignable to an agent.
    /// Used to establish the fully intended movement path of an agent before/during movement.
    /// </summary>
    public class PathInstance
    {
        public bool IsEmpty() => Nodes.Count == 0;
        public List<Vector3> Nodes;
        public int CurIndex = 0; // Coupled with the path instance to avoid indexing errors, on a new path this is zero

        public bool HasNext(out int nextIdx, bool mutate=false)
        {
            nextIdx = CurIndex + 1;
            if (nextIdx < Nodes.Count)
            {
                if (mutate) CurIndex = nextIdx;
                return true;
            }
            return false;
        }

        public PathInstance(List<Vector3> nodes)
        {
            Nodes = nodes; // Note: this is not a deep copy, use .ToList() if you need to mutate the node list externally and don't want to affect this
            Nodes ??= new(); // Ensure no nulls
        }

        public PathInstance(PathResult result)
        {
            Nodes = result.Path;
            Nodes ??= new(); // Ensure no nulls
        }

        /// <summary>
        /// Is the target node matching the desired target strictly or did the path generate a "closest node"
        /// </summary>
        /// <param name="threshold"> World distance between external target and the final node for this to be counted as a strict match.</param>
        public bool HasStrictTarget(Vector3 target, float threshold=0.001f)
        {
            if (Nodes == null || Nodes.Count == 0) return false;
            var lastNode = Nodes[^1];
            return Vector3.Distance(target, lastNode) < threshold;
        }
    }
    
    /// <summary>
    /// Attached to a pawn / character for pathfinding to a target using <see cref="PathfindingGridBuilder"/>
    /// If you need more complicated collision inherit from <seealso cref="IIrregularDynamicObstacle"/>
    /// </summary>
    public class PathingAgentBase : MonoBehaviour, IDynamicObstacle
    {
        public virtual Vector3 WorldPosition => transform.position;
        public bool TryGetGameObject(out GameObject obj)
        {
            obj = gameObject;
            return obj;
        }
    }
}
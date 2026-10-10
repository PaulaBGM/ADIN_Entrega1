using JPS4.Editor;
using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// A dynamic set of blocking edges that act as walls between tiles.
    /// </summary>
    public class SampleDynamicEdgeSet : MonoBehaviour, IDynamicEdgeSet
    {
        [SerializeField, Tooltip("Which edges of this cell cannot be moved through?")] 
        private EEdgeFlags blockedEdges;

        [Header("Edge Sprites")] 
        [SerializeField] private SpriteRenderer leftEdge;
        [SerializeField] private SpriteRenderer rightEdge;
        [SerializeField] private SpriteRenderer upEdge;
        [SerializeField] private SpriteRenderer downEdge;
        
        public virtual Vector3 WorldPosition => transform.position;
        public EEdgeFlags BlockedEdges => blockedEdges;

        public void SetBlocking(EEdgeFlags edgesBlocked, in PathfindingDynamicEdgeGridBuilder builder)
        {
            blockedEdges = edgesBlocked; // Update serialized field
            
            var isNoneBlocked = blockedEdges == EEdgeFlags.None;
            
            if (isNoneBlocked)
            { // CASE: this tile does not block edges
                builder.UnregisterDynamicEdgeSet(this);
            }
            else
            { // CASE: this tile is only an edge obstacle
                builder.RegisterDynamicEdgeSet(this);
            }
            // Note: in practice, be sure to keep track of whether this is registered or not already
            
            UpdateVisuals();
        }
        
        public bool TryGetGameObject(out GameObject obj)
        {
            obj = gameObject;
            return obj;
        }
        
        /// <summary>
        /// Update debug visuals when state changes.
        /// </summary>
        [Button("Update Visuals")]
        public void UpdateVisuals()
        {
            upEdge.gameObject.SetActive(BlockedEdges.HasFlag(EEdgeFlags.Up));
            downEdge.gameObject.SetActive(BlockedEdges.HasFlag(EEdgeFlags.Down));
            leftEdge.gameObject.SetActive(BlockedEdges.HasFlag(EEdgeFlags.Left));
            rightEdge.gameObject.SetActive(BlockedEdges.HasFlag(EEdgeFlags.Right));
        }
    }
}
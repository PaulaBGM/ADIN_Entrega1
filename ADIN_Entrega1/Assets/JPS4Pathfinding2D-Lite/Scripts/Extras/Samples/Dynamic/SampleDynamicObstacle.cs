using JPS4.Editor;
using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// A simple dynamic obstacle.
    /// </summary>
    public class SampleDynamicObstacle : MonoBehaviour, IDynamicObstacle
    {
        [SerializeField] private SpriteRenderer sprite;
        [SerializeField] private Color enabledColor;
        [SerializeField] private Color disabledColor;
        public virtual Vector3 WorldPosition => transform.position;

        // "Is this dynamic obstacle currently registered?
        public bool IsBlocking { get; private set; } = false;
        
        public void SetBlocking(bool isBlocked, in PathfindingDynamicEdgeGridBuilder builder)
        {
            if (isBlocked == IsBlocking)
            {
                // Do nothing, state has not changed
            }
            else if (isBlocked)
            {
                builder.RegisterDynamicObstacle(this);
            }
            else
            {
                builder.UnregisterDynamicObstacle(this);
            }

            IsBlocking = isBlocked;
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
            sprite.color = IsBlocking ? enabledColor : disabledColor;
        }
    }
}
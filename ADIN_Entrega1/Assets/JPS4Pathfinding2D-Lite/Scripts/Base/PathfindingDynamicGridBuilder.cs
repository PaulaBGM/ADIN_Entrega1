using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace JPS4
{
    public interface IDynamicObstacle
    {
        /// <summary> The blocking position of the tile (grid tile center) </summary>
        Vector3 WorldPosition { get; }

        public bool TryGetGameObject(out GameObject obj);
    }

    public interface IIrregularDynamicObstacle : IDynamicObstacle
    {
        /// <summary> World-space bounds of this obstacle. </summary>
        Bounds WorldBounds { get; }

        /// <summary> Layer mask for detecting this obstacle's blocking tiles within the bounds. </summary>
        LayerMask LayerMask { get; }
    }

    
    public class PathfindingDynamicGridBuilder : PathfindingGridBuilder
    {
        private readonly HashSet<IDynamicObstacle> _dynamicObstacles = new();
        protected readonly bool invalidatePathsOnDynamicRebuild = false;

        public void RegisterDynamicObstacle(IDynamicObstacle obstacle)
        {
            if (obstacle == null) return;
            _dynamicObstacles.Add(obstacle);
            if (obstacle.TryGetGameObject(out var obj))
            {
                int objLayerMask = 1 << obj.layer;
                bool overlaps = (CollisionSettings.CollisionMask & objLayerMask) != 0;
                Debug.Assert(!overlaps, $"Dynamic obstacle {obj.name} layer conflicts with grid collision mask.");
            }
        }

        public void UnregisterDynamicObstacle(IDynamicObstacle obstacle)
        {
            if (obstacle != null && _dynamicObstacles.Contains(obstacle)) _dynamicObstacles.Remove(obstacle);
        }

        /// <summary>
        /// Rebuild all dynamic obstacles, separate from the static build.
        /// This can be called in fixed intervals or when obstacles move. 
        /// </summary>
        public virtual void BuildDynamic()
        {
            if (!IsBuilt)
            {
                Debug.LogError("Static grid was not built prior to dynamic.");
                return;
            }

            if (invalidatePathsOnDynamicRebuild) buildID++; // This will invalidate any paths that have not completed

            // Reset dynamic grid:
            for (int i = 0; i < Grid.DynamicBlocked.Length; i++) Grid.DynamicBlocked[i] = false;

            // Build collision for additive dynamic obstacles.
            foreach (var obstacle in _dynamicObstacles)
            {
                if (obstacle == null) continue;
                BuildObstacleCollision(obstacle);
            }
        }

        public bool IsDynamicBlocked(Vector3 worldPosition)
        {
            var tile = GetGridPosition(worldPosition);
            return Grid.IsDynamicBlocked(tile);
        }

        private void BuildObstacleCollision(IDynamicObstacle obstacle)
        {
            if (collisionHandler == null) return;
            
            if (obstacle is IIrregularDynamicObstacle shape)
            { // Cast to find the shape of the obstacle and fill the dynamic grid
                var bounds = shape.WorldBounds;
                int2 min = GetGridPosition(bounds.min);
                int2 max = GetGridPosition(bounds.max);
                for (int x = min.x; x <= max.x; x++)
                {
                    for (int y = min.y; y <= max.y; y++)
                    {
                        Vector3 worldCenter = GetWorldPosition(new int2(x, y));
                        bool blocked = collisionHandler.IsTileBlocked(this, worldCenter, shape.LayerMask);
                        Grid.SetDynamicBlocked(x, y, blocked);
                    }
                }
            }
            else
            { // Default is single tile, no collision detection needed
                int2 tile = GetGridPosition(obstacle.WorldPosition);
                Grid.SetDynamicBlocked(tile.x, tile.y, true);
                return;
            }
        }

    }
}
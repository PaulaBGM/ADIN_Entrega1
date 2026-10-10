using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// Determines traversability matrix on grid build for <see cref="PathfindingGridBuilder"/>.
    /// </summary>
    public abstract class GridCollisionHandler
    {
        /// <summary>
        /// Re-build collision for the entire grid.
        /// </summary>
        /// <param name="builder">The grid builder that owns the traversability matrix.</param>
        public virtual void Build(GridBuilder builder)
        {
        }

        /// <summary>
        /// Check collision for a single world center of a tile.
        /// </summary>
        /// <param name="builder"> Use the collision settings from the builder (but with a custom mask)</param>
        public abstract bool IsTileBlocked(GridBuilder builder, Vector3 worldCenter, LayerMask layerMask);
    }
    
    /// <summary>
    /// Builds the grid collision by casting Physics2D rays to the center of each tile.
    /// </summary>
    public class RayCastCollisionHandler2D : GridCollisionHandler
    {
        public override void Build(GridBuilder builder)
        {
            var grid = builder.Grid;
            var trans = builder.transform;
            for (int x = 0; x < grid.Size.x; ++x)
            {
                for (int y = 0; y < grid.Size.y; ++y)
                {
                    Vector3 localCenter = builder.LocalLeftCorner + new Vector3( builder.CellSize * (x + 0.5f), builder.CellSize * (y + 0.5f), 0f);
                    Vector3 worldCenter = trans.TransformPoint(localCenter);

                    RaycastHit2D hit = Physics2D.Raycast(
                        worldCenter + trans.forward * builder.CollisionSettings.VerticalOffset,
                        -trans.forward,
                        Mathf.Infinity,
                        builder.CollisionSettings.CollisionMask
                    );
                    grid.SetStaticTraversable(x, y, !hit.collider);
                }
            }
        }

        public override bool IsTileBlocked(GridBuilder builder, Vector3 worldCenter, LayerMask layerMask)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                worldCenter + builder.transform.forward * builder.CollisionSettings.VerticalOffset,
                -builder.transform.forward,
                Mathf.Infinity,
                layerMask
            );
            return hit.collider;
        }
    }

    /// <summary>
    /// Builds the grid collision by casting Physics rays to the center of each tile.
    /// </summary>
    public class RayCastCollisionHandler3D: GridCollisionHandler
    {
        public override void Build(GridBuilder builder)
        {
            var grid = builder.Grid;
            var trans = builder.transform;
            for (int x = 0; x < grid.Size.x; ++x)
            {
                for (int y = 0; y < grid.Size.y; ++y)
                {
                    Vector3 localCenter = builder.LocalLeftCorner + new Vector3( builder.CellSize * (x + 0.5f), builder.CellSize * (y + 0.5f), 0f);
                    Vector3 worldCenter = trans.TransformPoint(localCenter);

                    var hit = Physics.Raycast(
                        worldCenter + trans.forward * builder.CollisionSettings.VerticalOffset,
                        -trans.forward,
                        Mathf.Infinity,
                        builder.CollisionSettings.CollisionMask
                    );
                    grid.SetStaticTraversable(x, y, !hit);
                }
            }
        }

        public override bool IsTileBlocked(GridBuilder builder, Vector3 worldCenter, LayerMask layerMask)
        {
            var hit = Physics.Raycast(
                worldCenter + builder.transform.forward * builder.CollisionSettings.VerticalOffset,
                -builder.transform.forward,
                Mathf.Infinity,
                builder.CollisionSettings.CollisionMask
            );
            return hit;
        }
    }
}
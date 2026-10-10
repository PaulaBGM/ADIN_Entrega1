using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using JPS4.Optimized;

namespace JPS4
{
    /// <summary>
    /// Bit flags for edge blocking in 4 cardinal directions.
    /// Each bit represents a blocked edge in ONLY that direction.
    /// </summary>
    [System.Flags]
    public enum EEdgeFlags : byte
    {
        None = 0,
        Right = 1 << 0,  // 0x01 - blocks movement to the right
        Left = 1 << 1,   // 0x02 - blocks movement to the left
        Up = 1 << 2,     // 0x04 - blocks movement up
        Down = 1 << 3,   // 0x08 - blocks movement down
        All = 0x0F       // All edges blocked
    }

    /// <summary>
    /// A blocking set of edges that can dynamically be added / removed.
    /// </summary>
    public interface IDynamicEdgeSet
    {
        /// <summary> The blocking position of the tile (grid tile center) </summary>
        Vector3 WorldPosition { get; }
        
        /// <summary> Direction flags indicating which edges are blocked from this tile. </summary>
        EEdgeFlags BlockedEdges { get; }

        bool TryGetGameObject(out GameObject obj);
    }

    /// <summary>
    /// Extended grid builder that supports dynamic edge obstacles - blocking movement
    /// between adjacent tiles in the specified direction rather than blocking entire tiles.
    /// 
    /// This allows for walls, doors, gates, and other barriers that block specific
    /// directions of movement while keeping both tiles traversable.
    ///
    /// DEV NOTE: This is an extra feature I didn't plan for the asset and is not included in the main JPS4+ / docs
    /// If you need help / want support with this in the main asset ping me at email me at silointeractivegames@gmail.com
    /// </summary>
    public class PathfindingDynamicEdgeGridBuilder : PathfindingDynamicGridBuilder
    {
        private readonly HashSet<IDynamicEdgeSet> _dynamicEdgeObstacles = new();

        public void RegisterDynamicEdgeSet(IDynamicEdgeSet obstacle)
        {
            if (obstacle == null) return;
            _dynamicEdgeObstacles.Add(obstacle);
        }

        public void UnregisterDynamicEdgeSet(IDynamicEdgeSet obstacle)
        {
            if (obstacle != null && _dynamicEdgeObstacles.Contains(obstacle))
            {
                _dynamicEdgeObstacles.Remove(obstacle);
            }
        }

        /// <summary>
        /// Build both dynamic tile obstacles and edge obstacles.
        /// Call this whenever edge obstacles change position or state.
        /// </summary>
        public override void BuildDynamic()
        {
            base.BuildDynamic(); // Build tile-level dynamic obstacles

            if (!IsBuilt)
            {
                Debug.LogError("Static grid was not built prior to dynamic edge build.");
                return;
            }

            // Reset all edge blocks
            for (int i = 0; i < Grid.DynamicEdges.Length; i++)
            {
                Grid.DynamicEdges[i] = 0;
            }

            // Apply edge obstacles
            foreach (var obstacle in _dynamicEdgeObstacles)
            {
                if (obstacle == null) continue;
                
                var tile = GetGridPosition(obstacle.WorldPosition);
                if (!IsInBounds(tile)) continue;

                int idx = Grid.IDX(tile);
                Grid.DynamicEdges[idx] = (byte)obstacle.BlockedEdges;
            }
        }

        /// <summary>
        /// Check if movement from one tile to an adjacent tile is blocked by an edge obstacle.
        /// </summary>
        /// <param name="from">Starting tile position</param>
        /// <param name="to">Destination tile position</param>
        /// <returns>True if the edge is blocked, false if movement is allowed</returns>
        public bool IsEdgeBlocked(int2 from, int2 to)
        {
            if (!Grid.DynamicEdges.IsCreated) return false;
            if (!IsInBounds(from) || !IsInBounds(to)) return true;

            int2 delta = to - from;
            
            // Only check for cardinal directions (4-connected movement)
            if (math.abs(delta.x) + math.abs(delta.y) != 1) return true;

            int fromIdx = Grid.IDX(from);
            byte edgeFlags = Grid.DynamicEdges[fromIdx];

            // Check which direction we're moving and test the corresponding bit
            if (delta.x == 1 && delta.y == 0) return (edgeFlags & (byte)EEdgeFlags.Right) != 0; // Moving right
            if (delta.x == -1 && delta.y == 0) return (edgeFlags & (byte)EEdgeFlags.Left) != 0; // Moving left
            if (delta.x == 0 && delta.y == 1) return (edgeFlags & (byte)EEdgeFlags.Up) != 0; // Moving up
            if (delta.x == 0 && delta.y == -1) return (edgeFlags & (byte)EEdgeFlags.Down) != 0; // Moving down

            return false;
        }

        /// <summary>
        /// Get the current edge block flags for a specific tile.
        /// </summary>
        public EEdgeFlags GetBlockedMovementFlags(int2 position)
        {
            if (!Grid.DynamicEdges.IsCreated || !IsInBounds(position)) return EEdgeFlags.None;

            return (EEdgeFlags)Grid.DynamicEdges[Grid.IDX(position)];
        }

        private bool IsInBounds(int2 pos)
        {
            return pos.x >= 0 && pos.x < Grid.Size.x && pos.y >= 0 && pos.y < Grid.Size.y;
        }

        /// <summary>
        /// Check if a tile position has any blocked edges.
        /// </summary>
        public bool HasBlockedEdges(int2 position)
        {
            return GetBlockedMovementFlags(position) != EEdgeFlags.None;
        }
    }
}

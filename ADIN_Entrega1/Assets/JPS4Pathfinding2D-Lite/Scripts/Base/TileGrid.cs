using System;
using Unity.Collections;
using Unity.Mathematics;

namespace JPS4
{
    /// <summary>
    /// Fully tile-based data wrapper for a pathfinding grid.
    /// This is what's used by the job requests and pathfinding algorithms.
    /// All 3D/world-space data or collision detection act on this object as helpers
    /// </summary>
    public class TileGrid : IDisposable
    {
        public readonly int2 Size;
        public NativeArray<bool> StaticTraversable;
        public NativeArray<bool> DynamicBlocked;
        public NativeArray<byte> DynamicEdges;
        
        // Used to switch quickly between using and not using the dynamic matrix
        // This isn't passed as another parameter on the job-level, so even for large grids
        public NativeArray<bool> EmptyMatrix;
            
        /// <summary>
        /// 2D tile position to 1D index for accessing the traversable native array. 
        /// </summary>
        public int IDX(int2 tile) => tile.y * Size.x + tile.x;
        public int IDX(int x, int y) => y * Size.x + x;
            
        public bool IsValid => StaticTraversable.IsCreated && DynamicBlocked.IsCreated;
            
        public bool IsTraversable(int2 tile)
        {
            int idx = IDX(tile);
            return StaticTraversable[idx] && !DynamicBlocked[idx];
        }

        public bool IsDynamicBlocked(int2 tile)
        {
            int idx = IDX(tile);
            return DynamicBlocked[idx];
        }

        public TileGrid(int2 size, Allocator allocator)
        {
            Size = size;
            StaticTraversable = new NativeArray<bool>(size.x * size.y, allocator);
            DynamicBlocked = new NativeArray<bool>(size.x * size.y, allocator);
            DynamicEdges = new NativeArray<byte>(size.x * size.y, allocator);
            EmptyMatrix = new NativeArray<bool>(size.x * size.y, allocator);
        }

        public TileGrid(int2 size, in NativeArray<bool> gridTraversableIn, Allocator allocator)
        {
            Size = size;
            if (size.x * size.y != gridTraversableIn.Length) throw new System.Exception("Grid size mismatch.");
            StaticTraversable = new NativeArray<bool>(gridTraversableIn.Length, allocator);
            DynamicBlocked = new NativeArray<bool>(gridTraversableIn.Length, allocator);
            DynamicEdges = new NativeArray<byte>(gridTraversableIn.Length, allocator);
            for (int i = 0; i < gridTraversableIn.Length; i++) StaticTraversable[i] = gridTraversableIn[i];
            EmptyMatrix = new NativeArray<bool>(size.x * size.y, allocator);
        }
            
        public void Dispose()
        {
            if (StaticTraversable.IsCreated) StaticTraversable.Dispose();
            if (DynamicBlocked.IsCreated) DynamicBlocked.Dispose();
            if (DynamicEdges.IsCreated) DynamicEdges.Dispose();
            if (EmptyMatrix.IsCreated) EmptyMatrix.Dispose();
        }

        public void SetStaticTraversable(int x, int y, bool isTraversable)
        {
            StaticTraversable[IDX(x, y)] = isTraversable;
        }

        public void SetDynamicBlocked(int x, int y, bool isBlocked)
        {
            DynamicBlocked[IDX(x, y)] = isBlocked;
        }

        public void SetDynamicEdge(int x, int y, byte edgeFlags)
        {
            DynamicEdges[IDX(x, y)] = edgeFlags;
        }
    }
}
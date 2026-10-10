using Unity.Mathematics;

namespace JPS4
{
    /// <summary>
    /// Utilty constants and info for Pathfinding algorithms.
    /// </summary>
    public static class JPS4Utils
    {
        public struct JPSDebugStep
        {
            public int2 current;
            public int2 source;
            public int2 from;

            public byte prunedMask;      // 4 bits: R L U D
            public byte naturalMask;     // optional
            public byte forcedMask;      // optional

            public int2 jumpRight;
            public int2 jumpLeft;
            public int2 jumpUp;
            public int2 jumpDown;

            public int2 rejected;
        }
        
        // Configuration constants
        public const int StepCost = 1; // Cost to move from one cell to an adjacent cell
        public const int MaxGScore = int.MaxValue - 1; // Maximum cost value (avoiding overflow)
        
        // 4-connected movement: can move right, left, up, or down (no diagonals)
        public static readonly int2[] DIRS =
        {
            new int2(1, 0), // Right
            new int2(-1, 0), // Left
            new int2(0, 1), // Up
            new int2(0, -1) // Down
        };
        
        public static readonly int RIGHT = 0;
        public static readonly int LEFT = 1;
        public static readonly int UP = 2;
        public static readonly int DOWN = 3;
        
        public static readonly int[] DIR_IDX_LUT = { DOWN, 0, LEFT, 0, RIGHT, 0, UP };
        
        public static int DirIndex(int2 dir)
        {
            // LEFT (-1,0) = -1 + 3*(1) = 2
            // RIGHT (1,0) =  1 + 3*(1) = 4
            // UP    (0,1) =  0 + 3(2)  = 6
            // DOWN (0,-1) =  0 + 3(0)  = 0
            return DIR_IDX_LUT[dir.x + 3 * (dir.y + 1)];
        }

        public static void SetPrunedMask(ref int mask, int dir)
        {
            mask |= 1 << dir;
        }

        public static void UnsetPrunedMask(ref int mask, int dir)
        {
            mask &= ~(1 << dir);
        }

        public static bool GetPrunedMask(int mask, int dir)
        {
            return (mask & (1 << dir)) != 0;
        }
    }
}
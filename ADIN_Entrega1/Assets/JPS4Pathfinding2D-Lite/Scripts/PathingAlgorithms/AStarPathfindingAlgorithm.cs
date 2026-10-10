//#define JPS4_DEBUG

using System.Collections.Generic;
using Unity.Mathematics;
using static JPS4.JPS4Utils;

namespace JPS4
{
    /// <summary>
    /// (FREE VERSION) Managed-heap, single-threaded A* implementation supporting both
    /// 4-connected and 8-connected grid movement.
    /// 
    /// Runs on the main thread only using standard C# List<T> and bool[]
    /// collections - no Burst compilation, no worker threads, no NativeContainers.
    /// Every call to Execute() blocks the caller until the search completes.
    /// 
    /// JPS4+ Pro replaces this with a [BurstCompile] IJob:
    /// - Burst SIMD compilation - up to 10-20x faster per query
    /// - Executes on Unity worker threads - main thread is never blocked
    /// - NativeArray/NativeList storage - zero GC allocations, no garbage spikes
    /// - Hundreds of paths per frame via parallel job scheduling
    /// Upgrade: https://assetstore.unity.com/packages/slug/jps4-2d-grid-pathfinding-355438
    /// </summary>
    public class AStarPathfindingAlgorithm
    {
        // Input data (read-only)
        public int2 GridSize;                      // Dimensions of the grid (width, height)
        public bool[] GridTraversable;             // True = walkable, False = obstacle
        public bool[] DynamicObstacles;            // True = blocked, False = free
        public byte[] DynamicBlockedEdges;           // Optional: edge blocking flags (4 bits per tile for 4 directions)
        public int2 Start;                         // Starting position
        public int2 Target;                        // Goal position
        public bool UseClosestTarget;              // If true & no path is found, reconstruct using the closest path to the target
        public bool AllowDiagonal;                 // True for 8-direction movement, false for 4-direction movement
        public bool CutCorners;                    // For diagonal movement, can we move between corner-touching obstacles?

        // Output data
        public List<int2> PathResult;              // Final path from start to target
        public List<JPSDebugStep> DebugSteps;

        private static readonly int2[] DIRS_4 =
        {
            new int2(1, 0),   // Right
            new int2(-1, 0),  // Left
            new int2(0, 1),   // Up
            new int2(0, -1)   // Down
        };

        private static readonly int2[] DIRS_8 =
        {
            new int2(1, 0),    // Right
            new int2(-1, 0),   // Left
            new int2(0, 1),    // Up
            new int2(0, -1),   // Down
            new int2(1, 1),    // Up-Right
            new int2(-1, 1),   // Up-Left
            new int2(1, -1),   // Down-Right
            new int2(-1, -1)   // Down-Left
        };

        // Configuration constants
        private const int StepCost4 = 1;
        private const int OrthogonalCost8 = 10;
        private const int DiagonalCost8 = 14;
        private const int MaxGScore = int.MaxValue - 1;

        /// <summary>
        /// Main execution method called by Unity's job system.
        /// </summary>
        public void Execute()
        {
            if (!InBounds(Start) || !InBounds(Target))
            {
                return;
            }

            if (PathResult == null) PathResult = new List<int2>();
            PathResult.Clear();

            if (Start.Equals(Target))
            {
                PathResult.Add(Start);
                return;
            }

            RunAStar();
        }

        private void RunAStar()
        {
            var openSet = ManagedMinHeap<int2>.Create();
            var gScore = new Dictionary<int2, int>();
            var cameFrom = new Dictionary<int2, int2>();

            Initialize(openSet, gScore, cameFrom);

            bool foundTarget = false;
            int2 bestPos = Start;
            bool hasBest = false;
            int bestH = MaxGScore;
            int bestG = MaxGScore;

            while (!openSet.IsEmpty)
            {
                if (!openSet.TryPopMin(out int2 currentPos)) break;
                int h = CalculateDistance(currentPos, Target);
                int g = GetGScore(gScore, currentPos);

                if (h < bestH || (h == bestH && g < bestG))
                {
                    bestH = h;
                    bestG = g;
                    bestPos = currentPos;
                    hasBest = true;
                }

                if (currentPos.Equals(Target))
                {
                    ReconstructPath(cameFrom, Target);
                    foundTarget = true;
                    break;
                }

                ExploreNeighbors(currentPos, gScore, cameFrom, openSet);
            }

            if (!foundTarget && UseClosestTarget && hasBest)
            {
                ReconstructPath(cameFrom, bestPos);
            }
        }

        private void Initialize(ManagedMinHeap<int2> openSet, Dictionary<int2, int> gScore, Dictionary<int2, int2> cameFrom)
        {
            gScore.Clear();
            cameFrom.Clear();

            gScore[Start] = 0;

            int fScore = CalculateDistance(Start, Target);
            openSet.TryPush(Start, fScore);
        }

        private void ExploreNeighbors(int2 currentPos, Dictionary<int2, int> gScore, Dictionary<int2, int2> cameFrom, ManagedMinHeap<int2> openSet)
        {
            int2[] directions = AllowDiagonal ? DIRS_8 : DIRS_4;
            int neighborCount = directions.Length;

#if JPS4_DEBUG
            int2 sourcePos = TryGetCameFromPosition(cameFrom, currentPos, out int2 source)
                ? source
                : new int2(-1, -1);
            if (DebugSteps == null) DebugSteps = new List<JPSDebugStep>();
            var debug = new JPSDebugStep
            {
                current = currentPos,
                source = sourcePos,
                from = sourcePos,
                rejected = new int2(-1, -1)
            };
#endif

            for (int i = 0; i < neighborCount; i++)
            {
                int2 delta = directions[i];
                int2 neighborPos = currentPos + delta;

                if (!IsValidNeighbor(currentPos, neighborPos)) continue;

                bool isDiagonal = delta.x != 0 && delta.y != 0;
                if (AllowDiagonal && isDiagonal && !CutCorners && !IsValidDiagonalMove(currentPos, neighborPos))
                {
                    continue;
                }

                int tentativeGScore = GetGScore(gScore, currentPos) + MovementCost(isDiagonal);

                if (tentativeGScore < GetGScore(gScore, neighborPos))
                {
                    UpdateNeighbor(neighborPos, tentativeGScore, currentPos, gScore, cameFrom, openSet);
                }
#if JPS4_DEBUG
                else debug.rejected = neighborPos;
                if (i == RIGHT) debug.jumpRight = neighborPos;
                if (i == LEFT) debug.jumpLeft = neighborPos;
                if (i == UP) debug.jumpUp = neighborPos;
                if (i == DOWN) debug.jumpDown = neighborPos;
#endif
            }

#if JPS4_DEBUG
            DebugSteps.Add(debug);
#endif
        }

        private int MovementCost(bool isDiagonal)
        {
            if (!AllowDiagonal) return StepCost4;
            return isDiagonal ? DiagonalCost8 : OrthogonalCost8;
        }

        private bool IsValidDiagonalMove(int2 current, int2 neighbor)
        {
            int2 delta = neighbor - current;

            if (delta.x == 0 || delta.y == 0) return true;

            int2 horizontal = current + new int2(delta.x, 0);
            int2 vertical = current + new int2(0, delta.y);

            return InBounds(horizontal) && IsTraversable(horizontal) &&
                   InBounds(vertical) && IsTraversable(vertical);
        }

        private bool IsEdgeBlocked(int2 from, int2 to)
        {
            if (DynamicBlockedEdges == null) return false;

            int2 delta = to - from;
            
            // Only valid for cardinal directions
            if (math.abs(delta.x) + math.abs(delta.y) != 1) return false;

            int toIdx = IDX(to);
            byte edgeFlags = DynamicBlockedEdges[toIdx];

            // Check if the destination cell's edge blocks entry from our direction:
            // Right=0x01, Left=0x02, Up=0x04, Down=0x08
            if (delta.x == 1 && delta.y == 0) return (edgeFlags & 0x02) != 0; // Moving right: check TO cell's LEFT edge
            if (delta.x == -1 && delta.y == 0) return (edgeFlags & 0x01) != 0; // Moving left: check TO cell's RIGHT edge
            if (delta.x == 0 && delta.y == 1) return (edgeFlags & 0x08) != 0; // Moving up: check TO cell's BOTTOM edge
            if (delta.x == 0 && delta.y == -1) return (edgeFlags & 0x04) != 0; // Moving down: check TO cell's TOP edge
            return false;
        }

        private bool IsValidNeighbor(int2 currentPos, int2 neighborPos)
        {
            return InBounds(neighborPos) && IsTraversable(neighborPos) && !IsEdgeBlocked(currentPos, neighborPos);
        }

        private void UpdateNeighbor(int2 neighborPos, int newGScore, int2 cameFromPos, Dictionary<int2, int> gScore, Dictionary<int2, int2> cameFrom, ManagedMinHeap<int2> openSet)
        {
            cameFrom[neighborPos] = cameFromPos;
            gScore[neighborPos] = newGScore;

            int fScore = newGScore + CalculateDistance(neighborPos, Target);
            openSet.PushOrDecrease(neighborPos, fScore);
        }

        private void ReconstructPath(Dictionary<int2, int2> cameFrom, int2 target)
        {
            var tempPath = new List<int2>();
            int2 current = target;

            while (!current.Equals(Start))
            {
                tempPath.Add(current);

                if (!cameFrom.TryGetValue(current, out int2 previous))
                {
#if JPS4_DEBUG
                    UnityEngine.Debug.LogError($"Path reconstruction failed at {current}");
#endif
                    return;
                }

                current = previous;
            }

            tempPath.Add(Start);

            for (int i = tempPath.Count - 1; i >= 0; i--)
            {
                PathResult.Add(tempPath[i]);
            }
        }

        private static int GetGScore(Dictionary<int2, int> gScore, int2 pos)
        {
            return gScore.TryGetValue(pos, out int score) ? score : MaxGScore;
        }

        private static bool TryGetCameFromPosition(Dictionary<int2, int2> cameFrom, int2 pos, out int2 previous)
        {
            return cameFrom.TryGetValue(pos, out previous);
        }

        private int IDX(int2 pos) => pos.y * GridSize.x + pos.x;

        private bool InBounds(int2 pos)
        {
            return pos.x >= 0 && pos.x < GridSize.x && pos.y >= 0 && pos.y < GridSize.y;
        }

        private bool IsTraversable(int2 pos)
        {
            return GridTraversable[IDX(pos)] && !DynamicObstacles[IDX(pos)];
        }

        private int CalculateDistance(int2 a, int2 b)
        {
            return math.abs(a.x - b.x) + math.abs(a.y - b.y);
        }
    }
}

//#define JPS4_DEBUG

using System.Collections.Generic;
using Unity.Mathematics;
using static JPS4.JPS4Utils;

namespace JPS4.Optimized
{
    public enum ECanonicalOrdering
    {
        HorizontalFirst = 0,
        VerticalFirst = 1
    }

    /// <summary>
    /// (FREE VERSION) Managed-heap, single-threaded implementation of JPS4
    /// (Jump Point Search, 4-connected grid).
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
    public class JPS4PathfindingAlgorithm
    {
        // Input data (read-only)
        public int2 GridSize;                     // Dimensions of the grid (width, height)
        public bool[] GridTraversable;            // True = walkable, False = obstacle
        public bool[] DynamicObstacles;           // True = blocked, False = free
        public byte[] DynamicBlockedEdges;          // Optional: edge blocking flags (4 bits per tile for 4 directions)
        public int2 Start;                        // Starting position
        public int2 Target;                       // Goal position
        public bool UseClosestTarget;             // If true & no path is found, reconstruct using the closest path to the target
        public ECanonicalOrdering CANONICAL_ORDERING = ECanonicalOrdering.HorizontalFirst;
        // Output data
        public List<int2> PathResult;             // Final path from start to target
        public List<JPSDebugStep> DebugSteps;

        /// <summary>
        /// Main execution method called by Unity's job system
        /// </summary>
        public void Execute()
        {
            if (!InBounds(Start) || !InBounds(Target)) return;
            if (PathResult == null) PathResult = new List<int2>();
            PathResult.Clear();
            
            // Handle trivial case where start equals target
            if (Start.Equals(Target))
            {
                PathResult.Add(Start);
                return;
            }
            
            // Run the actual JPS4 A* algorithm
            RunAStar();
        }

        /// <summary>
        /// Core A* pathfinding algorithm implementation
        /// </summary>
        private void RunAStar()
        {
            // Initialize data structures:
            // - openSet: priority queue of nodes to explore (sorted by f-score)
            // - gScore: cost from start to each node
            // - cameFrom: tracks the path by storing which node we came from
            var openSet = ManagedMinHeap<int2>.Create();
            var gScore = new Dictionary<int2, int>();
            var cameFrom = new Dictionary<int2, int2>();
            var parentDirs = new Dictionary<int2, byte>();
            var prunedDirs = new List<bool>(DIRS.Length);
            for (int i = 0; i < DIRS.Length; i++) prunedDirs.Add(false);
            
            Initialize(openSet, gScore, cameFrom, parentDirs);

            bool foundTarget = false;
            int2 bestPos = Start;
            bool hasBest = false;
            int bestH = MaxGScore;
            int bestG = MaxGScore;
            int nodeBudget = GridSize.x * GridSize.y;

            int numLoops = 0;
            
            // Main A* loop: explore nodes until we find the target or run out of options
            while (!openSet.IsEmpty)
            {
                // Get the node with the lowest f-score (most promising)
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

                // Success! We reached the target
                if (currentPos.Equals(Target))
                {
                    ReconstructPath(cameFrom, Target);
                    foundTarget = true;
                    break;
                }

                // Explore all neighbors of the current node
                ExploreNeighbors(currentPos, gScore, cameFrom, openSet, prunedDirs, parentDirs);
#if JPS4_DEBUG
                if (++numLoops > nodeBudget * 2)
                {
                    Debug.LogError(
                        $"JPS infinite loop\n" +
                        $"Start: {Start}\n" +
                        $"Target: {Target}\n" +
                        $"Current: {currentPos}"
                    );
                    break;
                }
            }
            if (numLoops > nodeBudget * 0.85f)
            {
                Debug.Log($"High loop count [{numLoops}] {Start} to {Target}. Path length {PathResult.Length}");
            }
#else
            }
#endif

            if (!foundTarget && UseClosestTarget && hasBest)
            {
                ReconstructPath(cameFrom, bestPos);
            }

        }

        private static readonly byte INVALID_BYTE = byte.MaxValue;

        /// <summary>
        /// Adds the starting node to the open set with cost 0
        /// </summary>
        private void Initialize(ManagedMinHeap<int2> openSet, Dictionary<int2, int> gScore, Dictionary<int2, int2> cameFrom, Dictionary<int2, byte> parentDirs)
        {
            gScore.Clear();
            cameFrom.Clear();
            parentDirs.Clear();
            
            // Initialize Start Node:
            gScore[Start] = 0;  // Cost to reach start from start is 0
            
            // f-score = g-score + distance
            // Since g-score is 0 at start, f-score equals the distance
            int distance = CalculateDistance(Start, Target);
            int fScore = distance;
            
            openSet.TryPush(Start, fScore);
        }

        int DirIndex(int2 dir)
        {
            if (dir.Equals(new int2(-1, 0))) return LEFT;
            else if (dir.Equals(new int2(0, -1))) return DOWN;
            else if (dir.Equals(new int2(1, 0))) return RIGHT;
            else return UP;
        }
        
        /// <summary>
        /// Examines all neighboring cells of the current positions
        /// </summary>
        private void ExploreNeighbors(int2 currentPos, Dictionary<int2, int> gScore, Dictionary<int2, int2> cameFrom, ManagedMinHeap<int2> openSet, List<bool> prunedDirs, Dictionary<int2, byte> parentDirs)
        {
            int2 prevPosition = new int2(0, 0);
            int neighborCount = DIRS.Length;
            for (int i = 0; i < neighborCount; ++i) prunedDirs[i] = false; // Reset pruned
            
            // JPS4: Prune based on presence of obstacles and current moving direction
            if (currentPos.Equals(Start))
            {
                // Prune nothing
            }
            else
            {
                //var sourcePosition = cameFrom[currentIndex]; // This is the open set, where the jump came from
                //var dir = currentPos - prevPosition;
                var dir = parentDirs.TryGetValue(currentPos, out byte parentDir) && parentDir != INVALID_BYTE
                    ? DIRS[parentDir]
                    : DIRS[RIGHT];
                prevPosition = -1 * dir + currentPos;
                
                // Prune prev direction (don't go backwards)
                prunedDirs[DirIndex(dir * -1)] = true;
                
#if JPS4_DEBUG
                int2 debugSourcePos = TryGetCameFromPosition(cameFrom, currentPos, out int2 sourcePos)
                    ? sourcePos
                    : currentPos;
                if(!math.clamp(currentPos - debugSourcePos, new int2(-1,-1),new int2(1,1)).Equals(dir)) Debug.Log("Source trace can't be trusted.");
                if(!parentDirs.TryGetValue(currentPos, out _) || parentDir == INVALID_BYTE) Debug.LogError("Missing cache value");
                if (prevPosition.Equals(new int2(-1, -1))) Debug.LogError($"Invalid previous position at {currentPos}");
                if(math.abs(dir.x) > 1 || math.abs(dir.y) > 1) Debug.LogError($"Invalid direction at {currentPos}");
#endif

                if (CANONICAL_ORDERING == ECanonicalOrdering.HorizontalFirst)
                {
                    // Moving horizontal
                    if (dir.x != 0)
                    { 
                        // Never prune horizontal nodes (with horizontal first canonical ordering)
                    }
                    // Moving vertical:
                    else
                    {
                        // Establish natural neighbors (without obstacles first)
                        // Where natural neighbors are those that remain after pruning (!prunedDirs)
                        // Prune left and right, keeping vertical
                        prunedDirs[LEFT]  = true;
                        prunedDirs[RIGHT] = true;

                        // Prune opposite direction (we're not going to reverse)
                        if (dir.y > 0) prunedDirs[DOWN] = true;
                        else prunedDirs[UP] = true;
                        
                        // Check for forced neighbors (these are only LEFT or RIGHT) 
                        // --> Vertical has a single natural neighbor that's only either up or down (forced cannot be natural)
                        // --> Inverse of the natural neighbor's direction is also excluded from forced (so no more vertical options remain)
                        // We can implicitly check blocked paths based on nodes left/right of the previous (AND the neighbor itself is traversable)
                        if (IsBlocked(prevPosition + new int2(-1, 0)) && IsMovementFree(currentPos, currentPos + DIRS[LEFT]))
                        {
                            prunedDirs[LEFT] = false;
                        }
                        if (IsBlocked(prevPosition + new int2(1, 0)) && IsMovementFree(currentPos, currentPos + DIRS[RIGHT]))
                        {
                            prunedDirs[RIGHT] = false;
                        }
                    }
                }
                else if(CANONICAL_ORDERING == ECanonicalOrdering.VerticalFirst)   
                {
                    // Moving vertical
                    if (dir.y != 0)
                    {
                        // Never prune vertical nodes (with vertical first canonical ordering)
                    }
                    // Moving horizontal:
                    else
                    {
                        // Establish natural neighbors (without obstacles first)
                        // Where natural neighbors are those that remain after pruning (!prunedDirs)
                        // Prune up and down, keeping horizontal
                        prunedDirs[UP]  = true;
                        prunedDirs[DOWN] = true;
                        
                        // Prune opposite direction (we're not going to reverse)
                        if (dir.x > 0) prunedDirs[LEFT] = true;
                        else prunedDirs[RIGHT] = true;
                        
                        // Check for forced neighbors (these are only UP or DOWN) 
                        // --> Horizontal has a single natural neighbor that's only either left or right (forced cannot be natural)
                        // --> Inverse of the natural neighbor's direction is also excluded from forced (so no more horizontal options remain)
                        // We can implicitly check blocked paths based on nodes up/down of the previous (AND the neighbor itself is traversable)
                        if (IsBlocked(prevPosition + new int2(0, -1)) && IsMovementFree(currentPos, currentPos + DIRS[DOWN]))
                        {
                            prunedDirs[DOWN] = false;
                        }
                        if (IsBlocked(prevPosition + new int2(0, 1)) && IsMovementFree(currentPos, currentPos + DIRS[UP]))
                        {
                            prunedDirs[UP] = false;
                        }
                    }
                    
                }
            }
            
#if  JPS4_DEBUG
            int2 source = TryGetCameFromPosition(cameFrom, currentPos, out int2 sourcePos2)
                ? sourcePos2
                : new int2(-1, -1);
            if (DebugSteps == null) DebugSteps = new List<JPSDebugStep>();
            var debug = new JPSDebugStep
            {
                current = currentPos,
                source = source,
                from = prevPosition,
                jumpLeft = new int2(-1,-1),
                jumpRight = new int2(-1,-1),
                jumpUp = new int2(-1,-1),
                jumpDown = new int2(-1,-1),
                rejected = new int2(-1,-1)
            };
            byte prunedMask = 0;
            for (int i = 0; i < 4; i++) if (prunedDirs[i]) prunedMask |= (byte)(1 << i);

            debug.prunedMask = prunedMask;
#endif
            
            // Check in each valid (not pruned) directions
            for (int i = 0; i < neighborCount; i++)
            {
                if (prunedDirs[i]) continue;
                
                // Try to jump in the specified direction
                if (TryJump(currentPos, DIRS[i], out int2 jumpPos))
                {
                    int tentativeGScore = GetGScore(gScore, currentPos) + StepCost*CalculateDistance(currentPos, jumpPos);

                    if (tentativeGScore < GetGScore(gScore, jumpPos))
                    {
                        parentDirs[jumpPos] = (byte)i;
                        UpdateNeighbor(jumpPos, tentativeGScore,
                            currentPos, gScore, cameFrom, openSet);
                    }
#if JPS4_DEBUG
                    else debug.rejected = jumpPos;
                    if (i == RIGHT) debug.jumpRight = jumpPos;
                    if (i == LEFT)  debug.jumpLeft  = jumpPos;
                    if (i == UP)    debug.jumpUp    = jumpPos;
                    if (i == DOWN)  debug.jumpDown  = jumpPos;
#endif
                }
            }
            
#if JPS4_DEBUG
            DebugSteps.Add(debug);
#endif
        }
        
        /// <summary>
        /// Move forward in the specified direction from the current position, checking jump point conditions.
        /// </summary>
        private bool TryJump(int2 start, int2 dir, out int2 result)
        {
            int2 pos = start;
            result = new int2(-1, -1);

            while (true)
            {
                var prev = pos;
                pos += dir; // Step in the specified direction
                
                if (!InBounds(pos) || !IsTraversable(pos) || IsEdgeBlocked(prev, pos)) return false;

                if (CANONICAL_ORDERING == ECanonicalOrdering.HorizontalFirst && dir.x != 0)
                {
                    // Horizontal movement is just a simple step as the jump point
                    result =  pos;
                    return true;
                }
                else if (CANONICAL_ORDERING == ECanonicalOrdering.VerticalFirst && dir.y != 0)
                {
                    // Vertical movement is just a simple step as the jump point
                    result =  pos;
                    return true;
                }

                if (HasForcedNeighbor(prev, pos, dir))
                {
                    result = pos;
                    return true;
                }
                
                if (pos.Equals(Target))
                {
                    result = pos;
                    return true;
                }
            }
        }

        private bool HasForcedNeighbor(int2 prevPosition, int2 currentPos, int2 dir)
        {
            if (CANONICAL_ORDERING == ECanonicalOrdering.HorizontalFirst)
            {
                // Moving horizontal:
                if (dir.x != 0) return false;
                // Moving vertical:
                else
                {
                    // Check for forced neighbors (these are only LEFT or RIGHT) 
                    if (IsBlocked(prevPosition + new int2(-1, 0)) && IsMovementFree(currentPos, currentPos + DIRS[LEFT]))
                    {
                        return true;
                    }
                    if (IsBlocked(prevPosition + new int2(1, 0)) && IsMovementFree(currentPos, currentPos + DIRS[RIGHT]))
                    {
                        return true;
                    }
                }
            }
            else if(CANONICAL_ORDERING == ECanonicalOrdering.VerticalFirst)   
            {
                // Moving vertical:
                if (dir.y != 0) return true;
                // Moving horizontal:
                else
                {
                    // Check for forced neighbors (these are only UP or DOWN) 
                    if (IsBlocked(prevPosition + new int2(0, -1)) && IsMovementFree(currentPos, currentPos + DIRS[DOWN]))
                    {
                        return true;
                    }

                    if (IsBlocked(prevPosition + new int2(0, 1)) && IsMovementFree(currentPos, currentPos + DIRS[UP]))
                    {
                        return true;
                    }
                }

            }
            return false;
        }
        
        
        /// <summary>
        /// Updates a neighbor with a better path and adds/updates it in the open set
        /// </summary>
        private void UpdateNeighbor(int2 neighborPos, int newGScore, int2 cameFromPos, Dictionary<int2, int> gScore, Dictionary<int2, int2> cameFrom, ManagedMinHeap<int2> openSet)
        {
            // Update cost and reference to previous position (from prev to this neighbor):
            cameFrom[neighborPos] = cameFromPos;
            gScore[neighborPos] = newGScore;
            
            // Calculate f-score = g-score + distance (estimated total cost)
            int distance = CalculateDistance(neighborPos, Target);
            int fScore = newGScore + distance;
            
            // Add to open set or update priority if already present
            openSet.PushOrDecrease(neighborPos, fScore);
        }

        /// <summary>
        /// Traces back from target to start using the cameFrom array to build the final path
        /// </summary>
        private void ReconstructPath(Dictionary<int2, int2> cameFrom, int2 target)
        {
            var tempPath = new List<int2>();
            int2 current = target;

            // Walk backwards from target to start
            while (!current.Equals(Start))
            {
                tempPath.Add(current);
                
                // Get the node we came from
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

            // Reverse the path so it goes from start to target
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

        // ============ GRID UTILITY METHODS ============
        
        private int IDX(int2 pos) => pos.y * GridSize.x + pos.x;

        private bool InBounds(int2 pos)
        {
            return pos.x >= 0 && pos.x < GridSize.x && pos.y >= 0 && pos.y < GridSize.y;
        }
        
        private bool IsTraversable(int2 pos)
        {
            return GridTraversable[IDX(pos)] && !DynamicObstacles[IDX(pos)]; // True if the position is not blocked
        }
        
        /// <summary>
        /// Used for checking a candidate position, BLOCKED means out of bounds or not traversable.
        /// </summary>
        private bool IsBlocked(int2 p)
        {
            return !InBounds(p) || !IsTraversable(p);
        }

        /// <summary>
        /// Check if an edge between two adjacent tiles is blocked.
        /// Checks if the destination cell has an edge flag that prevents entry from the given direction.
        /// </summary>
        private bool IsEdgeBlocked(int2 from, int2 to)
        {
            if (DynamicBlockedEdges == null) return false;
            if (!InBounds(to)) return true;

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

        /// <summary>
        /// Used for checking a candidate position, FREE means in bounds and traversable.
        /// </summary>
        private bool IsFree(int2 p)
        {
            return InBounds(p) && IsTraversable(p);
        }

        /// <summary>
        /// Check if movement from 'from' to 'to' is free (no obstacles or blocked edges).
        /// </summary>
        private bool IsMovementFree(int2 from, int2 to)
        {
            return IsFree(to) && !IsEdgeBlocked(from, to);
        }

        /// <summary>
        /// Distance heuristic that determines cost
        /// </summary>
        private int CalculateDistance(int2 a, int2 b)
        {
            // Manhattan distance:
            return math.abs(a.x - b.x) + math.abs(a.y - b.y);
        }
    }
}
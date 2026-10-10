using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Maze
{
    // Adapted from https://github.com/dvdmc/unity_maze_generator
    public static class MazeGenerator
    {
        // Static method to generate maze directly into a NativeArray
        public static void FillMazeGrid(NativeArray<bool> grid, int2 size, uint seed)
        {
            var rng = new Unity.Mathematics.Random(seed);
            
            // Size should be odd for proper maze generation (2n+1 format)
            int mazeWidth = (size.x - 1) / 2;
            int mazeHeight = (size.y - 1) / 2;
            
            if (mazeWidth <= 0 || mazeHeight <= 0)
            {
                // Fill with walls if size is too small
                for (int i = 0; i < grid.Length; i++)
                    grid[i] = false;
                return;
            }
            
            // Use NativeArray for cells (faster than managed array)
            int totalCells = mazeHeight * mazeWidth;
            var isUsed = new NativeArray<bool>(totalCells, Allocator.Temp);
            var connected = new NativeArray<byte>(totalCells, Allocator.Temp); // 4 bits for connections [N E S W]
            
            // Stack using NativeList (faster than managed Stack)
            var cellRecord = new NativeList<int2>(totalCells, Allocator.Temp);
            
            int2 position = new int2(0, 0);
            int usedTiles = 1;
            int posIdx = position.x * mazeWidth + position.y;
            isUsed[posIdx] = true;
            cellRecord.Add(position);
            
            // Reusable array for available directions (no allocations in loop)
            var availableDir = new NativeList<int>(4, Allocator.Temp);
            
            while (usedTiles < totalCells)
            {
                availableDir.Clear();
                int2 current = cellRecord[cellRecord.Length - 1];
                int X = current.x;
                int Y = current.y;
                
                // Check available neighbors inline (avoid function call overhead)
                if (X > 0 && !isUsed[(X - 1) * mazeWidth + Y])
                    availableDir.Add(0);
                if (Y < mazeWidth - 1 && !isUsed[X * mazeWidth + (Y + 1)])
                    availableDir.Add(1);
                if (X < mazeHeight - 1 && !isUsed[(X + 1) * mazeWidth + Y])
                    availableDir.Add(2);
                if (Y > 0 && !isUsed[X * mazeWidth + (Y - 1)])
                    availableDir.Add(3);
                
                if (availableDir.Length != 0)
                {
                    int nextDir = availableDir[rng.NextInt(0, availableDir.Length)];
                    int currentIdx = position.x * mazeWidth + position.y;
                    
                    // Update connections and position based on direction
                    switch (nextDir)
                    {
                        case 0: // North
                            connected[currentIdx] |= 1; // bit 0
                            position.x--;
                            connected[position.x * mazeWidth + position.y] |= 4; // bit 2
                            break;
                        case 1: // East
                            connected[currentIdx] |= 2; // bit 1
                            position.y++;
                            connected[position.x * mazeWidth + position.y] |= 8; // bit 3
                            break;
                        case 2: // South
                            connected[currentIdx] |= 4; // bit 2
                            position.x++;
                            connected[position.x * mazeWidth + position.y] |= 1; // bit 0
                            break;
                        case 3: // West
                            connected[currentIdx] |= 8; // bit 3
                            position.y--;
                            connected[position.x * mazeWidth + position.y] |= 2; // bit 1
                            break;
                    }
                    
                    int newIdx = position.x * mazeWidth + position.y;
                    isUsed[newIdx] = true;
                    cellRecord.Add(position);
                    usedTiles++;
                }
                else
                {
                    cellRecord.RemoveAt(cellRecord.Length - 1);
                    position = cellRecord[cellRecord.Length - 1];
                }
            }
            
            // Convert maze to grid - initialize all as walls
            for (int i = 0; i < grid.Length; i++)
                grid[i] = false;
            
            // Fill walkable spaces
            for (int i = 0; i < mazeHeight; i++)
            {
                for (int j = 0; j < mazeWidth; j++)
                {
                    int cellIdx = i * mazeWidth + j;
                    byte conn = connected[cellIdx];
                    
                    int gridX = j * 2 + 1;
                    int gridY = i * 2 + 1;
                    
                    // Cell center
                    grid[gridY * size.x + gridX] = true;
                    
                    // Connections
                    if ((conn & 1) != 0) // North
                        grid[(gridY - 1) * size.x + gridX] = true;
                    if ((conn & 2) != 0) // East
                        grid[gridY * size.x + (gridX + 1)] = true;
                    if ((conn & 4) != 0) // South
                        grid[(gridY + 1) * size.x + gridX] = true;
                    if ((conn & 8) != 0) // West
                        grid[gridY * size.x + (gridX - 1)] = true;
                }
            }
            
            // Cleanup
            isUsed.Dispose();
            connected.Dispose();
            cellRecord.Dispose();
            availableDir.Dispose();
        }
    }
}
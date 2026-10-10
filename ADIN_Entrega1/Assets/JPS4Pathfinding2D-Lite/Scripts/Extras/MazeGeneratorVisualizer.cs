using System.Collections.Generic;
using JPS4;
using JPS4.Editor;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Maze
{
    public class MazeGeneratorVisualizer : MonoBehaviour
    {
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private PathfindingGridBuilder gridBuilder;
        [SerializeField] private DemoPathingAgent demoPathingAgent;
        [SerializeField] private bool buildOnStart =false;
        
        public int2 gridSize = new int2(21, 21);
        public uint seed = 1;
        public float cellSize = 1f;
        
        private NativeArray<bool> mazeGrid;
        private List<GameObject> obstacles;

        [Button("Build")]
        void Build()
        {
            if (!Application.isPlaying) return;
            ResetGrid();
            mazeGrid = new NativeArray<bool>(gridSize.x * gridSize.y, Allocator.Persistent);
            seed = (uint)UnityEngine.Random.Range(0, 100000);
            MazeGenerator.FillMazeGrid(mazeGrid, gridSize, seed);
            
            Vector3 offset = transform.position - new Vector3(gridSize.x * cellSize * 0.5f, gridSize.y * cellSize * 0.5f, 0);
            
            // Spawn cells
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    bool isWalkable = mazeGrid[y * gridSize.x + x];
                    Vector3 pos = offset + new Vector3(x * cellSize, y * cellSize, 0);
                    if (!isWalkable)
                    {
                        var obj = GameObject.Instantiate(obstaclePrefab, transform);
                        obj.transform.position = pos;
                        obstacles.Add(obj);
                    }
                    
                }
            }

            if (gridBuilder) gridBuilder.TryBuildExternal(mazeGrid, gridSize);
            if(demoPathingAgent) demoPathingAgent.GeneratePath();
        }
        
        void Start()
        {
            if (!buildOnStart) return;
            Build();
        }

        [Button("Reset Grid")]
        void ResetGrid()
        {
            if (mazeGrid.IsCreated) mazeGrid.Dispose();
            var children = new List<GameObject>();
            foreach (Transform c in transform)
            {
                if (c == transform) continue;
                children.Add(c.gameObject);
            }
            foreach(var o in children) GameObject.DestroyImmediate(o.gameObject);
            obstacles ??= new();
            obstacles.Clear();
        }
        
        void OnDrawGizmos()
        {
            /*if (!mazeGrid.IsCreated) return;
            var size = cellSize;
            
            Vector3 offset = transform.position - new Vector3(gridSize.x * size * 0.5f, gridSize.y * size * 0.5f, 0);
            
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    bool isWalkable = mazeGrid[y * gridSize.x + x];
                    Gizmos.color = isWalkable ? Color.white : Color.black;
                    
                    Vector3 pos = offset + new Vector3(x * size, y * size, 0);
                    Gizmos.DrawCube(pos, Vector3.one * size * 0.9f);
                }
            }*/
        }
    }
}
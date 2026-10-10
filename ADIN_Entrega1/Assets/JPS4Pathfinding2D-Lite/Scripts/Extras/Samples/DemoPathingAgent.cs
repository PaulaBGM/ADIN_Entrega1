using System;
using System.Collections.Generic;
using System.Linq;
using JPS4.Editor;
using JPS4.JPS4.Debugging;
using UnityEngine;

namespace JPS4
{
    /// /// <summary>
    /// A simple, editor-safe pathfinding Agent that does synchronous pathing calls against a <see cref="PathfindingGridBuilder"/>.
    /// Includes test buttons and lazy-initializes grids that haven't been built for editor use
    /// </summary>
    public class DemoPathingAgent : PathingAgentBase
    {
        [Header("Configuration")]
        [SerializeField] private bool updateRefresh = false;
        [SerializeField] private bool autoBuildGrid = true;
        
        [Header("References")]
        [SerializeField, Tooltip("Grid reference that executes all pathfinding queries.")]
        private PathfindingGridBuilder grid;
        
        [SerializeField, Tooltip("Target position for the path query.")] 
        private Transform target;

        [Header("Movement")] 
        [SerializeField, Tooltip("Move automatically on Update.")]
        private bool autoMove = true;
        
        [Header("Debug")]
        [SerializeField, Tooltip("Show the current path query from this agent.")]
        private bool drawGizmos = true; 
        
        // Path vars
        public bool IsMoving => movementComponent && movementComponent.IsMoving;
        public Vector3 GridUp => grid ? grid.transform.forward : Vector3.forward;
        public Vector3 Target => curPath is { Count: > 0 } ? curPath[^1] : Vector3.zero;
        public List<Vector3> CurPath => curPath;
        private List<Vector3> curPath = null;
        
        // Update Refresh:
        private Vector3 _prevTargetPos;
        private Vector3 _prevSourcePos;
        private bool _explicitGenerate;

        private PathingMovementComponent movementComponent;
        void Awake()
        {
            movementComponent = GetComponent<PathingMovementComponent>();
        }

        void Start()
        {
            GeneratePath();
        }

        [Button("Generate Path")]
        public void GeneratePath()
        {
            if (!grid || !target)
            {
                Debug.LogError("Missing references.");
                return;
            }
            if(autoBuildGrid && !grid.IsBuilt) grid.Build();
            _explicitGenerate = true;

            var pathConfig = new PathConfig()
            {
                CollapseSkips = true,
                UseClosestTarget = false,
                QueryDynamic = false
            };

            var pathRequest = new PathRequest(transform.position, target.position, pathConfig, result =>
            {
                curPath = result.Path;
            });
            
            // Generate immediately:
            curPath = grid.GetPath(transform.position, target.position, pathConfig);
            
            // OR Start a coroutine:
            //StartCoroutine(grid.GetPathAsync(pathRequest));
            
            // OR Queue into builder:
            //grid.RequestPathQueued(pathRequest);
            
            // Debugging:
            if(TryGetComponent( out JPSPathDebugVisualizer debugViz))
            {
                debugViz.Refresh();
                debugViz.SetPath(this); 
            }

            Debug.Log($"Generated path with length {curPath.Count}");
        }

        /// <summary>
        /// Explicitly generate a path from a given pathConfig.
        /// </summary>
        /// <param name="pathConfig">Settings for how to query the builder.</param>
        public void GeneratePath(PathConfig pathConfig)
        {
            if (!grid || !target)
            {
                Debug.LogError("Missing references.");
                return;
            }
            if(autoBuildGrid && !grid.IsBuilt) grid.Build();
            _explicitGenerate = true;
            
            // To generate a path without blocking the game flow, create a request:
            var pathRequest = new PathRequest(transform.position, target.position, pathConfig, result =>
            {
                curPath = result.Path;
            });
            
            // Generate immediately:
            curPath = grid.GetPath(transform.position, target.position, pathConfig);
            
            // OR Start a coroutine:
            //StartCoroutine(grid.GetPathAsync(pathRequest));
            
            // OR Queue into builder:
            //grid.RequestPathQueued(pathRequest);
        }

        [Button("Move Along Path")]
        public void MoveAlongPath()
        {
            if (!Application.isPlaying) return;
            if (!movementComponent && !TryGetComponent(out movementComponent))
            {
                Debug.LogError("Missing movement component.");
                return;
            }

            movementComponent.MoveAlongPath(this);
        }
        
        void Update()
        {
            if (!updateRefresh) return;
            if (!target || !grid) return;

            bool didTargetMove = Vector3.Distance(target.position, _prevTargetPos) > 0.001f;
            bool didSourceMove = Vector3.Distance(transform.position, _prevSourcePos) > 0.001f;

            if (_explicitGenerate || didTargetMove || (didSourceMove && !IsMoving))
            {
                // Generate the path immediately
                if(!_explicitGenerate) GeneratePath();
            
                // If we're already moving, adjust the path by calling move again
                if (IsMoving || autoMove)
                {
                    MoveAlongPath(); // updates movement after finishing the current tile step
                }
                
                _prevTargetPos = target.position;
                _prevSourcePos = transform.position;
                _explicitGenerate = false;
            }
        }
        
        static readonly Vector3 DRAW_OFFSET = new Vector3(-0.05f, -0.05f, 0); // Small offset to avoid overlap w/ debugging visualization.
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (curPath == null || !grid || grid.IsDebuggingEnabled) return;
            Gizmos.color = Color.yellow;
            for (var i = 0; i < curPath.Count-1; i++)
            {
                Gizmos.DrawLine(curPath[i]- DRAW_OFFSET, curPath[i+1]- DRAW_OFFSET);
            }
        }
    }
}
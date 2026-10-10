using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JPS4.Editor;
using JPS4.JPS4.Debugging;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace JPS4
{
    /// /// <summary>
    /// Demo of dynamic obstacles and edges/walls in-between grid squares.
    /// For another dynamic obstacle example <seealso cref="SyncedWorldManager"/>
    ///
    /// This updates random squares with dynamic obstacles every <see cref="refreshRate"/> seconds.
    /// The path to the specified target is generated and visualized with gizmos.
    /// Dynamic doors are also included for example, turning on and off randomly with the loop here.
    /// </summary>
    public class DemoDynamicWorld : MonoBehaviour
    {
        [SerializeField, Tooltip("Grid reference to query all pathfinding requests.")]
        private PathfindingDynamicEdgeGridBuilder gridBuilder;

        [SerializeField, Tooltip("How long in between dynamic obstacles refreshes?")]
        private float refreshRate = 0.1f;

        [SerializeField, Tooltip("Moveable pathinsg character agent.")]
        private DemoPathingAgent agent;

        [SerializeField, Tooltip("An example door that will be turned on / off")] 
        private SampleDynamicEdgeSet doorObstacle;
        
        [SerializeField, Tooltip("An set of dynamic obstacles that will turn on / off")]
        private List<SampleDynamicObstacle> randomizedObstacles;
        
        void Start()
        {
            if (!gridBuilder)
            {
                Debug.LogError("Missing grid builder reference.");
                return;
            }
            gridBuilder.Build(); // Ensure static grid is built on start
        }
        
        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(ObstacleLoop());
        }
        
        private void OnDisable()
        {
            StopAllCoroutines();
        }
        
        private IEnumerator ObstacleLoop()
        {
            while (isActiveAndEnabled)
            {
                yield return UpdateObstacles();
            }
        }
        
        /// <summary>
        /// Every time this is called, randomize the blocked obstacle positions on the board.
        /// </summary>
        private IEnumerator UpdateObstacles()
        {
            if (!gridBuilder || !gridBuilder.IsBuilt) yield break;

            // Toggle door on/off:
            doorObstacle.SetBlocking(doorObstacle.BlockedEdges != EEdgeFlags.None ? EEdgeFlags.None : EEdgeFlags.Down, gridBuilder);

            // Randomly turn on/off obstacles:
            foreach (var obstacle in randomizedObstacles)
            {
                obstacle.SetBlocking(UnityEngine.Random.value > 0.5f, gridBuilder);
            }
            
            // Update all dynamic obstacles & edges
            gridBuilder.BuildDynamic();
            
            var pathConfig = new PathConfig()
            {
                CollapseSkips = true,
                UseClosestTarget = false,
                QueryDynamic = true,
            };
            agent.GeneratePath(pathConfig); // Update path

            // Wait for the step rate (movement should be <= this duration)
            yield return new WaitForSeconds(refreshRate);
        }
    }
}
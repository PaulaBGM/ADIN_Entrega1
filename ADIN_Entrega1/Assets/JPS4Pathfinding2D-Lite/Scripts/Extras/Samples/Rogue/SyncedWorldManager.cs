using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace JPS4
{
    /// <summary>
    /// Sample usage of the pathfinding package for a synced tile-based world.
    /// Handles a world update loop that executes all agents at the same time, recording intention & operating on a <see cref="PathfindingDynamicGridBuilder"/>.
    /// </summary>
    public class SyncedWorldManager : MonoBehaviour
    {
        [SerializeField, Tooltip("How long in between synced time steps")]
        private float stepRate = 0.1f;
        
        [SerializeField, Tooltip("Grid reference to query all pathfinding requests.")]
        private PathfindingDynamicGridBuilder gridBuilder;
        
        [SerializeField, Tooltip("Agents that chase down a target.")] 
        private List<SyncedFollowAgent> followAgents; // Note: first agents in the list get priority for claiming tiles
        
        [SerializeField, Tooltip("Agents that patrol between a set of targets.")] 
        private List<SyncedPatrolAgent> patrolAgents;
        
        private HashSet<int2> _claimedTiles = new(); // Refreshed each step, next tile set agents are trying to move to

        void Start()
        {
            if (!gridBuilder)
            {
                Debug.LogError("Missing grid builder reference.");
                return;
            }
            gridBuilder.Build(); // Ensure static grid is built on start
            
            // Set Dynamic obstacles:
            foreach (var agent in followAgents)
            {
                agent.Initialize(gridBuilder);
                gridBuilder.RegisterDynamicObstacle(agent);
            }
            foreach (var agent in patrolAgents)
            {
                gridBuilder.RegisterDynamicObstacle(agent);
            }
        }
        
        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(SyncedLoop());
        }
        
        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator SyncedLoop()
        {
            while (isActiveAndEnabled)
            {
                yield return DoStep();
            }
        }
        
        /// <summary>
        /// Perform a synced step for all agents, wait for the stepRate and all agent movement to stop.
        /// </summary>
        private IEnumerator DoStep()
        {
            if (!gridBuilder || !gridBuilder.IsBuilt) yield break;
            
            // Reset next steps "claimed" tiles:
            _claimedTiles.Clear();

            // Update all dynamic obstacles
            gridBuilder.BuildDynamic();

            // Search and pick between paths with and without dynamic obstacles for better target tracking:
            yield return FindStaticPaths();
            yield return FindDynamicPaths();

            // Begin all movements forward
            // UniTask.WhenAll is a fantastic and safer alternative here
            int remaining = followAgents.Count + patrolAgents.Count;
            foreach (var agent in followAgents)
            {
                StartCoroutine(DoAgentStep(agent, () => remaining--));
            }
            foreach (var agent in patrolAgents)
            {
                StartCoroutine(DoAgentStep(agent, () => remaining--));
            }

            // Wait for the step rate (movement should be <= this duration)
            yield return new WaitForSeconds(stepRate);
            
            // Wait for all agent movement to finish:
            yield return new WaitUntil(() => remaining == 0);
        }
        
        /// <summary>
        ///  Queue and wait for static paths to be generated for all agents.
        /// </summary>
        private IEnumerator FindStaticPaths()
        {
            // Re-queue all agent paths:
            // We could track and selectively refresh based on path obstruction, but the point of the library is to do this part fast!
            List<PathRequest> pathRequests = new();
            foreach (var agent in followAgents)
            {
                // Query paths without obstacles to get the shortest option:
                if(agent.TryCalculatePath(out PathRequest staticRequest, queryDynamic:false))
                {
                    pathRequests.Add(staticRequest);
                }
            }
            
            // Queue all valid requests:
            foreach (var request in pathRequests)
            {
                gridBuilder.RequestPathQueued(request);
            }
            
            // Wait for all path jobs to finish
            // We could use a semaphore WhenAll approach, but w/ only one scheduler this is safe
            yield return gridBuilder.WaitForAllJobs();
        }

        /// <summary>
        /// Queue dynamic paths for all agents with an invalid static path.
        /// This approach avoids agents getting stuck (static-only) without overcompensating & moving in the wrong direction (dynamic-only)
        /// Great for long coordiors or lots of blocking agents.
        /// </summary>
        private IEnumerator FindDynamicPaths()
        {
            // If the next node is blocked, queue a dynamic request to the first blocked along the static path:
            List<PathRequest> dynamicRequests = new();
            foreach (var agent in followAgents.Where(x=>!x.IsStaticPathValid()))
            {
                // Query paths without obstacles to get the shortest option:
                if(agent.TryRecalculateAlongPath(out PathRequest dynamicRequest))
                {
                    dynamicRequests.Add(dynamicRequest);
                }
            }

            // Queue all patrol agents with a simple dynamic path
            foreach (var agent in patrolAgents)
            {
                if (agent.TryCalculatePath(out PathRequest dynamicRequest, agent.GetCurrentTarget()))
                {
                    dynamicRequests.Add(dynamicRequest);
                }
            }
            
            // Queue all valid requests:
            foreach (var request in dynamicRequests)
            {
                gridBuilder.RequestPathQueued(request);
            }
            
            // Wait for all path jobs to finish
            if(dynamicRequests.Any()) yield return gridBuilder.WaitForAllJobs();
        }
        
        private IEnumerator DoAgentStep(SyncedAgent agent, Action onComplete)
        {
            // Try to claim a position this step, and move to it if successful
            if (agent.TryGetNextPosition(out var next))
            {
                var tile = gridBuilder.GetGridPosition(next);
                if (_claimedTiles.Add(tile))
                {
                    yield return agent.StepTowardsTarget();
                }
            }
            onComplete?.Invoke();
        }
    }
}
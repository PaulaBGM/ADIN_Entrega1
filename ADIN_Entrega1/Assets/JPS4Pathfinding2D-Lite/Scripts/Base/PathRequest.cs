using System;
using System.Collections.Generic;
using UnityEngine;

namespace JPS4
{
    /// <summary>
    /// Extra configuration options for generating a path.
    /// </summary>
    public struct PathConfig
    {
        public bool CollapseSkips;
        public bool QueryDynamic;
        public bool UseClosestTarget;
    }
    
    public struct PathResult
    {
        public enum EPathJobStatus
        {
            Success,
            Cancelled_Rebuild,  // Jobs are cancelled on grid rebuild
            Cancelled_Lifetime, // Jobs interrupted by grid destroyed or disabled
            Failed     
        }
        public EPathJobStatus Status;
        public List<Vector3> Path;
        public PathConfig Config;

        public PathResult(List<Vector3> path, EPathJobStatus status, PathConfig config)
        {
            Status = status;
            Path = path;
            Config = config;
        }
    }
    
    /// <summary>
    /// A Queued pathfinding request form start to target position in 3D.
    /// </summary>
    public class PathRequest
    {
        public Vector3 Start;
        public Vector3 Target;
        public Action<PathResult> OnComplete; // Resulting 3D path on completion.
        public PathConfig Config;
        
        public PathRequest(Vector3 start, Vector3 target, Action<PathResult> onComplete, bool collapseSkips=true, bool queryDynamic=true, bool useClosestTarget = true)
        {
            Start = start;
            Target = target;
            OnComplete = onComplete;
            Config = new()
            {
                CollapseSkips = collapseSkips,
                QueryDynamic = queryDynamic, 
                UseClosestTarget = useClosestTarget
            };
        }

        public PathRequest(Vector3 start, Vector3 target, PathConfig config, Action<PathResult> onComplete)
        {
            Start = start;
            Target = target;
            OnComplete = onComplete;
            Config = config;
        }
    }
}
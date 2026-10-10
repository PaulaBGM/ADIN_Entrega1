using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JPS4
{
    public class SyncedPatrolAgent : SyncedAgent
    {
        [SerializeField, Tooltip("List of targets to swap between.")]
        private List<Transform> patrolPoints = new List<Transform>();
        
        private int _currentPatrolIndex;
        
        public Vector3 GetCurrentTarget()
        {
            return _currentPatrolIndex < patrolPoints.Count ? patrolPoints[_currentPatrolIndex].position :transform.position;
        }

        protected override IEnumerator ExecuteMovement(Vector3 nextPos)
        {
            yield return base.ExecuteMovement(nextPos);
            if (CurPath != null && !CurPath.HasNext(out _))
            {
                // Switch target
                _currentPatrolIndex++;
                if (_currentPatrolIndex >= patrolPoints.Count) _currentPatrolIndex = 0;
            }
        }
    }
}
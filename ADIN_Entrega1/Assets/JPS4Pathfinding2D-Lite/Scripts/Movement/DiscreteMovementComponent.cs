using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JPS4.Editor;
using UnityEngine;
using UnityEngine.Serialization;

namespace JPS4
{
    /// <summary>
    /// Moves target in discrete steps, with animated movement and rotation steps.
    /// Thinking chess: pawn moves each cell one by one.
    ///
    /// This is an example of an example movement type for sample usage, you can extend or make your own from scratch.
    /// In practice, I recommend UniTask. Check out the <seealso cref="AnimationUtils"/> for more!
    /// </summary>
    public class DiscreteMovementComponent : PathingMovementComponent
    {
        // Dur = how long the move takes
        // Delay = delay between movements
        // Curve = controls the curve
        [SerializeField] private AnimationUtils.AnimDataDelay moveAnim;
        [SerializeField] private AnimationUtils.AnimDataDelay rotAnim;
        [SerializeField] private bool waitForRotation = false; // If true then wait a move duration to rotate when changing directions
        [SerializeField] private bool interruptImmediately = false; // If true then don't wait for the previous movement to finish on interrupt, animate the next move immediately 
        
        [FormerlySerializedAs("forwardAxis")] [Header("Rotation")] 
        [SerializeField, Tooltip("Should we rotate the agent when moving?")] private bool useRotation = true;
        [SerializeField, Tooltip("Local rotation offset for the object.")] private Vector3 offsetRotation = new Vector3(90,0,90);
        [SerializeField, Tooltip("Flip the reference up?")] private bool flipUp;
        
        [Header("Debug")]
        [SerializeField] private Color gizmoColor = Color.yellow;
        [SerializeField] private bool drawGizmos = false;
        
        [Header("Editor")]
        [SerializeField] private Vector3 editorMovingDir = Vector3.right;
        [SerializeField] private Vector3 editorGridUpAxis = Vector3.forward; // The axis of the grid orthogonal to the cells (Vector3.forward usually)

        private Vector3 _gridNormal = Vector3.forward;
        
        private Coroutine movementHandle;
        private Coroutine rotHandle;
        private Coroutine pathHandle;

        // Internal Tracking:
        private int _curPathIdx = 0;
        private List<Vector3> _curPath;
        private List<Vector3> _queuedPath = null; // Used to queue paths in between steps without hard interupt / cancellation

        public override void MoveAlongPath(DemoPathingAgent pawnAI)
        {
            // Validation:
            _gridNormal = !flipUp ? pawnAI.GridUp : -1f * pawnAI.GridUp;
            if (Mathf.Abs(_gridNormal.magnitude) < 0.001f)
            {
                Debug.LogError("Invalid axis for movement component."); // Should be unit vector (ex. (1,0,0))
                return;
            }
            base.MoveAlongPath(pawnAI);
            if (!pawnAI) return;
            if (pawnAI.CurPath == null)
            {
                Debug.LogError("Invalid path provided to movement component.");
            }
            var curPath = pawnAI.CurPath!.ToList(); // Copy to list for redundancy
            // Refresh current path:
            if (IsMoving)
            {
                // Queue a new path for the next step
                _queuedPath = curPath;
            }
            else if (curPath.Count > 0)
            {
                // New Movement request:
                if(pathHandle != null) StopCoroutine(pathHandle);
                pathHandle = StartCoroutine(MoveAlongPathAsync(curPath));
            }
        }
        
        /// <summary>
        /// Called after each step.
        /// </summary>
        /// <returns>Continue along the current path?</returns>
        private bool TryContinuePath()
        {
            // New Movement request:
            if (_queuedPath != null)
            {
                if(pathHandle != null) StopCoroutine(pathHandle); // Redundant since we return false
                pathHandle = StartCoroutine(MoveAlongPathAsync(_queuedPath));
                return false;
            }
            return true;
        }

        public override void Stop()
        {
            if(pathHandle != null) StopCoroutine(pathHandle); // Redundant since we return false
            base.Stop();
        }

        public override void Continue(DemoPathingAgent pawnAI)
        {
            MoveAlongPath(pawnAI);
            base.Continue(pawnAI);
        }

        /// <summary>
        /// Assuming we start at the first node of the path, move in discrete movements along the path.
        /// </summary>
        public IEnumerator MoveAlongPathAsync(List<Vector3> path)
        {
            _queuedPath = null; // Reset queued path on new request
            _curPath = path;
            
            // Enforce that the starting position is in parity with the path
            // If you want to continue a path from a current position, call ContinueAlongPath
            if (path.Count > 0) transform.position = path[0];
            
            for (int i = 0; i <= path.Count - 2; i++)
            {
                _curPathIdx = i;
                var next = path[i + 1];
                if(movementHandle != null) StopCoroutine(movementHandle);
                movementHandle = StartCoroutine(MoveToAsync(next));
                yield return movementHandle;
                if (!TryContinuePath()) yield break;
            }
        }
        
        public void MoveTo(Vector3 targetPos)
        {
            if(movementHandle != null) StopCoroutine(movementHandle);
            movementHandle = StartCoroutine(MoveToAsync(targetPos));
        }

        [Button("EditorTestAlignment")]
        void EditorTestAlignment()
        {
            var desiredWorldFwd = editorMovingDir.normalized;
            var desiredWorldUp = editorGridUpAxis.normalized;
            var correctedRotation = transform.parent ? transform.parent.rotation*Quaternion.Euler(offsetRotation) : Quaternion.Euler(offsetRotation);
            var desiredRotation = Quaternion.LookRotation(desiredWorldFwd, desiredWorldUp)*correctedRotation;
            transform.rotation = desiredRotation;
        }
        
        public override IEnumerator MoveToAsync(Vector3 target)
        {
            IsMoving = true;
            
            var cur = transform.position;
            var dir = target - cur;
            if (Vector3.Distance(cur, target) < 0.001f)
            {
                IsMoving = false;
                yield break; // Not enough distance
            }

            var desiredWorldFwd = dir.normalized;
            var desiredWorldUp = _gridNormal.normalized;

            if (useRotation)
            {
                var correctedRotation = transform.parent ? transform.parent.rotation*Quaternion.Euler(offsetRotation) : Quaternion.Euler(offsetRotation);
                var desiredRotation = Quaternion.LookRotation(desiredWorldFwd, desiredWorldUp)*correctedRotation;

                if(rotAnim.Dur <= 0.001f) transform.rotation = desiredRotation;
                else if(transform.rotation != desiredRotation) // Checks dot product close to 1
                {
                    if(rotHandle != null) StopCoroutine(rotHandle);
                    rotHandle = StartCoroutine(AnimationUtils.InterpAction(transform, transform.RotationLerper, transform.rotation, desiredRotation, rotAnim.Dur, rotAnim.Curve));
                    if (waitForRotation)
                    {
                        yield return rotHandle;
                        if (rotAnim.Delay > 0.0001f) yield return new WaitForSeconds(moveAnim.Delay);
                    }
                }
            }
            
            yield return AnimationUtils.InterpAction(transform, transform.PositionLerper, cur, target, moveAnim.Dur, moveAnim.Curve);
            if (moveAnim.Delay > 0.0001f) yield return new WaitForSeconds(moveAnim.Delay);

            IsMoving = false;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (_curPath == null) return;
            Gizmos.color =  gizmoColor;
            for (var i = _curPathIdx+1; i < _curPath.Count-1; i++)
            {
                Gizmos.DrawLine(_curPath[i], _curPath[i+1]);
            }            
        }
    }
}
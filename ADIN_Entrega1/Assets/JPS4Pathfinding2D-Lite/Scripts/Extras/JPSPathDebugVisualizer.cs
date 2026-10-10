using System.Collections.Generic;
using JPS4.Editor;
using Unity.Mathematics;
using UnityEngine;
using static JPS4.JPS4Utils;

namespace JPS4.JPS4.Debugging
{
    public class JPSPathDebugVisualizer : MonoBehaviour
    {
        [SerializeField] private PathfindingGridBuilder grid;

        [Header("Playback")]
        [Range(0, 500)]
        public int stepIndex;

        [Header("Toggles")]
        public bool drawCurrent = true;
        public bool drawCameFrom = true;
        public bool drawPrunedDirs = true;
        public bool drawJumpPoints = true;

        [Header("Colors")]
        public Color currentColor = Color.yellow;
        public Color cameFromColor = Color.cyan;
        public Color prunedColor = Color.red;
        public Color jumpColor = Color.green;
        
        private IReadOnlyList<JPSDebugStep> steps;

        [Button("Refresh Debug Data")]
        public void Refresh()
        {
            if (!grid || !grid.IsDebuggingEnabled) return;
            steps = grid.LastJPSDebugSteps;
            Debug.Log($"Steps = {steps.Count}");
        }

        private DemoPathingAgent path;
        public void SetPath(DemoPathingAgent pathIn)=>path=pathIn;

        [Button(">>")]
        void NextStep() => stepIndex++;
        [Button("<<")]
        void PrevStep() => stepIndex--;
        
        // Extra colors for backwards-compatibility:
        private readonly Color COLOR_ORANGE = new Color(1f, 0.6470588f, 0.0f, 1f);
        private readonly Color COLOR_YELLOW = new Color(1f, 0.92156863f, 0.015686275f, 1f);
        private readonly Color COLOR_SKY_BLUE = new Color(0.5294118f, 0.8078432f, 0.9215687f, 1f);

        private void OnDrawGizmos()
        {
            if (grid == null) return;
            if (steps == null || steps.Count == 0) return;

            stepIndex = Mathf.Clamp(stepIndex, 0, steps.Count - 1);
            var step = steps[stepIndex];

            if (drawCurrent) DrawNode(step.current, currentColor, 0.3f);

            if (drawCameFrom && step.from.x >= 0)
            {
                Gizmos.color = cameFromColor;
                Gizmos.DrawLine(
                    grid.GetWorldPosition(step.from),
                    grid.GetWorldPosition(step.current)
                );
            }
            
            if (drawPrunedDirs) DrawPruned(step);

            if (drawJumpPoints) DrawJumps(step);
            
            if (step.rejected.x > -1) DrawNode(step.rejected, COLOR_ORANGE, 0.4f, wire:true);
            
            // Path:
            var activeset = new HashSet<int2>();
            Gizmos.color = COLOR_YELLOW;
            for (var i = 1; i <= stepIndex; i++)
            {
                if (steps[i].jumpDown.x > -1) activeset.Add(steps[i].jumpDown);
                if (steps[i].jumpLeft.x > -1) activeset.Add(steps[i].jumpLeft);
                if (steps[i].jumpRight.x > -1) activeset.Add(steps[i].jumpRight);
                if (steps[i].jumpUp.x > -1) activeset.Add(steps[i].jumpUp);
                if (steps[i].rejected.x > -1) activeset.Remove(steps[i].rejected);
                activeset.Remove(steps[i].current);
                
                var from = grid.GetWorldPosition(steps[i].source);
                var to= grid.GetWorldPosition(steps[i].current);
                Gizmos.DrawLine(from-DRAW_OFFSET, to-DRAW_OFFSET);

                if (i == steps.Count-1 && path)
                { // Final line
                    from = grid.GetWorldPosition(steps[i].current);
                    to = path.Target;
                    Gizmos.DrawLine(from-DRAW_OFFSET, to-DRAW_OFFSET);
                }
            }

            foreach (var active in activeset)
            {
                DrawNode(active, COLOR_SKY_BLUE, 0.4f, wire:true);
            }
        }
        
        readonly Vector3 DRAW_OFFSET = new Vector3(-0.05f, -0.05f, 0);

        private void DrawNode(int2 p, Color c, float size, bool wire=false)
        {
            Gizmos.color = c;
            if(!wire) Gizmos.DrawCube(grid.GetWorldPosition(p), Vector3.one * size);
            else Gizmos.DrawWireCube(grid.GetWorldPosition(p), Vector3.one * size);
        }

        private void DrawPruned(JPSDebugStep step)
        {
            DrawDir(step, 0, Vector2.right);
            DrawDir(step, 1, Vector2.left);
            DrawDir(step, 2, Vector2.up);
            DrawDir(step, 3, Vector2.down);
        }

        private void DrawDir(JPSDebugStep step, int bit, Vector2 dir)
        {
            if ((step.prunedMask & (1 << bit)) == 0) return;

            Gizmos.color = prunedColor;
            Gizmos.DrawRay(
                grid.GetWorldPosition(step.current),
                (Vector3)(dir * 0.4f)
            );
        }

        private void DrawJumps(JPSDebugStep step)
        {
            DrawJump(step.jumpRight);
            DrawJump(step.jumpLeft);
            DrawJump(step.jumpUp);
            DrawJump(step.jumpDown);
        }

        private void DrawJump(int2 p)
        {
            if (p.x < 0) return;
            DrawNode(p, jumpColor, 0.25f);
        }
    }
}
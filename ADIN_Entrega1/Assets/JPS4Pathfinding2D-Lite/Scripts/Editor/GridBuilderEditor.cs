#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;
#endif

namespace JPS4.Editor
{
#if UNITY_EDITOR
    /// <summary>
    /// Custom inspector for <see cref="PathfindingGridBuilder"/> (and derived types such as
    /// <see cref="PathfindingDynamicGridBuilder"/>).
    ///
    /// Shows prominent in-editor warnings that Burst-compiled / parallel-job pathfinding is a
    /// JPS4+ Pro feature, and redirects users to the full asset on the Unity Asset Store.
    /// </summary>
    [CustomEditor(typeof(PathfindingGridBuilder), true)]
    [CanEditMultipleObjects]
    public class GridBuilderEditor : UnityEditor.Editor
    {
        private const string ASSET_STORE_URL =
            "https://assetstore.unity.com/packages/slug/jps4-2d-grid-pathfinding-355438";

        public override void OnInspectorGUI()
        {
            // ── FREE VERSION BANNER ──────────────────────────────────────────────
            // Displayed prominently at the very top of every JPS4 GridBuilder inspector.
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            EditorGUILayout.HelpBox(
                "    JPS4+ PRO VERSION Unlocks:\n" +
                "  - Fully optimized algorithms!\n"+
                "  - Burst-compiled [BurstCompile] IJob execution (up to 10-20x faster per query)\n" +
                "  - Fully off-thread: worker threads only, main thread never blocked\n" +
                "  - NativeArray/NativeList collections: zero GC pressure, no garbage spikes\n" +
                "  - True async via JobHandle: chain, batch & schedule hundreds of paths per frame\n" +
                "  - RequestPath() returns a live JobHandle you can yield or dependency-chain\n",
                MessageType.Error
            );
            GUI.backgroundColor = prevBg;

            EditorGUILayout.Space(4);

            // ── DRAW PROPERTIES WITH INLINE PRO NOTES ───────────────────────────
            // We iterate properties manually so that targeted warnings can be
            // injected immediately below the fields they relate to.
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                using (new EditorGUI.DisabledScope("m_Script" == prop.propertyPath))
                    EditorGUILayout.PropertyField(prop, true);

                // ── After the algorithm selector: note every algorithm runs single-threaded ──
                if (prop.name == "pathingAlgorithm")
                {
                    prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.55f, 0.2f);
                    EditorGUILayout.HelpBox(
                        "Fully functional pathfinding, but not multi-threaded or optimized." +
                        "PRO FEATURE: Extremely Optimized versions, Burst-compiled, multi-threaded execution\n",
                        MessageType.Warning
                    );
                    GUI.backgroundColor = prevBg;
                }

                // ── After the job concurrency slider: note it has no effect here ──
                if (prop.name == "maxConcurrentJobs")
                {
                    prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.55f, 0.2f);
                    EditorGUILayout.HelpBox(
                        "PRO FEATURE: Parallel job concurrency (no effect in free version)\n",
                        MessageType.Warning
                    );
                    GUI.backgroundColor = prevBg;
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(2);

            // ── BUTTON METHODS (replicate ButtonAttributeEditor behaviour) ───────
            var methods = target.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public   | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var btn = method.GetCustomAttribute<ButtonAttribute>();
                if (btn == null) continue;

                string label = string.IsNullOrEmpty(btn.ButtonLabel)
                    ? ObjectNames.NicifyVariableName(method.Name)
                    : btn.ButtonLabel;

                if (GUILayout.Button(label))
                {
                    method.Invoke(target, null);
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
#endif
}

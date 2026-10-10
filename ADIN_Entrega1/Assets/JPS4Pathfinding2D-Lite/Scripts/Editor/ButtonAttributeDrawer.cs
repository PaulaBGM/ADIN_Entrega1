#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;
#endif

namespace JPS4.Editor
{
// Editor/ButtonAttributeDrawer.cs
#if UNITY_EDITOR
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public class ButtonAttributeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();
        
            // Get all methods with ButtonAttribute
            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        
            foreach (var method in methods)
            {
                var buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
                if (buttonAttribute != null)
                {
                    // Use custom label or method name
                    string buttonLabel = string.IsNullOrEmpty(buttonAttribute.ButtonLabel) 
                        ? ObjectNames.NicifyVariableName(method.Name) 
                        : buttonAttribute.ButtonLabel;
                
                    if (GUILayout.Button(buttonLabel))
                    {
                        // Invoke the method
                        method.Invoke(target, null);
                        EditorUtility.SetDirty(target);
                    }
                }
            }
        }
    }
#endif
}
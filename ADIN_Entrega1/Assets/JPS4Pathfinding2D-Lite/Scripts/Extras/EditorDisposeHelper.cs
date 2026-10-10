using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JPS4
{
#if UNITY_EDITOR
    [CreateAssetMenu(fileName = "EditorDisposeHelper", menuName = "JPS4/EditorDisposeHelper", order = 1)]
    public class EditorDisposeHelper : ScriptableSingleton<EditorDisposeHelper>
    {
        // Called on first load and on after of each domain reload 
        private void OnEnable()
        {
        }

        // Called on Unity exit and before each domain reload  
        private void OnDisable()
        {
            // This will slow down domain reload a bit on large scenes, if you're not using "Build" in editor, comment this out
            var builders = GameObject.FindObjectsByType<GridBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in builders)
            {
                if(b) b.Dispose();
            }
            
        }
    }
#else
    public class EditorDisposeHelper : ScriptableObject
    {
    }
#endif
}


#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace JPS4.Editor
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ShowIfEnumAttribute))]
    public class ShowIfEnumDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ShowIfEnumAttribute showIf = attribute as ShowIfEnumAttribute;
            SerializedProperty enumProp = property.serializedObject.FindProperty(showIf.enumFieldName);

            if (enumProp != null && enumProp.enumValueIndex == (int)showIf.enumValue)
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ShowIfEnumAttribute showIf = attribute as ShowIfEnumAttribute;
            SerializedProperty enumProp = property.serializedObject.FindProperty(showIf.enumFieldName);

            if (enumProp != null && enumProp.enumValueIndex == (int)showIf.enumValue)
            {
                return EditorGUI.GetPropertyHeight(property, label);
            }

            return 0f;
        }
    }
#endif
}
// ButtonAttribute.cs
using System;
using UnityEngine;

namespace JPS4.Editor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ButtonAttribute : PropertyAttribute
    {
        public readonly string ButtonLabel;
    
        public ButtonAttribute()
        {
            ButtonLabel = null;
        }
    
        public ButtonAttribute(string buttonLabel)
        {
            ButtonLabel = buttonLabel;
        }
    }
}
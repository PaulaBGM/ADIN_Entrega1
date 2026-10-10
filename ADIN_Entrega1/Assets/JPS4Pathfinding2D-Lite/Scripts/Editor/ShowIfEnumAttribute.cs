using UnityEngine;
using System;

namespace JPS4.Editor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ShowIfEnumAttribute : PropertyAttribute
    {
        public string enumFieldName;
        public object enumValue;

        public ShowIfEnumAttribute(string enumFieldName, object enumValue)
        {
            this.enumFieldName = enumFieldName;
            this.enumValue = enumValue;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace RoslynCSharp
{
    internal static class SerializedPropertyUtil
    {
        // Methods
        public static IEnumerable<SerializedProperty> GetChildProperties(SerializedProperty parent)
        {
            int parentDepth = parent.depth;
            IEnumerator enumerator = parent.GetEnumerator();

            while (enumerator.MoveNext() == true)
            {
                // Check for property
                if ((enumerator.Current is SerializedProperty childProperty) == false)
                    continue;

                // Check depth
                if (childProperty.depth > parentDepth + 1)
                    continue;

                // Get property copy
                yield return childProperty.Copy();
            }
        }

        public static SerializedProperty GetPropertyField(SerializedObject obj, string propertyName)
        {
            return obj.FindProperty(string.Format("<{0}>k__BackingField", propertyName));
        }

        public static SerializedProperty GetNestedPropertyField(SerializedObject obj, string propertyName, string nestedPropertyName)
        {
            return obj.FindProperty(string.Format("<{0}>k__BackingField", propertyName))
                ?.FindPropertyRelative(nestedPropertyName);
        }

        public static SerializedProperty GetNestedPropertyProperty(SerializedObject obj, string propertyName, string nestedPropertyName)
        {
            return obj.FindProperty(string.Format("<{0}>k__BackingField", propertyName))
                ?.FindPropertyRelative(string.Format("<{0}>k__BackingField", nestedPropertyName));
        }
    }
}

using System;
using UnityEditor;
using UnityEngine;

namespace RoslynCSharp
{
    public sealed class AssemblyReferenceAssetDrawer
    {
        // Private
        private readonly SerializedObject reference;
        private SerializedProperty assemblyNameProperty = null;
        private SerializedProperty assemblyPathProperty = null;
        private SerializedProperty assemblyWriteTimeProperty = null;

        // Constructor
        public AssemblyReferenceAssetDrawer(SerializedObject reference)
        {
            // Check for null
            if(reference == null)
                throw new ArgumentNullException(nameof(reference));

            // Check type
            if (reference.targetObject.GetType() != typeof(AssemblyReferenceAsset))
                throw new ArgumentException("Serialized object must be for type: AssemblyReferenceAsset");

            this.reference = reference;
        }

        // Methods
        public bool DisplayAssemblyName()
        {
            // Check for null
            if (assemblyNameProperty == null)
                assemblyNameProperty = SerializedPropertyUtil.GetNestedPropertyProperty(reference, nameof(AssemblyReferenceAsset.ReferenceAssembly), nameof(AssemblyReferenceSource.AssemblyName));

            // Display property
            return EditorGUILayout.PropertyField(assemblyNameProperty);
        }

        public bool DisplayAssemblyPath()
        {
            // Check for null
            if(assemblyPathProperty == null)
                assemblyPathProperty = SerializedPropertyUtil.GetNestedPropertyProperty(reference, nameof(AssemblyReferenceAsset.ReferenceAssembly), nameof(AssemblyReferenceSource.AssemblyPath));

            // Display property
            return EditorGUILayout.PropertyField(assemblyPathProperty);
        }

        public bool DisplayAssemblyLastWriteTime()
        {
            // Check for null
            if (assemblyWriteTimeProperty == null)
                assemblyWriteTimeProperty = SerializedPropertyUtil.GetNestedPropertyProperty(reference, nameof(AssemblyReferenceAsset.ReferenceAssembly), nameof(AssemblyReferenceSource.LastWriteTimeUTC));

            // Display property
            DateTime writeTime = DateTime.FromFileTimeUtc(assemblyWriteTimeProperty.longValue);

            // Display as local time for the user
            DateTime localWriteTime = writeTime.ToLocalTime();

            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.PrefixLabel("Last Write Time");
                GUILayout.Space(2);

                // Display test field
                EditorGUILayout.TextField(localWriteTime.ToString());
            }
            GUILayout.EndHorizontal();
            return false;
        }
    }
}

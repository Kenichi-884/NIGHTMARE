using Microsoft.CodeAnalysis;
using System;
using System.IO;
using UnityEngine;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a self contains assembly reference generated from a specific assembly file, but serialized as an asset so that the reference can be used on all platforms.
    /// </summary>
    [CreateAssetMenu(menuName = "Roslyn C# 2.0/Assembly Reference Asset")]
    public sealed class AssemblyReferenceAsset : ScriptableObject, ICompilationReference
#if UNITY_EDITOR
        , ISerializationCallbackReceiver
#endif
    {
#if UNITY_EDITOR
        // Editor only - used to update changes to settings after deserialize is completed
        [NonSerialized]
        private bool serializeChanges = false;
#endif

        // Properties
        /// <summary>
        /// The reference assembly that this reference asset was created from.
        /// Used to auto update the assembly reference when the assembly image is changed on disk.
        /// </summary>
        [field: SerializeField]
        public AssemblyReferenceSource ReferenceAssembly { get; private set; } = new();
        /// <summary>
        /// The raw portable executable image of the compiled assembly, used for referencing purposes.
        /// </summary>
        [field: SerializeField]
        [field: HideInInspector]    // Important - array can be many kb-s and can be very slow to draw inspector in debug mode.
        public byte[] AssemblyImage { get; private set; }
        /// <summary>
        /// Return a value indicating whether that reference asset contains a valid assembly image.
        /// </summary>
        public bool HasValidImage => AssemblyImage != null && AssemblyImage.Length > 0;
        /// <summary>
        /// Get the metadata reference from the assembly image.
        /// </summary>
        public MetadataReference CompilationReference => MetadataReference.CreateFromImage(AssemblyImage);

        // Methods
        /// <summary>
        /// Force a refresh of the assembly reference image from disk if available.
        /// This requires the assembly reference path to be valid and available on the current system.
        /// </summary>
        [ContextMenu("Refresh Assembly Reference")]
        public void RefreshAssemblyReference()
        {
            // Perform a refresh of the assembly from disk if possible
            ReferenceAssembly.RefreshAssemblyReference();
        }

#if UNITY_EDITOR
#if ROSLYNCSHARP_DEV
        /// <summary>
        /// Save the embedded assembly image to disk.
        /// Useful developer tool to check the assembly version that is currently saved.
        /// </summary>
        [ContextMenu("Save Assembly Image")]
        public void SaveAssemblyImage()
        {
            // Get save path
            string savePath = EditorUtility.SaveFilePanel("Save Assembly", "", "", "dll");

            // Save image bytes
            if (string.IsNullOrEmpty(savePath) == false)
                File.WriteAllBytes(savePath, AssemblyImage);
        }
#endif

#if UNITY_EDITOR
        private void OnEnable()
        {
#if UNITY_EDITOR
            if (serializeChanges == true)
            {
                serializeChanges = false;
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public void UpdateAssemblyReference(string assemblyPath, string assemblyName)
        {
            ReferenceAssembly.UpdateAssemblyReference(assemblyPath, assemblyName);
            ReloadAssemblyImage();
        }

        public void UpdateAssemblyReference(Assembly assembly)
        {
            ReferenceAssembly.UpdateAssemblyReference(assembly);
            ReloadAssemblyImage();
        }

        private void ReloadAssemblyImage()
        {
            try
            {
                // Update assembly image to latest version
                this.AssemblyImage = File.ReadAllBytes(ReferenceAssembly.AssemblyFullPath);

                // Mark as dirty to serialize changes
                Debug.Log("Auto updated reference asset for assembly: " + ReferenceAssembly.AssemblyName);
                serializeChanges = true;
            }
            catch (Exception e)
            {
                Debug.LogError("Could not auto update reference asset for assembly: " + e);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // Not used
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Update reference
            if(ReferenceAssembly != null && ReferenceAssembly.AutoRefreshAssemblyReference() == true)
            {
                // Try to read image from disk
                ReloadAssemblyImage();
            }
        }
#endif
#endif
    }
}

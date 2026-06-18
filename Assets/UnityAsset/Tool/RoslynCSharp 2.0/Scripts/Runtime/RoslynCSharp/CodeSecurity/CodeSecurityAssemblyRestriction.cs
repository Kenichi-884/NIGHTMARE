using System;
using System.Reflection;
using Trivial.CodeSecurity.Restrictions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoslynCSharp.CodeSecurity
{
    [Serializable]
    public sealed class CodeSecurityAssemblyRestriction : AssemblyRestriction
    {
        // Properties
        /// <summary>
        /// The optional assembly reference asset used to auto update
        /// </summary>
        [field: SerializeField]
        public AssemblyReferenceAsset ReferenceAssemblyAsset { get; internal set; }
        /// <summary>
        /// Get the last time the assembly was compiled in UTC file time.
        /// </summary>
        [field: SerializeField]
        public long LastWriteTimeUTC { get; private set; }

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        public CodeSecurityAssemblyRestriction() { }

        /// <summary>
        /// Create a new instance from the assembly.
        /// </summary>
        /// <param name="assembly">The assembly to create from</param>
        /// <param name="allowed">Are the members allowed</param>
        public CodeSecurityAssemblyRestriction(Assembly assembly, RestrictionAllow allowed = RestrictionAllow.Allow)
        {
            InitializeFromAssembly(assembly, allowed);

#if UNITY_EDITOR
            try
            {
                if (Application.isPlaying == false)
                {
                    // Get the assembly name
                    string assemblyName = assembly.GetName().Name;

                    // Find all reference assets
                    string[] guids = AssetDatabase.FindAssets($"t:{typeof(AssemblyReferenceAsset).FullName}");

                    // Update reference asset
                    foreach (string guid in guids)
                    {
                        // Get the path
                        string path = AssetDatabase.GUIDToAssetPath(guid);

                        // Load the asset
                        AssemblyReferenceAsset asset = string.IsNullOrEmpty(path) == false
                            ? AssetDatabase.LoadAssetAtPath<AssemblyReferenceAsset>(path)
                            : null;

                        // Check for null
                        if (asset == null)
                            continue;

                        // Check for assembly name
                        if (asset.ReferenceAssembly.AssemblyName == assemblyName)
                        {
                            this.ReferenceAssemblyAsset = asset;
                            break;
                        }
                    }
                }
            }
            catch(UnityException)
            {
                // Must be on background thread which is not allowed - simply do nothing.
            }
#endif
        }

        // Methods
        internal bool UpdateRestrictionChangesFromAssemblyReferenceAsset()
        {
            // Check write time
            if (ReferenceAssemblyAsset != null && ReferenceAssemblyAsset.ReferenceAssembly.LastWriteTimeUTC > LastWriteTimeUTC)
            {
                // Update timestamp
                this.LastWriteTimeUTC = ReferenceAssemblyAsset.ReferenceAssembly.LastWriteTimeUTC;

                // The assembly has changed on disk - auto apply changes
                try
                {
                    // Try to load
                    Assembly reference = Assembly.LoadFrom(ReferenceAssemblyAsset.ReferenceAssembly.AssemblyFullPath);

                    // Apply difference from last time the assembly was loaded (Members may have been added or removed)
                    UpdateDifferenceFromAssembly(reference);

                    Debug.Log("Auto updated code security restrictions for assembly: " + ReferenceAssemblyAsset.ReferenceAssembly.AssemblyName);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError("Could not auto update code security restrictions for assembly: " + e);
                }
            }
            return false;
        }

//#if UNITY_EDITOR
//        void ISerializationCallbackReceiver.OnBeforeSerialize()
//        {
//            // Not used
//        }

//        void ISerializationCallbackReceiver.OnAfterDeserialize()
//        {
//            // Check write time
//            if(ReferenceAssemblyAsset != null && ReferenceAssemblyAsset.ReferenceAssembly.LastWriteTimeUTC > LastWriteTimeUTC)
//            {
//                // Update timestamp
//                this.LastWriteTimeUTC = ReferenceAssemblyAsset.ReferenceAssembly.LastWriteTimeUTC;

//                // The assembly has changed on disk - auto apply changes
//                try
//                {
//                    // Try to load
//                    Assembly reference = Assembly.LoadFrom(ReferenceAssemblyAsset.ReferenceAssembly.AssemblyFullPath);

//                    // Apply difference from last time the assembly was loaded (Members may have been added or removed)
//                    UpdateDifferenceFromAssembly(reference);

//                    // Mark as dirty??
//                    EditorUtility.SetDirty(this);
//                    Debug.Log("Auto updated code security restrictions for assembly: " + ReferenceAssemblyAsset.ReferenceAssembly.AssemblyName);
//                }
//                catch (Exception e)
//                {
//                    Debug.LogError("Could not auto update code security restrictions for assembly: " + e);
//                }
//            }
//        }
//#endif
    }
}

using RoslynCSharp.CodeSecurity;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("RoslynCSharp.Editor")]

namespace RoslynCSharp
{
    [Serializable]
    public sealed class ConditionalAssemblyReference
    {
        public AssemblyReferenceAsset Reference;
        public string DefineCondition;        
    }

    /// <summary>
    /// The security mode used to prevent compiled code from crashing the game by hogging the main thread.
    /// </summary>
    [Flags]
    public enum ExecutionSecurityMode : uint
    {
        /// <summary>
        /// No execution security is injected.
        /// Recommended only if absolute performance is required for tight loops.
        /// </summary>
        None,
        /// <summary>
        /// Ensure that user code will time out if a long or infinite execution cycle is entered.
        /// The timeout time can be set via execution settings.
        /// </summary>
        ExecutionTimeout = 1 << 0,
        /// <summary>
        /// Ensure that user code will return after a set number of iterations to avoid infinite or very long execution cycles.
        /// </summary>
        ExecutionIterations = 1 << 1,
    }

    /// <summary>
    /// The execution settings used to ensure that compiled code cannot cause the host app to crash by blocking the main thread for a long or infinite time.
    /// Note that execution security can only be applied to assemblies that were compiled by Roslyn C#, as safety checks need to be injected after the parsing phase.
    /// </summary>
    [Serializable]
    public sealed class ExecutionSecuritySettings
    {
        // Properties
        /// <summary>
        /// The security mode used to process compiled code.
        /// </summary>
        [field: SerializeField]
        [field: Tooltip("Determined which execution security features are injected into compiled code. Use None if no execution security is required")]
        public ExecutionSecurityMode SecurityMode { get; set; } = ExecutionSecurityMode.ExecutionTimeout | ExecutionSecurityMode.ExecutionIterations;
        /// <summary>
        /// The amount of time in seconds before execution is forcefully terminated in user code.
        /// </summary>
        [field: SerializeField]
        [field: Tooltip("The amount of time in seconds before compiled code loops are forcefully aborted to prevent crashing or hanging the game for an excessive amount of time")]
        public float TimeoutSeconds { get; set; } = 2f;
        /// <summary>
        /// The maximum number of iterations of a looping execution cycle before execution is forcefully terminated in user code.
        /// </summary>
        [field: SerializeField]
        [field: Tooltip("The maximum number of iterations a compiled code loop is allowed to run in one operation before being forcefully aborted to prevent crashing or hanging the game for an excessive amount of time")]
        public int TimeoutIterations { get; set; } = 5000;

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="securityMode">The execution security flags to use</param>
        /// <param name="timeoutSeconds">The optional time in seconds for loop timeout"/></param>
        /// <param name="maxIterations">The optional max iterations for loops</param>
        public ExecutionSecuritySettings(ExecutionSecurityMode securityMode = ExecutionSecurityMode.ExecutionTimeout | ExecutionSecurityMode.ExecutionIterations, float timeoutSeconds = 2f, int maxIterations = 5000)
        {
            this.SecurityMode = securityMode;
            this.TimeoutSeconds = timeoutSeconds;
            this.TimeoutIterations = maxIterations;
        }

        internal ExecutionSecuritySettings(ExecutionSecuritySettings other)
        {
            this.SecurityMode = other.SecurityMode;
            this.TimeoutSeconds = other.TimeoutSeconds;
            this.TimeoutIterations = other.TimeoutIterations;
        }
    }

    /// <summary>
    /// Represents the main settings object for Roslyn C#.
    /// Used to store default and user settings in the project.
    /// </summary>
#if ROSLYNCSHARP_DEV
    [CreateAssetMenu]
#endif
    [Serializable]
    public sealed class RoslynCSharpSettings : ScriptableObject
#if UNITY_EDITOR
        , ISerializationCallbackReceiver
#endif
    {
        // Private
        private const string defaultSettingsName = "Editor/RoslynCSharpDefaultSettings";
        private const string userSettingsName = "RoslynCSharpUserSettings";

        private static RoslynCSharpSettings defaultSettings = null;
        private static RoslynCSharpSettings userSettings = null;

#if UNITY_EDITOR
        // Editor only - used to update changes to settings after deserialize is completed
        [NonSerialized]
        private bool serializeChanges = false;
#endif

        // Internal
        [SerializeField]
        internal List<string> defineSymbols = new();
        [SerializeField]
        internal List<AssemblyReferenceAsset> assemblyReferences = new();
        [SerializeField]
        internal List<ConditionalAssemblyReference> conditionalAssemblyReferences = new();

        // Properties
        /// <summary>
        /// The log level used to determine which messages should be logged to the Unity console.
        /// </summary>
        [field: SerializeField]
        public LogLevel LogLevel { get; set; } = LogLevel.Warnings;
        /// <summary>
        /// The compiler options that are used for compilation requests unless custom options are provided.
        /// </summary>
        [field: SerializeField]
        public CompileFlags CompilerFlags { get; set; } = new();
        /// <summary>
        /// Should loaded code be statically evaluated for potentially harmful code.
        /// </summary>
        [field: SerializeField]
        public bool SecurityCheckCode { get; set; } = true;
        /// <summary>
        /// Should assemblies that have been loaded or compiled which have already passed security verification be automatically whitelisted in the security settings so that other compile requests can easily reference the public API.
        /// With this option disabled, 
        /// </summary>
        [field: SerializeField]
        public bool WhitelistCompiledAndLoadedAssemblies { get; set; } = true;
        /// <summary>
        /// The code security restrictions used for static security verification of compiled assemblies before loading.
        /// </summary>
        [field: SerializeField]
        public CodeSecurityRestrictionValidator CodeSecurityRestrictions { get; set; } = new();
        /// <summary>
        /// The execution security settings used for compiled code.
        /// </summary>
        [field: SerializeField]
        public ExecutionSecuritySettings ExecutionSecuritySettings { get; set; } = new();
        /// <summary>
        /// The preprocessor define symbols that will be used for compilation unless custom options are provided.
        /// </summary>
        public IList<string> DefineSymbols => defineSymbols;
        /// <summary>
        /// The default assembly references that will be used for compilation unless custom options are provided.
        /// </summary>
        public IList<AssemblyReferenceAsset> AssemblyReferences => assemblyReferences;
        /// <summary>
        /// The conditional assembly references that will be used only when the specified define condition is met.
        /// </summary>
        public IList<ConditionalAssemblyReference> ConditionalAssemblyReferences => conditionalAssemblyReferences;

        /// <summary>
        /// Get the user settings for the project.
        /// </summary>
        public static RoslynCSharpSettings UserSettings
        {
            get
            {
                // Load settings
                if (userSettings == null)
                {
                    RoslynCSharpSettings userSettingsAsset = LoadSettingsResources(userSettingsName, Application.isPlaying == true);
                    
                    // Create instance so we don't modify the original
                    if(userSettingsAsset != null)
                        userSettings = Instantiate(userSettingsAsset);
                }

                return userSettings;
            }
        }

        /// <summary>
        /// Get the default settings for the project.
        /// Should not be modified in most cases, because changes will be overwritten when updating the asset.
        /// </summary>
        public static RoslynCSharpSettings DefaultSettings
        {
            get
            {
                // Load settings
                if (defaultSettings == null)
                    defaultSettings = LoadSettingsResources(defaultSettingsName, Application.isPlaying == true);

                return defaultSettings;
            }
        }

        // Methods
        private void OnEnable()
        {
            // Check for changes
#if UNITY_EDITOR
            if(serializeChanges == true)
            {
                serializeChanges = false;
                EditorUtility.SetDirty(this);
            }
#endif

            if(Application.isPlaying == true && userSettings != null)
                Debug.LogLevel = userSettings.LogLevel;
        }

#if ROSLYNCSHARP_DEV
        [ContextMenu("Reset Code Security Restrictions")]
#endif
        private void ResetCodeSecurityRestrictions()
        {
            CodeSecurityRestrictions = CodeSecurityRestrictionValidator.CreatedDefaultRestrictions();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Sort Code Security Restrictions")]
        private void SortCodeSecurityRestrictions()
        {
            CodeSecurityRestrictions.Sort();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // Methods
#if UNITY_EDITOR
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // Not used
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Update restrictions and mark as dirty if required
            if (CodeSecurityRestrictions.UpdateRestrictionChangesFromAssemblyReferenceAsset() == true)
                serializeChanges = true;
        }
#endif

        private static RoslynCSharpSettings LoadSettingsResources(string resourcesName, bool logOnError)
        {
            // Try to load settings
            RoslynCSharpSettings settings = Resources.Load<RoslynCSharpSettings>(resourcesName);

            // Check for error
            if(settings == null)
            {
                // Create default
                settings = CreateInstance<RoslynCSharpSettings>();

                // Report error
                if (logOnError == true)
                    Debug.LogWarningFormat("Failed to load settings asset '{0}'. Using defaults!", resourcesName);
            }
            return settings;
        }

#if UNITY_EDITOR
        public static RoslynCSharpSettings CreateOrLoadRoslynUserSettings()
        {
            // Check for already loaded
            RoslynCSharpSettings settings = Resources.Load<RoslynCSharpSettings>(userSettingsName);

            // Check for found
            if (settings != null)
                return settings;

            // Need to create the asset - try to load defaults
            RoslynCSharpSettings defaults = Resources.Load<RoslynCSharpSettings>(defaultSettingsName);

            // Check for no defaults
            if (defaults == null)
            {
                return null;
                //UnityEngine.Debug.LogWarning("Could not find Roslyn C# default settings, user settings will be created with default values!");
                //settings = CreateInstance<RoslynCSharpSettings>();
            }
            else
            {
                // Create a clone
                settings = Instantiate(defaults);
            }

            // This is the guid of the root `Roslyn C# 2.0` folder
            string rootFolder = AssetDatabase.GUIDToAssetPath("4aeed7adcaad2464c9651d0eed231ebb");

            // Create asset path
            string assetPath = rootFolder + "/Resources/" + userSettingsName + ".asset";

            // Save user settings asset
            AssetDatabase.CreateAsset(settings, assetPath);

            // Log to user
            UnityEngine.Debug.Log("Roslyn C# - Created user settings: " + assetPath);
            return settings;
        }
#endif
    }
}

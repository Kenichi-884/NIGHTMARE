using Microsoft.CodeAnalysis;
using RoslynCSharp.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Trivial.CodeSecurity;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a <see cref="Assembly"/> that has been loaded into a Roslyn C# <see cref="ScriptDomain"/>.
    /// </summary>
    public abstract class ScriptAssembly : ICompilationReference
    {
        // Private
        private static readonly List<ScriptType> matchedTypes = new List<ScriptType>();

        private readonly ScriptDomain domain;
        private readonly AssemblySource source;
        private readonly CompileResult compileResult;
        private readonly CodeSecurityReport securityReport;
        private readonly IMainTypeSelector mainTypeSelector;

        private ScriptType cachedMainType = null;
        private string[] cachedEmbeddedResourceNames = null;

        // Public
        /// <summary>
        /// The default type selector used for identifying the main type defined with an assembly.
        /// The default behaviour is to search for public MonoBehaviour types, then public classes, then public any type, and finally fall back to first defined type.
        /// Note that the special `Module` type is not considered as a possible MainType.
        /// </summary>
        public static readonly IMainTypeSelector DefaultMainTypeSelector = new DefaultMainTypeSelector();

        // Properties
        /// <summary>
        /// Get the <see cref="ScriptDomain"/> that this assembly was loaded into.
        /// </summary>
        public virtual ScriptDomain Domain => domain;
        /// <summary>
        /// Get the <see cref="Assembly"/> that is represented by this assembly.
        /// </summary>
        public abstract Assembly SystemAssembly { get; }
        /// <summary>
        /// Get all types defined within the assembly.
        /// Includes all public and non-public types.
        /// </summary>
        public abstract ScriptType[] Types { get; }
        /// <summary>
        /// Get the <see cref="AssemblySource"/> where this assembly was loaded from.
        /// </summary>
        public virtual AssemblySource Source => source;
        /// <summary>
        /// Get the <see cref="CodeSecurityReport"/> for this assembly if code security verification occurred during loading.
        /// </summary>
        public virtual CodeSecurityReport SecurityReport => securityReport;
        /// <summary>
        /// Return a value indicating whether this assembly has passed code security verification.
        /// This value will be true if code security verification was not run during loading.
        /// </summary>
        public virtual bool IsSecurityVerified => securityReport == null || securityReport.IsSecurityVerified;
        /// <summary>
        /// Get the <see cref="CompilationResult"/> for this assembly if it was runtime compiled.
        /// </summary>
        public virtual CompileResult CompilationResult => compileResult;
        /// <summary>
        /// Return a value indicating whether this assembly was runtime compiled or simply loaded from a source.
        /// </summary>
        public virtual bool IsRuntimeCompiled => compileResult != null;

        /// <summary>
        /// Get the name of the assembly.
        /// </summary>
        public virtual string Name => SystemAssembly.GetName().Name;
        /// <summary>
        /// Get the full name of the assembly.
        /// </summary>
        public virtual string FullName => SystemAssembly.GetName().FullName;
        /// <summary>
        /// Get the version of the assembly.
        /// </summary>
        public virtual Version Version => SystemAssembly.GetName().Version;
        /// <summary>
        /// Try to get the main type defined in the assembly.
        /// The default behaviour is to search for public MonoBehaviour types, then public classes, then public any type, and finally fall back to first defined type.
        /// Note that the special `Module` type is not considered as a possible MainType.
        /// </summary>
        public virtual ScriptType MainType => cachedMainType == null ? (cachedMainType = mainTypeSelector.SelectMainType(this, GetUserTypesOnly())) : cachedMainType;
        private IReadOnlyList<ScriptType> GetUserTypesOnly() => Types
            .Where(t => t.Name != "<Module>" 
                && t.FullName != "Microsoft.CodeAnalysis.EmbeddedAttribute" 
                && t.FullName != "System.Runtime.CompilerServices.RefSafetyRulesAttribute" 
                && t.Namespace != "Trivial.ExecutionSecurity")
            .ToList();

        /// <summary>
        /// Get the file path where the assembly was loaded from if it was loaded from disk.
        /// This value will be null if the assembly was loaded in memory.
        /// </summary>
        public virtual string AssemblyPath => Source.AssemblyHintPath;
        /// <summary>
        /// Get the file path where the debug symbols were loaded if the associated assembly was loaded from disk.
        /// This value will be null if the associated assembly was loaded in memory, of if debug symbols (.pdb) are not present along side the assembly.
        /// </summary>
        public virtual string DebugSymbolsPath => Source.DebugSymbolsHintPath;
        /// <summary>
        /// Return a value indicating whether the assembly was loaded with .pdb debug symbols or not.
        /// </summary>
        public virtual bool HasDebugSymbols => Source.HasDebugSymbols;
        /// <summary>
        /// Get the metadata reference information so that this assembly object can be referenced as a dependency during compilation.
        /// </summary>
        public virtual MetadataReference CompilationReference => Source.CompilationReference;

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="source"></param>
        /// <param name="compileResult"></param>
        /// <param name="securityReport"></param>
        /// <param name="mainTypeSelector"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected ScriptAssembly(ScriptDomain domain, AssemblySource source, CompileResult compileResult, CodeSecurityReport securityReport, IMainTypeSelector mainTypeSelector) 
        { 
            // Check for null
            if(domain == null)
                throw new ArgumentNullException(nameof(domain));

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            this.domain = domain;
            this.source = source;
            this.compileResult = compileResult;
            this.securityReport = securityReport;
            this.mainTypeSelector = mainTypeSelector;

            // Check for null selector
            if (mainTypeSelector == null)
                this.mainTypeSelector = DefaultMainTypeSelector;
        }

        // Methods
        /// <summary>
        /// Add this assembly to the security restrictions for the project user settings.
        /// </summary>
        public void AddToSecurityWhitelist()
        {
            // Add to user
            AddToSecurityWhitelist(RoslynCSharpSettings.UserSettings);
        }

        /// <summary>
        /// Add this assembly to the specified settings security restrictions.
        /// </summary>
        /// <param name="userSettings">The settings to add to</param>
        /// <exception cref="ArgumentNullException">userSettings is null</exception>
        public void AddToSecurityWhitelist(RoslynCSharpSettings userSettings)
        {
            // Check for null
            if(userSettings == null)
                throw new ArgumentNullException(nameof(userSettings));

            // Check for already added
            if (userSettings.CodeSecurityRestrictions.AssemblyRestrictions.Any(a => a.Name == Name) == true)
                return;

            // Add new entry for this assembly
            userSettings.CodeSecurityRestrictions.AssemblyRestrictions.Add(
                new CodeSecurity.CodeSecurityAssemblyRestriction(SystemAssembly));
        }

        /// <summary>
        /// Get the <see cref="AssemblyName"/> of this assembly.
        /// </summary>
        /// <returns></returns>
        public virtual AssemblyName GetName()
        {
            return SystemAssembly.GetName();
        }

        /// <summary>
        /// Load the assembly image bytes into memory.
        /// </summary>
        /// <returns></returns>
        public virtual byte[] LoadAssemblyImage()
        {
            return Source.LoadAssemblyImage();
        }

        /// <summary>
        /// Load the assembly debug symbols bytes into memory if present.
        /// This will return null if debug symbols are not available for the source assembly.
        /// </summary>
        /// <returns></returns>
        public virtual byte[] LoadDebugSymbolsImage()
        {
            return Source.LoadDebugSymbolsImage();
        }

        #region HasType
        /// <summary>
        /// Returns true if this <see cref="ScriptAssembly"/> defines a type with the specified name.
        /// Depending upon settings, name comparison may or may not be case sensitive.
        /// </summary>
        /// <param name="name">The name of the type to look for</param>
        /// <returns>True if a type with the specified name is defined</returns>
        public virtual bool HasType(string name)
        {
            return FindType(name) != null;
        }

        /// <summary>
        /// Returns true if this <see cref="ScriptAssembly"/> defines one or more types that inherit from the specified type.
        /// The specified type may be a base class or interface type.
        /// </summary>
        /// <param name="subType">The type to check for in the inheritace chain</param>
        /// <returns>True if there are one or more defined types that inherit from the specified type</returns>
        public virtual bool HasSubTypeOf(Type subType)
        {
            return FindSubTypeOf(subType) != null;
        }

        /// <summary>
        /// Returns true if this <see cref="ScriptAssembly"/> defined one or more types that inherit from the specified generic type.
        /// The specified generic type may be a base class or interface type.
        /// </summary>
        /// <typeparam name="T">The generic type to check for in the inheritance chain</typeparam>
        /// <returns>True if there are one or more defined types that inherit from the specified generic type</returns>
        public bool HasSubTypeOf<T>()
        {
            return HasSubTypeOf(typeof(T));
        }

        /// <summary>
        /// Returns true if this <see cref="ScriptAssembly"/> defines a type that inherits from the specified type and matches the specified name.
        /// Depending upon settings, name comparison may or may not be case sensitive.
        /// </summary>
        /// <param name="subType">The type to check for in the inheritance chain</param>
        /// <param name="name">The name of the type to look for</param>
        /// <returns>True if a type that inherits from the specified type and has the specified name is defined</returns>
        public virtual bool HasSubTypeOf(Type subType, string name)
        {
            return FindSubTypeOf(subType, name) != null;
        }

        /// <summary>
        /// Returns true if this <see cref="ScriptAssembly"/> defines a type that inherits from the specified genric type and matches the specified name.
        /// Depending upon settings, name comparison may or may not be case sensitive.
        /// </summary>
        /// <typeparam name="T">The generic type to check for in the inheritance chain</typeparam>
        /// <param name="name">The name of the type to look for</param>
        /// <returns>True if a type that inherits from the specified type and has the specified name is defined</returns>
        public bool HasSubTypeOf<T>(string name)
        {
            return HasSubTypeOf(typeof(T), name);
        }
        #endregion

        #region FindType
        /// <summary>
        /// Attempts to find a type defined in this <see cref="ScriptAssembly"/> with the specified name.
        /// Depending upon settings, name comparison may or may not be case sensitive.
        /// </summary>
        /// <param name="name">The name of the type to look for</param>
        /// <returns>An instance of <see cref="ScriptType"/> representing the found type or null if the type could not be found</returns>
        public virtual ScriptType FindType(string name)
        {
            // Try to find the type
            Type type = SystemAssembly.GetType(name, false, false);

            // Check for error
            if (type == null)
            {
                return null;
            }

            // Get the cached script type
            return Types.FirstOrDefault(t => t.FullName == name);
        }

        /// <summary>
        /// Attempts to find a type defined in this <see cref="ScriptAssembly"/> from the specified system type.
        /// </summary>
        /// <param name="type">The system type to look for</param>
        /// <returns>An instance of <see cref="ScriptType"/> representing the found type or null if the type could not be found</returns>
        public virtual ScriptType FindType(Type type)
        {
            // Check for error
            if (type == null)
                return null;

            // Get the cached script type
            return Types.FirstOrDefault(t => t.FullName == type.FullName);
        }

        /// <summary>
        /// Attempts to find a type defined in this <see cref="ScriptAssembly"/> that inherits from the specified base type.
        /// If there is more than one type that inherits from the specified base type, then the first matching type will be returned.
        /// If you want to find all types then use <see cref="FindAllSubTypesOf(Type, bool, bool)"/>. 
        /// </summary>
        /// <param name="subType">The type to check for in the inheritance chain</param>
        /// <param name="includeNonPublic">Should the search include non public types</param>
        /// <param name="findNestedTypes">Should nested types be included in the search</param>
        /// <returns>An instance of <see cref="ScriptType"/> representing the found type or null if the type could not be found</returns>
        public virtual ScriptType FindSubTypeOf(Type subType, bool includeNonPublic = true, bool findNestedTypes = true)
        {
            // Find all types in the assembly
            foreach (ScriptType type in Types)
            {
                // Check for non-public discoverability
                if (includeNonPublic == false)
                    if (type.IsPublic == false)
                        continue;

                // Check for skip nested types
                if (type.IsNested == true && findNestedTypes == false)
                    continue;

                // Check for subtype
                if (type.IsSubTypeOf(subType) == true)
                {
                    // Return first occurrence
                    return type;
                }
            }

            // Not found
            return null;
        }

        /// <summary>
        /// Attempts to find a type defined in this <see cref="ScriptAssembly"/> that inherits from the specified generic type. 
        /// If there is more than one type that inherits from the specified generic type, then the first matching type will be returned.
        /// If you want to find all types then use <see cref="FindAllSubTypesOf{T}(bool, bool)"/>.
        /// </summary>
        /// <param name="includeNonPublic">Should the search include non public types</param>
        /// <param name="findNestedTypes">Should nested types be included in the search</param>
        /// <typeparam name="T">The generic type to check for in the inheritance chain</typeparam>
        /// <returns>An instance of <see cref="ScriptType"/> representing the found type or null if the type could not be found</returns>
        public ScriptType FindSubTypeOf<T>(bool includeNonPublic = true, bool findNestedTypes = true)
        {
            return FindSubTypeOf(typeof(T), includeNonPublic, findNestedTypes);
        }

        /// <summary>
        /// Attempts to find a type defined in this <see cref="ScriptAssembly"/> that inherits from the specified base type and matches the specified name.
        /// Depending upon settings, name comparison may or may not be case sensitive.
        /// </summary>
        /// <param name="subType">The type to check for in the inheritance chain</param>
        /// <param name="name">The name of the type to look for</param>
        /// <returns>An instance of <see cref="ScriptType"/> representing the found type or null if the type could not be found</returns>
        public virtual ScriptType FindSubTypeOf(Type subType, string name)
        {
            // Find a type with the specified name
            ScriptType type = FindType(name);

            // Check for error
            if (type == null)
                return null;

            // Make sure the identifier type is a subclass
            if (type.IsSubTypeOf(subType) == true)
                return type;

            return null;
        }

        /// <summary>
        /// Attempts to find a type defined in this <see cref="ScriptAssembly"/> that inherits from the specified generic type and matches the specified name. 
        /// Depending upon settings, name comparison may or may not be case sensitive.
        /// </summary>
        /// <typeparam name="T">The generic type to check for in the inheritance chain</typeparam>
        /// <param name="name">The name of the type to look for</param>
        /// <returns>An instance of <see cref="ScriptType"/> representing the found type or null if the type could not be found</returns>
        public ScriptType FindSubTypeOf<T>(string name)
        {
            return FindSubTypeOf(typeof(T), name);
        }

        /// <summary>
        /// Attempts to find all types defined in this <see cref="ScriptAssembly"/> that inherits from the specified type.
        /// If there are no types that inherit from the specified type then the return value will be an empty array.
        /// </summary>
        /// <param name="subType">The type to check for in the inheritance chain</param>
        /// <param name="includeNonPublic">Should the search include non public types</param>
        /// <param name="findNestedTypes">Should nested types be included in the search</param>
        /// <returns>(Not Null) An array of <see cref="ScriptType"/> or an empty array if no matching type was found</returns>
        public virtual ScriptType[] FindAllSubTypesOf(Type subType, bool includeNonPublic = true, bool findNestedTypes = true)
        {
            // Use shared list
            matchedTypes.Clear();

            // Find all types
            foreach (ScriptType type in Types)
            {
                // Check for non-public discovery
                if (includeNonPublic == false)
                    if (type.IsPublic == false)
                        continue;

                // Check for skip nested types
                if (type.IsNested == true && findNestedTypes == false)
                    continue;

                // Make sure the type is a Unity object
                if (type.IsSubTypeOf(subType) == true)
                {
                    // Add type
                    matchedTypes.Add(type);
                }
            }

            // Get the array
            return matchedTypes.ToArray();
        }

        /// <summary>
        /// Attempts to find all types defined in this <see cref="ScriptAssembly"/> that inherit from the specified generic type.
        /// If there are no types that inherit from the specified type then the return value will be an empty array.
        /// </summary>
        /// <typeparam name="T">The generic type to check for in the inheritance chain</typeparam>
        /// <returns>(Not Null) An array of <see cref="ScriptType"/> or an empty array if no matching type was found</returns>
        public ScriptType[] FindAllSubTypesOf<T>(bool includeNonPublic = true, bool findNestedTypes = true)
        {
            return FindAllSubTypesOf(typeof(T), includeNonPublic, findNestedTypes);
        }

        /// <summary>
        /// Returns an array of all defined types in this <see cref="ScriptAssembly"/>. 
        /// </summary>
        /// <returns>An array of <see cref="ScriptType"/> representing all types defined in this <see cref="ScriptAssembly"/></returns>
        public virtual ScriptType[] FindAllTypes(bool includeNonPublic = true, bool findNestedTypes = true)
        {
            // Use shared array
            matchedTypes.Clear();
            matchedTypes.AddRange(Types);

            // Remove nested types
            if (findNestedTypes == false)
                matchedTypes.RemoveAll(t => t.IsNested == true);

            // Get as array
            return matchedTypes.ToArray();
        }

        /// <summary>
        /// Attempts to find all types defined in this <see cref="ScriptAssembly"/> that inherit from <see cref="UnityEngine.Object"/>.  
        /// If there are no types that inherit from <see cref="UnityEngine.Object"/> then the return value will be an empty array.
        /// </summary>
        /// <returns>(Not Null) An array of <see cref="ScriptType"/> or an empty array if no matching type was found</returns>
        public ScriptType[] FindAllUnityTypes(bool includeNonPublic = true, bool findNestedTypes = true)
        {
            return FindAllSubTypesOf<UnityEngine.Object>(includeNonPublic, findNestedTypes);
        }

        /// <summary>
        /// Attempts to find all types defined in this <see cref="ScriptAssembly"/> that inherit from <see cref="UnityEngine.MonoBehaviour"/>.  
        /// If there are no types that inherit from <see cref="UnityEngine.MonoBehaviour"/> then the return value will be an empty array.
        /// </summary>
        /// <returns>(Not Null) An array of <see cref="ScriptType"/> or an empty array if no matching type was found</returns>
        public ScriptType[] FindAllMonoBehaviourTypes(bool includeNonPublic = true, bool findNestedTypes = true)
        {
            return FindAllSubTypesOf<UnityEngine.MonoBehaviour>(includeNonPublic, findNestedTypes);
        }

        /// <summary>
        /// Attempts to find all types defined in this <see cref="ScriptAssembly"/> that inherit from <see cref="UnityEngine.ScriptableObject"/>.  
        /// If there are no types that inherit from <see cref="UnityEngine.ScriptableObject"/> then the return value will be an empty array.
        /// </summary>
        /// <returns>(Not Null) An array of <see cref="ScriptType"/> or an empty array if no matching type was found</returns>
        public ScriptType[] FindAllScriptableObjectTypes(bool includeNonPublic = true, bool findNestedTypes = true)
        {
            return FindAllSubTypesOf<UnityEngine.ScriptableObject>(includeNonPublic, findNestedTypes);
        }
        #endregion

        #region EnumerateType
        /// <summary>
        /// Enumerate all types defined in this <see cref="ScriptAssembly"/> that inherits from the specified type.
        /// </summary>
        /// <param name="subType">The type to check for in the inheritance chain</param>
        /// <param name="includeNonPublic">Should the search include non public types</param>
        /// <param name="enumerateNestedTypes">Should nested types be included in the search</param>
        /// <returns>Enumerable of matching results</returns>
        public virtual IEnumerable<ScriptType> EnumerateAllSubTypesOf(Type subType, bool includeNonPublic = true, bool enumerateNestedTypes = true)
        {
            foreach (ScriptType type in Types)
            {
                // Check for visible
                if (includeNonPublic == false)
                    if (type.IsPublic == false)
                        continue;

                // Check for skip nested types
                if (type.IsNested == true && enumerateNestedTypes == false)
                    continue;

                // Check for sub type
                if (type.IsSubTypeOf(subType) == true)
                    yield return type;
            }
        }

        /// <summary>
        /// Enumerate all types defined in this <see cref="ScriptAssembly"/> that inherit from the specified generic type.
        /// </summary>
        /// <typeparam name="T">The generic type to check for in the inheritance chain</typeparam>
        /// <returns>Enumerable of matching results</returns>
        public IEnumerable<ScriptType> EnumerateAllSubTypesOf<T>(bool includeNonPublic = true, bool enumerateNestedTypes = true)
        {
            return EnumerateAllSubTypesOf(typeof(T), includeNonPublic, enumerateNestedTypes);
        }

        /// <summary>
        /// Enumerate all defined types in this <see cref="ScriptAssembly"/>. 
        /// </summary>
        /// <returns>Enumerable of all results</returns>
        public virtual IEnumerable<ScriptType> EnumerateAllTypes(bool includeNonPublic = true, bool enumerateNestedTypes = true)
        {
            foreach (ScriptType type in Types)
            {
                // Check for visible
                if (includeNonPublic == false)
                    if (type.IsPublic == false)
                        continue;

                // Check for skip nested types
                if (type.IsNested == true && enumerateNestedTypes == false)
                    continue;

                // Return type
                yield return type;
            }
        }

        /// <summary>
        /// Enumerate all types defined in this <see cref="ScriptAssembly"/> that inherit from <see cref="UnityEngine.Object"/>.  
        /// </summary>
        /// <returns>Enumerable of matching results</returns>
        public IEnumerable<ScriptType> EnumerateAllUnityTypes(bool includeNonPublic = true, bool enumerateNestedTypes = true)
        {
            return EnumerateAllSubTypesOf<UnityEngine.Object>(includeNonPublic, enumerateNestedTypes);
        }

        /// <summary>
        /// Enumerate all types defined in this <see cref="ScriptAssembly"/> that inherit from <see cref="UnityEngine.MonoBehaviour"/>.  
        /// </summary>
        /// <returns>Enumerable of matching results</returns>
        public IEnumerable<ScriptType> EnumerateAllMonoBehaviourTypes(bool includeNonPublic = true, bool enumerateNestedTypes = true)
        {
            return EnumerateAllSubTypesOf<UnityEngine.MonoBehaviour>(includeNonPublic, enumerateNestedTypes);
        }

        /// <summary>
        /// Enumerate all types defined in this <see cref="ScriptAssembly"/> that inherit from <see cref="UnityEngine.ScriptableObject"/>.  
        /// </summary>
        /// <returns>Enumerable of matching results</returns>
        public IEnumerable<ScriptType> EnumerateAllScriptableObjectTypes(bool includeNonPublic = true, bool enumerateNestedTypes = true)
        {
            return EnumerateAllSubTypesOf<UnityEngine.ScriptableObject>(includeNonPublic, enumerateNestedTypes);
        }
        #endregion

        #region EmbeddedResources
        /// <summary>
        /// Try to find the embedded resource stream for reading with the given search name.
        /// </summary>
        /// <param name="resourceName">The name of the embedded resource or shortened search term to find the resource by partial name</param>
        /// <returns>A readable stream or null</returns>
        public virtual Stream GetEmbeddedResourceStream(string resourceName)
        {
            // Search for full resource name
            string resource = FindEmbeddedResourceName(resourceName);

            // Try to get the stream
            return SystemAssembly.GetManifestResourceStream(resource);
        }

        /// <summary>
        /// Try to find and read the embedded resource content as a string from the given search name.
        /// </summary>
        /// <param name="resourceName">The name of the embedded resource or shortened search term to find the resource by partial name</param>
        /// <returns>A string containing the contents of the embedded resource or null</returns>
        public string GetEmbeddedResourceText(string resourceName)
        {
            // Get the resource stream
            Stream resourceStream = GetEmbeddedResourceStream(resourceName);

            if (resourceStream != null)
            {
                // Read all text
                using (StreamReader reader = new StreamReader(resourceStream))
                {
                    return reader.ReadToEnd();
                }
            }
            return null;
        }

        /// <summary>
        /// Try to find and read the embedded resource content as a byte array from the given search name.
        /// </summary>
        /// <param name="resourceName">The name of the embedded resource or shortened search term to find the resource by partial name</param>
        /// <returns>A byte array containing the raw contents of the embedded resource or null</returns>
        public byte[] GetEmbeddedResourceBytes(string resourceName)
        {
            // Get the resource stream
            Stream resourceStream = GetEmbeddedResourceStream(resourceName);

            if (resourceStream != null)
            {
                using (resourceStream)
                {
                    using (MemoryStream memory = new MemoryStream())
                    {
                        resourceStream.CopyTo(memory);
                        return memory.ToArray();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Attempt to search for a full embedded resource name from the given search string.
        /// Useful for quickly finding resources without needing to include the resource namespace.
        /// </summary>
        /// <param name="searchName">The search term used to find the closest matching embedded resource</param>
        /// <returns>The full path of the embedded resource or null</returns>
        public string FindEmbeddedResourceName(string searchName)
        {
            string[] allResources = GetEmbeddedResourceNames();

            // Check all
            foreach (string resource in allResources)
            {
                if (resource.IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return resource;
            }
            return null;
        }

        /// <summary>
        /// Get the full names of all embedded resources including namespace.
        /// Array will be empty if there are no embedded resources in the assembly.
        /// </summary>
        /// <returns>An array of all embedded resources full names</returns>
        public string[] GetEmbeddedResourceNames()
        {
            if (cachedEmbeddedResourceNames == null)
                cachedEmbeddedResourceNames = GetEmbeddedResourceNamesImpl();

            return cachedEmbeddedResourceNames;
        }

        /// <summary>
        /// Get the full names of all embedded resources including namespace.
        /// Array should be empty if there are no embedded resources in the assembly.
        /// </summary>
        /// <returns>An array of all embedded resources full names</returns>
        protected virtual string[] GetEmbeddedResourceNamesImpl()
        {
            return SystemAssembly.GetManifestResourceNames();
        }
        #endregion

        /// <summary>
        /// Create a default Mono/Jit <see cref="ScriptAssembly"/> metadata object.
        /// </summary>
        /// <param name="domain">The domain that the assembly belongs to</param>
        /// <param name="source">The source for the assembly</param>
        /// <param name="compileResult">The compilation result for the assembly, or null if it was not compiled</param>
        /// <param name="securityReport">The security report for the assembly, or null if security checks did not run</param>
        /// <param name="mainTypeSelector">The main type selector for the assembly</param>
        /// <returns>A loaded <see cref="ScriptAssembly"/> with default implementation</returns>
        public static ScriptAssembly CreateDefault(ScriptDomain domain, AssemblySource source, CompileResult compileResult, CodeSecurityReport securityReport, IMainTypeSelector mainTypeSelector)
        {
            return new ScriptAssemblyImpl(domain, source, compileResult, securityReport, mainTypeSelector);
        }
    }
}

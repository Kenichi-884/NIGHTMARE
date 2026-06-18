using System;
using System.IO;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoslynCSharp
{
    [Serializable]
    public sealed class AssemblyReferenceSource
    {
        // Type
        /// <summary>
        /// The type of assembly reference path, used to specify the same relative assembly location on different devices or versions.
        /// </summary>
        public enum AssemblyReferencePathType
        {
            /// <summary>
            /// The path is an absolute path and is only usable on the device it was created for.
            /// </summary>
            Absolute = 0,
            /// <summary>
            /// The path is relative to the Unity editor install location.
            /// </summary>
            UnityInstall,
            /// <summary>
            /// The path is relative to the Unity project location.
            /// </summary>
            UnityProject,
        }

        // Properties
        /// <summary>
        /// The name of the assembly that is referenced.
        /// </summary>
        [field: SerializeField]
        public string AssemblyName { get; private set; }
        /// <summary>
        /// The original path of the assembly that is referenced.
        /// </summary>
        [field: SerializeField]
        public string AssemblyPath { get; private set; }
        /// <summary>
        /// Get the full path to the assembly.
        /// <see cref="AssemblyPath"/> may be relative to the Unity install or project folder, this property will resolve the full rooted path.
        /// </summary>
        public string AssemblyFullPath
        {
            get
            {
                switch (AssemblyPathType)
                {
                    case AssemblyReferencePathType.UnityInstall:
                        {
#if UNITY_EDITOR
                            // Get the install folder
                            string editorInstallFolder = Directory.GetParent(EditorApplication.applicationPath).FullName;

                            // Get full path
                            return Path.Combine(editorInstallFolder, AssemblyPath);
#else
                            throw new NotSupportedException("Path relative to Unity install directory is not supported at runtime");
#endif
                        }
                    case AssemblyReferencePathType.UnityProject:
                        {
                            // Get the project folder
                            string projectFolder = Directory.GetParent(Application.dataPath).FullName;

                            // Get full path
                            return Path.Combine(projectFolder, AssemblyPath);
                        }
                }
                return AssemblyPath;
            }
        }
        /// <summary>
        /// Checks whether the specified assembly reference exists if it is set.
        /// False if it does not exist or if this asset does not yet reference an assembly.
        /// </summary>
        public bool AssemblyExists
        {
            get
            {
                try
                {
                    return File.Exists(AssemblyFullPath);
                }
                catch
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// The type of path that is stored by this assembly.
        /// </summary>
        [field: SerializeField]
        public AssemblyReferencePathType AssemblyPathType { get; private set; }

        /// <summary>
        /// Get the last time the assembly was compiled in UTC file time.
        /// </summary>
        [field: SerializeField]
        public long LastWriteTimeUTC { get; private set; }
        /// <summary>
        /// Get the last date and time that the assembly was compiled.
        /// </summary>
        public DateTime LastWriteTime => DateTime.FromFileTimeUtc(LastWriteTimeUTC);

        // Methods
        /// <summary>
        /// Update this reference asset with the provided information.
        /// </summary>
        /// <param name="assemblyPath">The path of the source assembly, used to read the assembly image</param>
        /// <param name="assemblyName">The name of the assembly. Should be a short name without public key information</param>
        /// <exception cref="ArgumentException">Assembly path is null or empty</exception>
        /// <exception cref="FileNotFoundException">Could not find the assembly path</exception>
        public void UpdateAssemblyReference(string assemblyPath, string assemblyName)
        {
            // Check args
            if (string.IsNullOrEmpty(assemblyPath) == true)
                throw new ArgumentException("Assembly path cannot be null or empty");

            if (string.IsNullOrEmpty(assemblyName) == true)
                throw new ArgumentException("Assembly name cannot be null or empty");

            // Check for exists
            if (File.Exists(assemblyPath) == false)
                throw new FileNotFoundException(assemblyPath);

            // Check path type
            string assemblyFullPath = assemblyPath;
            AssemblyReferencePathType pathType = AssemblyReferencePathType.Absolute;

#if UNITY_EDITOR
            // Check for relative to Unity project
            string editorInstallFolder = Directory.GetParent(EditorApplication.applicationPath).FullName;

            // Try to get relative
            string editorRelativePath = Path.GetRelativePath(editorInstallFolder, assemblyPath);

            // Check for relative
            if(editorRelativePath != assemblyPath && editorRelativePath.StartsWith("..") == false && Path.IsPathRooted(editorRelativePath) == false && Path.IsPathRooted(assemblyPath) == true)
            {
                pathType = AssemblyReferencePathType.UnityInstall;
                assemblyPath = editorRelativePath;
            }
            else
            {
                // Try to get relative
                string projectRelativePath = GetProjectRelativePathSafe(assemblyPath);

                // Check for relative
                if(string.IsNullOrEmpty(projectRelativePath) == false)
                {
                    pathType = AssemblyReferencePathType.UnityProject;
                    assemblyPath = projectRelativePath;
                }
                else if (Path.IsPathRooted(assemblyPath) == false)
                {
                    pathType = AssemblyReferencePathType.UnityProject;
                }
            }
#endif

            // Init the assembly
            this.AssemblyPath = assemblyPath;
            this.AssemblyPathType = pathType;
            this.AssemblyName = assemblyName;
            this.LastWriteTimeUTC = File.GetLastWriteTimeUtc(assemblyFullPath).ToFileTimeUtc();
        }

        /// <summary>
        /// Update this reference asset with information for the provided assembly.
        /// Note that the specified assembly `Location` property must have a valid load path set, otherwise you should use <see cref="UpdateAssemblyReference(string, string)"/>.
        /// </summary>
        /// <param name="assembly">The assembly to update from</param>
        /// <exception cref="ArgumentNullException">Assembly is null</exception>
        /// <exception cref="ArgumentException">Assembly `Location` property is not valid</exception>
        public void UpdateAssemblyReference(Assembly assembly)
        {
            // Check for null
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            // Check for location not available
            if (string.IsNullOrEmpty(assembly.Location) == true)
                throw new ArgumentException("Location of specified assembly is not available");

            // Update the assembly info
            UpdateAssemblyReference(assembly.Location, assembly.GetName().Name);
        }

        public void RefreshAssemblyReference()
        {
            // Check for any reference - do nothing
            if (string.IsNullOrEmpty(AssemblyName) == true || string.IsNullOrEmpty(AssemblyPath) == true)
                return;

            // Update reference - use full path because it is required for refreshing reference
            UpdateAssemblyReference(AssemblyFullPath, AssemblyName);
        }

        /// <summary>
        /// Attempt to check and update the source assembly if it has been modified since last refresh.
        /// </summary>
        /// <returns>True if the assembly location is valid and the assembly was modified on disk, or false if not</returns>
        public bool AutoRefreshAssemblyReference()
        {
            // Get the full path
            string fullPath = AssemblyFullPath;

            // Check for available
            if (string.IsNullOrEmpty(fullPath) == false && File.Exists(fullPath) == true)
            {
                // Get the last write time
                DateTime newLastWriteTime = File.GetLastWriteTimeUtc(fullPath);

                // Check for newer
                if (newLastWriteTime > LastWriteTime)
                {
                    // Need to update the reference because the assembly image may have changed
                    RefreshAssemblyReference();
                    return true;
                }
            }
            // Assembly has not changed
            return false;
        }

        private static string GetProjectRelativePathSafe(string path)
        {
            // Check for null
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            // Get full path
            if (Path.IsPathRooted(path) == false)
                path = NormalizePath(Path.GetFullPath(path));

            // Get project path
            string projectPath = NormalizePath(Application.dataPath);

            Uri root = null;
            Uri child = null;

            try
            {
                root = new Uri(projectPath);
                child = new Uri(path);
            }
            catch
            {
                return path;
            }

            // Get the normalized relative path
            return NormalizePath(Uri.UnescapeDataString(root.MakeRelativeUri(child).OriginalString));
        }

        private static string NormalizePath(string path)
        {
            // Check for null
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            const char WindowsSeparator = '\\';
            const char UnixSeparator = '/';

            // Replace separators
            path = path.Replace(WindowsSeparator, UnixSeparator);

            // Trim any trailing slashes
            path = path.TrimEnd(UnixSeparator);

            return path;
        }
    }
}

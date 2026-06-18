using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Xml;

namespace RoslynCSharp.Project
{
    /// <summary>
    /// Represents a C# project file (.csproj) that can be used for compilation.
    /// </summary>
    public sealed class CSharpProject
    {
        // Type
        private readonly struct AssemblyReference : ICompilationReference
        {
            // Private
            private readonly string assemblyNameOrPath;

            // Properties
            public MetadataReference CompilationReference => MetadataReference.CreateFromFile(assemblyNameOrPath);

            // Constructor
            public AssemblyReference(string assemblyNameOrPath)
            {
                this.assemblyNameOrPath = assemblyNameOrPath;
            }
        }

        // Private
        private readonly List<string> sources = new();
        private readonly List<string> defineSymbols = new();
        private readonly List<string> assemblyReferences = new();
        private readonly List<string> projectReferences = new();

        // Properties
        /// <summary>
        /// The name of the output assembly.
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Get all C# source files that are defined in this project.
        /// </summary>
        public IList<string> Sources => sources;
        /// <summary>
        /// Get all define symbols that are defined in this project.
        /// </summary>
        public IList<string> DefineSymbols => defineSymbols;
        /// <summary>
        /// Get the names or paths of all assemblies that are referenced by this project.
        /// </summary>
        public IList<string> AssemblyReferences => assemblyReferences;
        /// <summary>
        /// Get the name of all other CSharp projects that are referenced by this project.
        /// </summary>
        public IList<string> ProjectReferences => projectReferences;
        /// <summary>
        /// Get the exception that was thrown during parsing if any.
        /// </summary>
        public Exception ParseException { get; internal set; }

        // Methods
        /// <summary>
        /// Get all referenced in this project as an enumerable of <see cref="ICompilationReference"/> that can be used in a compile request.
        /// </summary>
        /// <returns>An enumerable of referenceable objects</returns>
        public IEnumerable<ICompilationReference> GetAssemblyReferences()
        {
            // Process all
            foreach(string assemblyReference in assemblyReferences)
            {
                // Create reference
                yield return new AssemblyReference(assemblyReference);
            }
        }

        /// <summary>
        /// Create a new project parsed from the specified .csproj file path.
        /// If there is an error during loading then <see cref="ParseException"/> will be set with the load exception.
        /// </summary>
        /// <param name="cSharpProjectFile">The path to the .csproj file</param>
        /// <returns>The loaded C# project</returns>
        public static CSharpProject Parse(string cSharpProjectFile)
        {
            // Create the project
            CSharpProject project = new();

            // Open for reading
            try
            {
                // Create reader
                using(XmlReader reader = XmlReader.Create(cSharpProjectFile))
                {
                    // Try to parse project
                    new CSharpProjectParser(project)
                        .Parse(reader);
                }
            }
            catch(Exception e)
            {
                project.ParseException = e;
            }

            return project;
        }
    }
}

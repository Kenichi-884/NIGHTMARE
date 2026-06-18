using Microsoft.CodeAnalysis;
using System;
using System.IO;
using System.Reflection;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a specific assembly location with optional debug symbols.
    /// </summary>
    [Serializable]
    public abstract class AssemblySource : ICompilationReference
    {
        // Type
        [Serializable]
        private sealed class AssemblySource_Memory : AssemblySource
        {
            // Private
            private readonly byte[] assemblyImage;
            private readonly byte[] debugSymbolsImage;

            // Properties
            public override string AssemblyHintPath => null;
            public override string DebugSymbolsHintPath => null;
            public override bool HasDebugSymbols => debugSymbolsImage != null;
            public override MetadataReference CompilationReference => MetadataReference.CreateFromImage(assemblyImage);

            // Constructor
            public AssemblySource_Memory(byte[] assemblyImage, byte[] debugSymbolsImage)
            {
                // Check for null
                if(assemblyImage == null)
                    throw new ArgumentNullException(nameof(assemblyImage));

                this.assemblyImage = assemblyImage;
                this.debugSymbolsImage = debugSymbolsImage;
            }

            // Methods
            public override Assembly LoadAssembly(IAssemblyLoader assemblyLoader)
            {
                // Check for null
                if(assemblyLoader == null)
                    throw new ArgumentNullException(nameof(assemblyLoader));

                // Load the assembly
                return assemblyLoader.LoadAssembly(assemblyImage, debugSymbolsImage);
            }

            public override byte[] LoadAssemblyImage()
            {
                return assemblyImage;
            }

            public override byte[] LoadDebugSymbolsImage()
            {
                return debugSymbolsImage;
            }

            public override Trivial.CodeSecurity.AssemblySource GetSecuritySource()
            {
                return Trivial.CodeSecurity.AssemblySource.FromImage(assemblyImage, debugSymbolsImage);
            }
        }

        [Serializable]
        private sealed class AssemblySource_File : AssemblySource
        {
            // Private
            private readonly string assemblyPath;
            private readonly string debugSymbolsPath;

            // Properties
            public override string AssemblyHintPath => assemblyPath;
            public override string DebugSymbolsHintPath => debugSymbolsPath;
            public override bool HasDebugSymbols => string.IsNullOrEmpty(debugSymbolsPath) == false && File.Exists(debugSymbolsPath) == true;
            public override MetadataReference CompilationReference => MetadataReference.CreateFromFile(assemblyPath);

            // Constructor
            public AssemblySource_File(string assemblyPath, string debugSymbolsPath)
            {
                // Check for null or empty
                if (string.IsNullOrEmpty(assemblyPath) == true)
                    throw new ArgumentException("Assembly path cannot be null or empty");

                this.assemblyPath = assemblyPath;
                this.debugSymbolsPath = debugSymbolsPath;
            }

            // Methods
            public override Assembly LoadAssembly(IAssemblyLoader assemblyLoader)
            {
                // Check for null
                if (assemblyLoader == null)
                    throw new ArgumentNullException(nameof(assemblyLoader));

                // Load the assembly
                return assemblyLoader.LoadAssembly(assemblyPath, debugSymbolsPath);
            }

            public override byte[] LoadAssemblyImage()
            {
                return File.ReadAllBytes(assemblyPath);
            }

            public override byte[] LoadDebugSymbolsImage()
            {
                if(HasDebugSymbols == true)
                    return File.ReadAllBytes(debugSymbolsPath);

                return null;
            }

            public override Trivial.CodeSecurity.AssemblySource GetSecuritySource()
            {
                return Trivial.CodeSecurity.AssemblySource.FromFile(assemblyPath);
            }
        }

        // Properties
        /// <summary>
        /// A hint path where the assembly is located on disk.
        /// </summary>
        public abstract string AssemblyHintPath { get; }
        /// <summary>
        /// A hint path where the debug symbols are located on disk.
        /// </summary>
        public abstract string DebugSymbolsHintPath { get; }

        /// <summary>
        /// Returns true if Portable PDB debug symbols are present in the compilation output.
        /// Requires compiling with optimize disabled to produce a debug build.
        /// </summary>
        public abstract bool HasDebugSymbols { get; }

        /// <summary>
        /// Get the metadata reference for this compiled assembly.
        /// It allows this object to be passed directly as a compiler reference.
        /// </summary>
        public abstract MetadataReference CompilationReference { get; }

        // Methods
        /// <summary>
        /// Load the compiled assembly into memory.
        /// </summary>
        /// <returns></returns>
        public abstract Assembly LoadAssembly(IAssemblyLoader assemblyLoader);

        /// <summary>
        /// Load the entire assembly image bytes.
        /// </summary>
        /// <returns></returns>
        public abstract byte[] LoadAssemblyImage();

        /// <summary>
        /// Load the entire portable PDB debug symbols image bytes.
        /// </summary>
        /// <returns></returns>
        public abstract byte[] LoadDebugSymbolsImage();

        /// <summary>
        /// Get the assembly source for code security verification purposes.
        /// </summary>
        /// <returns></returns>
        public abstract Trivial.CodeSecurity.AssemblySource GetSecuritySource();

        /// <summary>
        /// Create an assembly source from the specified assembly image bytes.
        /// </summary>
        /// <param name="assemblyImage">The assembly image bytes (.dll PE file bytes)</param>
        /// <param name="debugSymbolsImage">The optional debug symbols bytes</param>
        /// <returns>An assembly source representing the provided assembly image</returns>
        public static AssemblySource FromMemory(byte[] assemblyImage, byte[] debugSymbolsImage = null)
        {
            return new AssemblySource_Memory(assemblyImage, debugSymbolsImage);
        }

        /// <summary>
        /// Create an assembly source from the specified assembly path.
        /// </summary>
        /// <param name="assemblyPath">The path of the managed .dll PE assembly file</param>
        /// <param name="debugSymbolsPath">The optional path of the debug symbols if present (Usually replace the assembly path extension with .pdb extension)</param>
        /// <returns>An assembly source representing the provided assembly path</returns>
        public static AssemblySource FromFile(string assemblyPath, string debugSymbolsPath = null)
        {
            return new AssemblySource_File(assemblyPath, debugSymbolsPath);
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a single compilation request which can comprise of one or many source texts, files or syntax trees.
    /// </summary>
    public sealed class CompileRequest
    {
        // Private
        private readonly CompileFlags flags = null;
        private readonly IEnumerable<IParserProcessor> parserProcessors = null;
        private readonly IEnumerable<ICompilationProcessor> compilationProcessors = null;

        // Constructor
        /// <summary>
        /// Create a new compilation request.
        /// </summary>
        /// <param name="flags">The compiler flags to use or null for default</param>
        /// <param name="parserProcessors">The optional parser processor to use for additional parser post processing</param>
        /// <param name="compilationProcessors">The optional compilation processor to use for additional IL post processing</param>
        public CompileRequest(CompileFlags flags = null, IEnumerable<IParserProcessor> parserProcessors = null, IEnumerable<ICompilationProcessor> compilationProcessors = null)
        {
            this.flags = flags;
            this.parserProcessors = parserProcessors;
            this.compilationProcessors = compilationProcessors;

            // Check for no options
            if (flags == null)
                this.flags = CompileFlags.Default;
        }

        // Methods
        #region Parse
        /// <summary>
        /// Parse the specified source file into syntax trees.
        /// Note that parser processors will be run at this stage.
        /// </summary>
        /// <param name="cSharpSource">The C# source string to parse</param>
        /// <param name="defineSymbols">The optional define symbols used to determine which define conditions are met</param>
        /// <returns>The syntax trees representing the parsed and processed code</returns>
        /// <exception cref="ArgumentException">Input source is null or empty</exception>
        public SyntaxTree[] ParseSource(string cSharpSource, IEnumerable<string> defineSymbols = null)
        {
            // Check arg
            if (string.IsNullOrEmpty(cSharpSource) == true)
                throw new ArgumentException("Source cannot be null or empty");

            // Parse text
            SyntaxTree syntaxTree = ParseSourceTree(cSharpSource, defineSymbols);

            // Run processor
            return RunParserProcessor(syntaxTree);
        }

        /// <summary>
        /// Parse the specified source strings into syntax trees.
        /// Note that parser processors will be run at this stage.
        /// </summary>
        /// <param name="cSharpSources">The C# source strings to parse</param>
        /// <param name="defineSymbols">The optional define symbols used to determine which define conditions are met</param>
        /// <returns>The syntax trees representing the parsed and processed code</returns>
        /// <exception cref="ArgumentNullException">Input sources is null</exception>
        public SyntaxTree[] ParseSources(IEnumerable<string> cSharpSources, IEnumerable<string> defineSymbols = null)
        {
            // Check for null
            if(cSharpSources == null)
                throw new ArgumentNullException(nameof(cSharpSources));

            // Parse all
            SyntaxTree[] syntaxTrees = cSharpSources
                .Where(s => string.IsNullOrEmpty(s) == false)
                .Select(s => ParseSourceTree(s, defineSymbols))
                .ToArray();

            // Run processors
            return RunParserProcessor(syntaxTrees);
        }

        /// <summary>
        /// Parse the specified source file into syntax trees.
        /// Note that parser processors will be run at this stage.
        /// </summary>
        /// <param name="cSharpFile">The C# source file to parse</param>
        /// <param name="defineSymbols">The optional define symbols used to determine which define conditions are met</param>
        /// <returns>The syntax trees representing the parsed and processed code</returns>
        /// <exception cref="ArgumentException">The file path is null or empty</exception>
        public SyntaxTree[] ParseFile(string cSharpFile, IEnumerable<string> defineSymbols = null)
        {
            // Check arg
            if (string.IsNullOrEmpty(cSharpFile) == true)
                throw new ArgumentException("File cannot be null or empty");

            // Parse file
            SyntaxTree syntaxTree = ParseFileTree(cSharpFile, defineSymbols);

            // Run processor
            return RunParserProcessor(syntaxTree);
        }

        /// <summary>
        /// Parse the specified source files into syntax trees.
        /// Note that parser processors will be run at this stage.
        /// </summary>
        /// <param name="cSharpFiles">The C# source files to parse</param>
        /// <param name="defineSymbols">The optional define symbols used to determine which define conditions are met</param>
        /// <returns>The syntax trees representing the parsed and processed code</returns>
        /// <exception cref="ArgumentNullException">The file paths are null</exception>
        public SyntaxTree[] ParseFiles(IEnumerable<string> cSharpFiles, IEnumerable<string> defineSymbols = null)
        {
            // Check for null
            if(cSharpFiles == null)
                throw new ArgumentNullException(nameof(cSharpFiles));

            // Parse all
            SyntaxTree[] syntaxTrees = cSharpFiles
                .Where(f => string.IsNullOrEmpty(f) == false)
                .Select(f => ParseFileTree(f, defineSymbols))
                .ToArray();

            // Run processor
            return RunParserProcessor(syntaxTrees);
        }

        /// <summary>
        /// Run parser processors on the specified C# syntax tree.
        /// </summary>
        /// <param name="syntaxTree">The syntax tree to process</param>
        /// <returns>The processed syntax trees</returns>
        /// <exception cref="ArgumentNullException">Input syntax tree is null</exception>
        public SyntaxTree[] ProcessSyntaxTree(SyntaxTree syntaxTree)
        {
            // Check for null
            if(syntaxTree == null)
                throw new ArgumentNullException(nameof(syntaxTree));

            // Run processor
            return RunParserProcessor(syntaxTree);
        }

        /// <summary>
        /// Run parser processors on the specified C# syntax trees.
        /// </summary>
        /// <param name="syntaxTrees">The syntax trees to process</param>
        /// <returns>The processed syntax trees</returns>
        /// <exception cref="ArgumentNullException">Input syntax trees is null</exception>
        public SyntaxTree[] ProcessSyntaxTrees(SyntaxTree[] syntaxTrees)
        {
            // Check for null
            if(syntaxTrees == null)
                throw new ArgumentNullException(nameof(syntaxTrees));

            // Run processor and create duplicate array to avoid modifying the source
            return RunParserProcessor(syntaxTrees);
        }

        private SyntaxTree ParseSourceTree(string cSharpSource, IEnumerable<string> defineSymbols)
        {
            // Parse the tree
            return CSharpSyntaxTree.ParseText(cSharpSource,
                flags.GetParseOptions(defineSymbols),
                "source", Encoding.Default);
        }

        private SyntaxTree ParseFileTree(string cSharpFile, IEnumerable<string> defineSymbols)
        {
            // Create text reader
            using (StreamReader sourceReader = File.OpenText(cSharpFile)) 
            {
                // Create the file source
                SourceText fileSource = SourceText.From(sourceReader, (int)sourceReader.BaseStream.Length, Encoding.Default);
                
                // Parse the tree
                return CSharpSyntaxTree.ParseText(fileSource,
                    flags.GetParseOptions(defineSymbols), 
                    cSharpFile);
            }
        }
        #endregion

        #region Compile
        /// <summary>
        /// Compile the specified syntax tree with the provided references.
        /// </summary>
        /// <param name="syntaxTree">The syntax tree to compile</param>
        /// <param name="references">The optional references to compile with</param>
        /// <returns>The result of the compile request</returns>
        public CompileResult CompileSyntaxTree(SyntaxTree syntaxTree, IEnumerable<ICompilationReference> references)
        {
            return CompileSyntaxTrees(new[] { syntaxTree }, references);
        }

        /// <summary>
        /// Compile the specified syntax trees as a batch with the provided references.
        /// </summary>
        /// <param name="syntaxTrees">The syntax trees to compile</param>
        /// <param name="references">The optional references to compile with</param>
        /// <returns>The result of the compile request</returns>
        public CompileResult CompileSyntaxTrees(SyntaxTree[] syntaxTrees, IEnumerable<ICompilationReference> references)
        {
            string outputPath = null;

            // Output info
            string outputName = flags.OutputName;

            // Generate new name
            if (string.IsNullOrEmpty(outputName) == true)
                outputName = Guid.NewGuid().ToString();

#if (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS) && UNITY_EDITOR == false
            if (flags.GenerateInMemory == false)
            {
                Debug.LogWarning("GenerateInMemory was disabled but it is required on this platform! Enabling GenerateInMemory...");
                flags.GenerateInMemory = true;
            }
#endif

            // Check for in memory
            if (flags.GenerateInMemory == false)
            {
                // Get extra info
                string outputDirectory = flags.OutputDirectory;
                string outputExtension = flags.GetOutputExtension();

                // Build full path
                outputPath = outputDirectory != null ? outputDirectory : string.Empty;

                // Check for override file name
                string actualOutputName = string.IsNullOrEmpty(flags.OutputFileName) == false
                    ? flags.OutputFileName
                    : outputName;

                // Append name
                outputPath = Path.Combine(outputPath, Path.ChangeExtension(actualOutputName, outputExtension));
            }

            // Create compilation object
            Compilation compileObject = CSharpCompilation.Create(outputName, syntaxTrees, 
                references.Select(r => r.CompilationReference), 
                flags.GetCompileOptions());

            // Create parent directory if required
            if(flags.GenerateInMemory == false)
            {
                // Get the parent folder
                string parentFolder = Directory.GetParent(outputPath).FullName;

                // Create if required
                if(string.IsNullOrEmpty(parentFolder) == false && Directory.Exists(parentFolder) == false)
                    Directory.CreateDirectory(parentFolder);

                // Get debug symbols path
                string debugSymbolPath = flags.GenerateSymbols == true
                    ? Path.ChangeExtension(outputPath, ".pdb")
                    : null;

                // Get documentation path
                string documentationPath = flags.GenerateDocumentation == true
                    ? Path.ChangeExtension(outputPath, ".xml")
                    : null;

                // Create file streams
                using (Stream assemblyStream = File.Create(outputPath))
                using (Stream debugSymbolStream = debugSymbolPath != null ? File.Create(debugSymbolPath) : null)
                using (Stream documentationStream = documentationPath != null ? File.Create(documentationPath) : null)
                {
                    // Produce the compilation
                    return EmitCompilation(compileObject, assemblyStream, debugSymbolStream, documentationStream);
                }
            }
            else
            {
                // Create memory streams
                using (Stream assemblyStream = new MemoryStream())
                using (Stream debugSymbolStream = flags.GenerateSymbols == true && flags.DebugFormat != DebugInformationFormat.Embedded ? new MemoryStream() : null)
                using (Stream documentationStream = flags.GenerateDocumentation == true ? new MemoryStream() : null)
                {
                    // Produce the compilation
                    return EmitCompilation(compileObject, assemblyStream, debugSymbolStream, documentationStream);
                }
            }
        }

        private CompileResult EmitCompilation(Compilation compileObject, Stream assemblyStream, Stream debugSymbolStream, Stream documentationStream)
        {
            // Create emit options
            EmitOptions emitOptions = new(false, flags.DebugFormat);

            // Emit the compilation
            EmitResult emitResult = compileObject.Emit(
                assemblyStream, 
                debugSymbolStream, 
                documentationStream,
                null, null, 
                emitOptions);

            // Create the compilation assembly
            AssemblySource compiledAssembly = null;

            // Check for success
            if (emitResult.Success == true)
            {
                // Get debug path
                string debugPath = debugSymbolStream is FileStream fs
                    ? fs.Name
                    : null;

                // Get debug image
                byte[] debugImage = debugSymbolStream is MemoryStream ms
                    ? ms.ToArray()
                    : null;

                // Get the compiled assembly
                compiledAssembly = flags.GenerateInMemory == false
                    ? AssemblySource.FromFile(((FileStream)assemblyStream).Name, debugPath)
                    : AssemblySource.FromMemory(((MemoryStream)assemblyStream).ToArray(), debugImage);

                // Run processor
                compiledAssembly = RunCompilationProcessor(compiledAssembly);
            }

            // Create the result
            return new CompileResult(compiledAssembly, emitResult.Diagnostics);
        }
#endregion

        private SyntaxTree[] RunParserProcessor(SyntaxTree syntaxTree)
        {
            // Create array
            return RunParserProcessor(new[] { syntaxTree });
        }

        private SyntaxTree[] RunParserProcessor(SyntaxTree[] syntaxTrees)
        {
            // Check for any
            if (parserProcessors == null)
                return syntaxTrees;

            // Run all processors in order of priority
            foreach (IParserProcessor parserProcessor in parserProcessors.OrderBy(p => p.Priority))
            {
                // Run the processor
                try
                {
                    // Run processor
                    SyntaxTree[] result = parserProcessor.OnPostProcess(syntaxTrees);

                    // Check for any
                    if (result != null)
                    {
                        syntaxTrees = result;
                        continue;
                    }

                    // Report error
                    Debug.LogErrorFormat("Parser processor '{0}' returned a null syntax tree: Using the compiler parsed syntax tree instead", parserProcessor);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Debug.LogErrorFormat("Parser processor '{0}' threw an exception: Using the compiler parsed syntax tree instead", parserProcessor);
                }
            }
            return syntaxTrees;
        }

        private AssemblySource RunCompilationProcessor(AssemblySource compilationAssembly)
        {
            // Check for any
            if (compilationProcessors == null)
                return compilationAssembly;

            // Run all processors in order of priority
            foreach (ICompilationProcessor compilationProcessor in compilationProcessors.OrderBy(p => p.Priority))
            {
                // Run the processor
                try
                {
                    // Run processor
                    AssemblySource result = compilationProcessor.OnPostProcess(compilationAssembly);

                    // Check for any
                    if (result != null)
                    {
                        compilationAssembly = result;
                        continue;
                    }

                    // Report error
                    Debug.LogErrorFormat("Compilation processor '{0}' returned a null assembly: Using the compiler emitted assembly instead", compilationProcessor);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Debug.LogErrorFormat("Compilation processor '{0}' threw an exception: Using the compiler emitted assembly instead", compilationProcessor);
                }
            }
            return compilationAssembly;
        }
    }
}

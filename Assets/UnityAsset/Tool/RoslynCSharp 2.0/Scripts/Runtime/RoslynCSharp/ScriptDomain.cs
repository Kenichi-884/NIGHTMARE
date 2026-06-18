using Microsoft.CodeAnalysis;
using RoslynCSharp.Implementation;
using RoslynCSharp.Project;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Trivial.CodeSecurity;
using UnityEngine;
using SecurityAssemblySource = Trivial.CodeSecurity.AssemblySource;

namespace RoslynCSharp
{
    /// <summary>
    /// The security mode used when loading and compiling assemblies.
    /// </summary>
    public enum ScriptSecurityMode
    {
        /// <summary>
        /// Use the security mode from the Roslyn C# settings window.
        /// </summary>
        UseSettings,
        /// <summary>
        /// Security verification will be skipped.
        /// </summary>
        EnsureLoad,
        /// <summary>
        /// Security verification will be used.
        /// </summary>
        EnsureSecurity,
    }

    /// <summary>
    /// A script domain stores a collection of assemblies that have been loaded or compiled, and provides API's for loading and compiling from a variety of input sources.
    /// Note that a domain is used mainly for organization purposes and it is possible to have multiple script domains per project if required. 
    /// This does not represent an app domain although it provides access to the current domain via <see cref="AppDomain"/> property. Support for separate app domains is not currently supported/feasible with Unity/Mono.
    /// </summary>
    public sealed class ScriptDomain : IDisposable
    {
        // Type
        private struct AsyncCompileState<T>
        {
            // Public
            public CompileResult CompileResult;
            public CodeSecurityReport SecurityReport;
            public T Result;

            // Constructor
            public AsyncCompileState(CompileResult compileResult, T result)
            {
                this.CompileResult = compileResult;
                this.SecurityReport = null;
                this.Result = result;
            }

            public AsyncCompileState(CompileResult compileResult, CodeSecurityReport securityReport, T result)
            {
                this.CompileResult = compileResult;
                this.SecurityReport = securityReport;
                this.Result = result;
            }
        }

        // Private
        private AppDomain appDomain;
        private readonly bool isAppDomainOwned = false;
        private readonly IAssemblyLoader assemblyLoader;
        private readonly IAssemblyContext assemblyContext;
        private readonly List<ScriptAssembly> loadedAssemblies = new();

        // Public
        /// <summary>
        /// Used to check whether the current backend is IL2CPP or not.
        /// If false then we can assume Mono-Jit backend.
        /// </summary>
        // Note - don't use const here because it can lead to unreachable code warnings
#if ENABLE_IL2CPP
        public static readonly bool IsIL2CPPBackend = true;
#else
        public static readonly bool IsIL2CPPBackend = false;
#endif

        // Properties
        /// <summary>
        /// Get the app domain associated with this script domain.
        /// Currently just returns the Unity created domain.
        /// </summary>
        public AppDomain AppDomain
        {
            get
            {
                CheckDisposed();
                return appDomain;
            }
        }

        /// <summary>
        /// Get all <see cref="ScriptAssembly"/> that are currently loaded into this domain.
        /// This will include any assembly that was loaded or compiled from this domain.
        /// </summary>
        public ScriptAssembly[] Assemblies
        {
            get
            {
                CheckDisposed();
                return loadedAssemblies.ToArray();
            }
        }

        /// <summary>
        /// Get only <see cref="ScriptAssembly"/> that have been compiled into this domain.
        /// </summary>
        public ScriptAssembly[] CompiledAssemblies
        {
            get
            {
                CheckDisposed();
                return EnumerateCompiledAssemblies.ToArray();
            }
        }

        /// <summary>
        /// Enumerate all <see cref="ScriptAssembly"/> that are currently loaded into this domain.
        /// This will include any assembly that was loaded or compiled from this domain.
        /// </summary>
        public IEnumerable<ScriptAssembly> EnumerateAssemblies
        {
            get
            {
                CheckDisposed();
                return loadedAssemblies;
            }
        }

        /// <summary>
        /// Enumerate only <see cref="ScriptAssembly"/> that have been compiled into this domain.
        /// </summary>
        public IEnumerable<ScriptAssembly> EnumerateCompiledAssemblies
        {
            get
            {
                CheckDisposed();
                return loadedAssemblies.Where(a => a.IsRuntimeCompiled);
            }
        }

        /// <summary>
        /// Get the default assembly loader for this script domain.
        /// Only usable on Mono/Jit platforms.
        /// </summary>
        public IAssemblyLoader AssemblyLoader
        {
            get
            {
                CheckDisposed();
                return assemblyLoader;
            }
        }

        /// <summary>
        /// Get the default assembly context for this script domain.
        /// Only usable on Mono/Jit platforms.
        /// </summary>
        public IAssemblyContext AssemblyContext
        {
            get
            {
                CheckDisposed();
                return assemblyContext;
            }
        }

        /// <summary>
        /// Check if this domain has been disposed.
        /// </summary>
        public bool IsDisposed => appDomain == null;

        // Constructor
        /// <summary>
        /// Create a new script domain instance, used to make load or compile requests.
        /// </summary>
        /// <param name="sandbox">The optional app domain used to load compiled code. Requires all dependencies to be loaded ahead of time</param>
        /// <param name="keepDomainAlive">Should the provided app domain be unloaded when this object is disposed, or should it be kept alive</param>
        public ScriptDomain(AppDomain sandbox = null, bool keepDomainAlive = false)
        {
            this.appDomain = (sandbox == null || sandbox == AppDomain.CurrentDomain)
                ? AppDomain.CurrentDomain
                : sandbox;
            this.isAppDomainOwned = sandbox != null 
                && sandbox != AppDomain.CurrentDomain
                && keepDomainAlive == false;

            // Create default loader
            this.assemblyLoader = new DefaultAssemblyLoader(appDomain);

            // Create default context
            this.assemblyContext = new DefaultAssemblyContext();
        }

        // Methods
        /// <summary>
        /// Dispose this domain.
        /// Note that this will just clear all stored assemblies and will not actually unload the Unity app domain.
        /// </summary>
        public void Dispose()
        {
            // Unload if owned
            if(isAppDomainOwned == true)
            {
                AppDomain.Unload(appDomain);
            }

            // Clear domain
            appDomain = null;

            // Clear loaded
            lock (loadedAssemblies)
            {
                loadedAssemblies.Clear();
            }
        }

        // Load assembly
        #region LoadAssembly
        /// <summary>
        /// Load a managed assembly from a text asset using the Unity resources API.
        /// </summary>
        /// <param name="assemblyResourcesPath">The resources path where the assembly text asset is located</param>
        /// <param name="debugSymbolsResourcesPath">The optional resources path of the debug symbols file if applicable (.pdb usually)</param>
        /// <param name="securityMode">The security mode to use when loading the assembly</param>
        /// <param name="mainTypeSelector">The optional main type selector used to determine which type in the assembly is considered to be the main type</param>
        /// <returns>The loaded assembly or null if an error occurred</returns>
        /// <exception cref="ArgumentException">Assembly resources path is null or empty</exception>
        /// <exception cref="DllNotFoundException">Could not find the text asset or load the dll at the specified path</exception>
        public ScriptAssembly LoadAssemblyFromResources(string assemblyResourcesPath, string debugSymbolsResourcesPath = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings, IMainTypeSelector mainTypeSelector = null, IAssemblyContext assemblyContext = null)
        {
            // Check disposed
            CheckDisposed();

            // Check arg
            if (string.IsNullOrEmpty(assemblyResourcesPath) == true)
                throw new ArgumentException("Assembly resources path cannot be null or empty");

            // Load assembly asset
            TextAsset assemblyAsset = Resources.Load<TextAsset>(assemblyResourcesPath);

            // Check for load error
            if (assemblyAsset == null)
                throw new DllNotFoundException("Could not load assembly from resources: " + assemblyResourcesPath);

            // Load debug symbols asset
            TextAsset debugSymbolsAsset = string.IsNullOrEmpty(debugSymbolsResourcesPath) == false
                ? Resources.Load<TextAsset>(debugSymbolsResourcesPath)
                : null;

            // Load the assembly
            return LoadAssembly(assemblyAsset.bytes, debugSymbolsAsset != null ? debugSymbolsAsset.bytes : null, securityMode, mainTypeSelector, assemblyContext);
        }

        /// <summary>
        /// Load a managed assembly from the specified path.
        /// </summary>
        /// <param name="assemblyPath">The path of the assembly to load</param>
        /// <param name="securityMode">The security mode used to load the assembly</param>
        /// <param name="mainTypeSelector">The optional main type selector used to determine which type in the assembly is considered to be the main type</param>
        /// <returns>The loaded assembly or null if an error occurred</returns>
        /// <exception cref="ArgumentException">The assembly path is null or empty</exception>
        public ScriptAssembly LoadAssembly(string assemblyPath, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings, IMainTypeSelector mainTypeSelector = null, IAssemblyContext assemblyContext = null)
        {
            // Check disposed
            CheckDisposed();

            // Create assembly source
            AssemblySource source = AssemblySource.FromFile(assemblyPath, Path.ChangeExtension(assemblyPath, ".pdb"));
            
            // Load the assembly
            return RegisterAssembly(source, null, out _, securityMode, mainTypeSelector, assemblyContext);
        }

        /// <summary>
        /// Load a managed assembly image from the provided bytes that should represent a value PE assembly image.
        /// </summary>
        /// <param name="assemblyImage">The assembly image bytes</param>
        /// <param name="debugSymbolsImage">The optional debug symbols image (usually contents of .pdb file)</param>
        /// <param name="securityMode">The security mode used to load the assembly</param>
        /// <param name="mainTypeSelector">The optional main type selector used to determine which type in the assembly is considered to be the main type</param>
        /// <returns>The loaded assembly or null if an error occurred</returns>
        /// <exception cref="ArgumentNullException">Assembly image is null</exception>
        public ScriptAssembly LoadAssembly(byte[] assemblyImage, byte[] debugSymbolsImage = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings, IMainTypeSelector mainTypeSelector = null, IAssemblyContext assemblyContext = null)
        {
            // Check disposed
            CheckDisposed();

            // Create assembly source
            AssemblySource source = AssemblySource.FromMemory(assemblyImage, debugSymbolsImage);

            // Load the assembly
            return RegisterAssembly(source, null, out _, securityMode, mainTypeSelector, assemblyContext);
        }
        #endregion

        #region LoadMainAssembly
        /// <summary>
        /// Load a managed assembly from a text asset using the Unity resources API.
        /// </summary>
        /// <param name="assemblyResourcesPath">The resources path where the assembly text asset is located</param>
        /// <param name="debugSymbolsResourcesPath">The optional resources path of the debug symbols file if applicable (.pdb usually)</param>
        /// <param name="securityMode">The security mode to use when loading the assembly</param>
        /// <param name="mainTypeSelector">The optional main type selector used to determine which type in the assembly is considered to be the main type</param>
        /// <returns>The loaded main type for the compiled assembly or null if an error occurred</returns>
        /// <exception cref="ArgumentException">Assembly resources path is null or empty</exception>
        /// <exception cref="DllNotFoundException">Could not find the text asset or load the dll at the specified path</exception>
        public ScriptType LoadMainAssemblyFromResources(string assemblyResourcesPath, string debugSymbolsResourcesPath = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings, IMainTypeSelector mainTypeSelector = null, IAssemblyContext assemblyContext = null)
            => LoadAssemblyFromResources(assemblyResourcesPath, debugSymbolsResourcesPath, securityMode, mainTypeSelector, assemblyContext)?.MainType;

        /// <summary>
        /// Load a managed assembly from the specified path.
        /// </summary>
        /// <param name="assemblyPath">The path of the assembly to load</param>
        /// <param name="securityMode">The security mode used to load the assembly</param>
        /// <param name="mainTypeSelector">The optional main type selector used to determine which type in the assembly is considered to be the main type</param>
        /// <returns>The loaded main type for the compiled assembly or null if an error occurred</returns>
        /// <exception cref="ArgumentException">The assembly path is null or empty</exception>
        public ScriptType LoadMainAssembly(string assemblyPath, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings, IMainTypeSelector mainTypeSelector = null, IAssemblyContext assemblyContext = null)
            => LoadAssembly(assemblyPath, securityMode, mainTypeSelector, assemblyContext)?.MainType;

        /// <summary>
        /// Load a managed assembly image from the provided bytes that should represent a value PE assembly image.
        /// </summary>
        /// <param name="assemblyImage">The assembly image bytes</param>
        /// <param name="debugSymbolsImage">The optional debug symbols image (usually contents of .pdb file)</param>
        /// <param name="securityMode">The security mode used to load the assembly</param>
        /// <param name="mainTypeSelector">The optional main type selector used to determine which type in the assembly is considered to be the main type</param>
        /// <returns>The loaded main type for the compiled assembly or null if an error occurred</returns>
        /// <exception cref="ArgumentNullException">Assembly image is null</exception>
        public ScriptType LoadMainAssembly(byte[] assemblyImage, byte[] debugSymbolsImage = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings, IMainTypeSelector mainTypeSelector = null, IAssemblyContext assemblyContext = null)
            => LoadAssembly(assemblyImage, debugSymbolsImage, securityMode, mainTypeSelector, assemblyContext)?.MainType;
        #endregion



        // Compile only
        #region CompileSource
        /// <summary>
        /// Compile the specified C# source code string.
        /// Use <see cref="CompileSources(string[], CompileOptions)"/> if you need to compile multiple strings as a batch.
        /// </summary>
        /// <param name="cSharpSource">The C# source code string to compile</param>
        /// <param name="options">The options for the compile request. Use <see cref="CompileOptions.FromSettings(LogLevel, IEnumerable{string}, IEnumerable{ICompilationReference})"/> to create options from settings asset</param>
        /// <returns>The <see cref="CompileResult"/> for the compile request</returns>
        /// <exception cref="ArgumentException">Source string is null or empty</exception>
        /// <exception cref="ArgumentNullException">Compile options are null</exception>
        public CompileResult CompileSource(string cSharpSource, CompileOptions options = null)
        {
            // Check for disposed
            CheckDisposed();

            // Check source
            if (string.IsNullOrEmpty(cSharpSource) == true)
                throw new ArgumentException("Source cannot be null or empty");

            // Get default options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Create compilation request
            CompileRequest request = new(options,
                options.ParserProcessors, options.CompilationProcessors);

            // Parse sources
            SyntaxTree[] syntaxTrees = request.ParseSource(cSharpSource, options.GetDefines());

            // Compile
            CompileResult result = request.CompileSyntaxTrees(syntaxTrees, options.GetReferences());

            // Report compilation log
            result.ReportCompilationErrors(options.CompilerLogLevel);

            return result;
        }

        /// <summary>
        /// Compile the specified C# source code strings as a batch.
        /// </summary>
        /// <param name="cSharpSources">An array of C# source code strings to compile</param>
        /// <param name="options">The options for the compile request. Use <see cref="CompileOptions.FromSettings(LogLevel, IEnumerable{string}, IEnumerable{ICompilationReference})"/> to create options from settings asset</param>
        /// <returns>The <see cref="CompileResult"/> for the compile request</returns>
        /// <exception cref="ArgumentNullException">Source code array is null</exception>
        /// <exception cref="ArgumentException">Source code array has a length of zero</exception>
        public CompileResult CompileSources(string[] cSharpSources, CompileOptions options = null)
        {
            // Check for disposed
            CheckDisposed();

            // Check null
            if (cSharpSources == null)
                throw new ArgumentNullException(nameof(cSharpSources));

            // Get default options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Check for none
            if (cSharpSources.Length == 0)
                throw new ArgumentException("Sources cannot be empty");

            // Create compilation request
            CompileRequest request = new(options, 
                options.ParserProcessors, options.CompilationProcessors);

            // Parse sources
            SyntaxTree[] syntaxTrees = request.ParseSources(cSharpSources, options.GetDefines());

            // Compile
            CompileResult result = request.CompileSyntaxTrees(syntaxTrees, options.GetReferences());

            // Report compilation log
            result.ReportCompilationErrors(options.CompilerLogLevel);

            return result;
        }

        /// <summary>
        /// Compile the specified C# source code string asynchronously.
        /// </summary>
        /// <param name="cSharpSource">The C# source code string to compile</param>
        /// <param name="options">The options for the compile request. Use <see cref="CompileOptions.FromSettings(LogLevel, IEnumerable{string}, IEnumerable{ICompilationReference})"/> to create options from settings asset</param>
        /// <returns>An operation that can be awaited in a coroutine or async await context and provides access to the <see cref="CompileResult"/></returns>
        public CompileAsync CompileSourceAsync(string cSharpSource, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Run async
            return RunAsyncCompile(() =>
            {
                return CompileSource(cSharpSource, options);
            });
        }

        /// <summary>
        /// Compile the specified C# source code strings as a batch asynchronously.
        /// </summary>
        /// <param name="cSharpSources">An array of C# source code strings to compile</param>
        /// <param name="options">The options for the compile request. Use <see cref="CompileOptions.FromSettings(LogLevel, IEnumerable{string}, IEnumerable{ICompilationReference})"/> to create options from settings asset</param>
        /// <returns>An operation that can be awaited in a coroutine or async await context and provides access to the <see cref="CompileResult"/></returns>
        public CompileAsync CompileSourcesAsync(string[] cSharpSources, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Run async
            return RunAsyncCompile(() =>
            {
                return CompileSources(cSharpSources, options);
            });
        }
        #endregion

        #region CompileFile
        /// <summary>
        /// Compile the specified C# source file.
        /// Use <see cref="CompileFiles(string[], CompileOptions)"/> if you need to compile multiple file paths as a batch.
        /// </summary>
        /// <param name="cSharpFile">The C# file path to compile (.cs usually)</param>
        /// <param name="options">The options for the compile request. Use <see cref="CompileOptions.FromSettings(LogLevel, IEnumerable{string}, IEnumerable{ICompilationReference})"/> to create options from settings asset</param>
        /// <returns>The <see cref="CompileResult"/> for the compile request</returns>
        /// <exception cref="ArgumentException">File path is null or empty</exception>
        public CompileResult CompileFile(string cSharpFile, CompileOptions options = null)
        {
            // Check for disposed
            CheckDisposed();

            // Check source
            if (string.IsNullOrEmpty(cSharpFile) == true)
                throw new ArgumentException("File cannot be null or empty");

            // Get default options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Create compilation request
            CompileRequest request = new(options,
                options.ParserProcessors, options.CompilationProcessors);

            // Parse file
            SyntaxTree[] syntaxTrees = request.ParseFile(cSharpFile, options.GetDefines());

            // Compile
            CompileResult result = request.CompileSyntaxTrees(syntaxTrees, options.GetReferences());

            // Report compilation log
            result.ReportCompilationErrors(options.CompilerLogLevel);

            return result;
        }

        /// <summary>
        /// Compile the specified C# source files as a batch.
        /// </summary>
        /// <param name="cSharpFiles">The C# file paths to compile (.cs usually)</param>
        /// <param name="options">The options for the compile request. Use <see cref="CompileOptions.FromSettings(LogLevel, IEnumerable{string}, IEnumerable{ICompilationReference})"/> to create options from settings asset</param>
        /// <returns>The <see cref="CompileResult"/> for the compile request</returns>
        /// <exception cref="ArgumentNullException">File path array is null</exception>
        /// <exception cref="ArgumentException">File path array is empty</exception>
        public CompileResult CompileFiles(string[] cSharpFiles, CompileOptions options = null)
        {
            // Check for disposed
            CheckDisposed();

            // Check null
            if (cSharpFiles == null)
                throw new ArgumentNullException(nameof(cSharpFiles));

            // Get default options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Check for none
            if (cSharpFiles.Length == 0)
                throw new ArgumentException("Files cannot be empty");

            // Create compilation request
            CompileRequest request = new(options,
                options.ParserProcessors, options.CompilationProcessors);

            // Parse files
            SyntaxTree[] syntaxTrees = request.ParseFiles(cSharpFiles, options.GetDefines());

            // Compile
            CompileResult result = request.CompileSyntaxTrees(syntaxTrees, options.GetReferences());

            // Report compilation log
            result.ReportCompilationErrors(options.CompilerLogLevel);

            return result;
        }

        public CompileAsync CompileFileAsync(string cSharpFile, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompile(() =>
            {
                return CompileFile(cSharpFile, options);
            });
        }

        public CompileAsync CompileFilesAsync(string[] cSharpFiles, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompile(() =>
            {
                return CompileFiles(cSharpFiles, options);
            });
        }
        #endregion

        #region CompileSyntaxTree
        public CompileResult CompileSyntaxTree(SyntaxTree syntaxTree, CompileOptions options = null)
        {
            // Check for disposed
            CheckDisposed();

            // Check null
            if(syntaxTree == null) 
                throw new ArgumentNullException(nameof(syntaxTree));

            // Get default options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Create compilation request
            CompileRequest request = new(options,
                options.ParserProcessors, options.CompilationProcessors);

            // Process tree
            SyntaxTree[] processedSyntaxTrees = request.ProcessSyntaxTree(syntaxTree);

            // Compile
            CompileResult result = request.CompileSyntaxTrees(processedSyntaxTrees, options.GetReferences());

            // Report compilation log
            result.ReportCompilationErrors(options.CompilerLogLevel);

            return result;
        }

        public CompileResult CompileSyntaxTrees(SyntaxTree[] syntaxTrees, CompileOptions options = null)
        {
            // Check for disposed
            CheckDisposed();

            // Check null
            if (syntaxTrees == null)
                throw new ArgumentNullException(nameof(syntaxTrees));

            // Get default options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Check for none
            if (syntaxTrees.Length == 0)
                throw new ArgumentException("Files cannot be empty");

            // Create compilation request
            CompileRequest request = new(options,
                options.ParserProcessors, options.CompilationProcessors);

            // Process trees
            SyntaxTree[] processedSyntaxTrees = request.ProcessSyntaxTrees(syntaxTrees);

            // Compile
            CompileResult result = request.CompileSyntaxTrees(processedSyntaxTrees, options.GetReferences());

            // Report compilation log
            result?.ReportCompilationErrors(options.CompilerLogLevel);

            return result;
        }

        public CompileAsync CompileSyntaxTreeAsync(SyntaxTree syntaxTree, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompile(() =>
            {
                return CompileSyntaxTree(syntaxTree, options);
            });
        }

        public CompileAsync CompileSyntaxTreesAsync(SyntaxTree[] syntaxTrees, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompile(() =>
            {
                return CompileSyntaxTrees(syntaxTrees, options);
            });
        }
        #endregion

        #region CompileProject
        public CompileResult CompileProject(CSharpProject project, CompileOptions options = null)
        {
            // Check null
            if(project == null) 
                throw new ArgumentNullException(nameof(project));

            // Get options
            options = options != null
                ? new CompileOptions(options, project.DefineSymbols, project.GetAssemblyReferences())
                : CompileOptions.FromSettings(LogLevel.Errors, project.DefineSymbols, project.GetAssemblyReferences());

            // Update name
            if(string.IsNullOrEmpty(project.AssemblyName) == false)
                options.OutputName = project.AssemblyName;

            // Compile files
            return CompileFiles(project.Sources.ToArray(), options);
        }

        public CompileAsync CompileProjectAsync(CSharpProject project, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompile(() =>
            {
                return CompileProject(project, options);
            });
        }
        #endregion

        #region CompileFolder
        public CompileResult CompileDirectory(string directoryPath, SearchOption searchOption, CompileOptions options = null)
        {
            // Check arg
            if (string.IsNullOrEmpty(directoryPath) == true)
                throw new ArgumentException("Directory path cannot be null or empty");

            // Get all files in folder
            string[] files = Directory.GetFiles(directoryPath, "*.cs", searchOption);

            // Check for any
            if (files.Length == 0)
                throw new InvalidOperationException("Could not locate any .cs source files in the specified directory: " + directoryPath);

            // Compile the files
            return CompileFiles(files, options);
        }

        public CompileAsync CompileDirectoryAsync(string directoryPath, SearchOption searchOption, CompileOptions options = null)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompile(() =>
            {
                return CompileDirectory(directoryPath, searchOption, options);
            });
        }
        #endregion



        // Compile and load
        #region CompileAndLoadSource
        public ScriptAssembly CompileAndLoadSource(string cSharpSource, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSource(cSharpSource, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadSource(string cSharpSource, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileSource(cSharpSource, options);
            securityReport = null;

            // Check for success
            if (compileResult.Success == false)
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public ScriptAssembly CompileAndLoadSources(string[] cSharpSources, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSources(cSharpSources, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadSources(string[] cSharpSources, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileSources(cSharpSources, options);
            securityReport = null;

            // Check for success
            if (compileResult.Success == false)
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadSourceAsync(string cSharpSource, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSource(cSharpSource, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadSourcesAsync(string[] cSharpSources, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSources(cSharpSources, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }
        #endregion

        #region CompileAndLoadFile
        public ScriptAssembly CompileAndLoadFile(string cSharpFile, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadFile(cSharpFile, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadFile(string cSharpFile, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileFile(cSharpFile, options);
            securityReport = null;

            // Check for success
            if (compileResult.Success == false)
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult?.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public ScriptAssembly CompileAndLoadFiles(string[] cSharpFiles, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadFiles(cSharpFiles, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadFiles(string[] cSharpFiles, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileFiles(cSharpFiles, options);
            securityReport = null;

            // Check for success
            if(compileResult.Success == false) 
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult?.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadFileAsync(string cSharpFile, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileFile(cSharpFile, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadFilesAsync(string[] cSharpFiles, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileFiles(cSharpFiles, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }
        #endregion

        #region CompileAndLoadSyntaxTree
        public ScriptAssembly CompileAndLoadSyntaxTree(SyntaxTree syntaxTree, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSyntaxTree(syntaxTree, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadSyntaxTree(SyntaxTree syntaxTree, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileSyntaxTree(syntaxTree, options);
            securityReport = null;

            // Check for success
            if (compileResult.Success == false)
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public ScriptAssembly CompileAndLoadSyntaxTrees(SyntaxTree[] syntaxTrees, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSyntaxTrees(syntaxTrees, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadSyntaxTrees(SyntaxTree[] syntaxTrees, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileSyntaxTrees(syntaxTrees, options);
            securityReport = null;

            // Check for success
            if (compileResult.Success == false)
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadSyntaxTreeAsync(SyntaxTree syntaxTree, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSyntaxTree(syntaxTree, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadSyntaxTreesAsync(SyntaxTree[] syntaxTrees, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSyntaxTrees(syntaxTrees, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }
        #endregion

        #region CompileAndLoadProject
        public ScriptAssembly CompileAndLoadProject(CSharpProject project, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadProject(project, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadProject(CSharpProject project, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileProject(project, options);
            securityReport = null;

            // Check for success
            if (compileResult.Success == false)
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadProjectAsync(CSharpProject project, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileProject(project, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }
        #endregion

        #region CompileAndLoadFolder
        public ScriptAssembly CompileAndLoadDirectory(string directoryPath, SearchOption searchOption, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadDirectory(directoryPath, searchOption, out _, out _, options, securityMode);
        public ScriptAssembly CompileAndLoadDirectory(string directoryPath, SearchOption searchOption, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            // Compile request
            compileResult = CompileDirectory(directoryPath, searchOption, options);
            securityReport = null;

            // Check for success
            if(compileResult.Success == false) 
                return null;

            // Load the assembly
            return RegisterAssembly(compileResult.Assembly, compileResult, out securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);
        }

        public CompileAsync<ScriptAssembly> CompileAndLoadDirectoryAsync(string directoryPath, SearchOption searchOption, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileDirectory(directoryPath, searchOption, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptAssembly>(result, securityReport, assembly);
                }

                // Error occurred
                return new AsyncCompileState<ScriptAssembly>(result, null);
            });
        }
        #endregion



        // Compile and load main
        #region CompileAndLoadMainSource
        public ScriptType CompileAndLoadMainSource(string cSharpSource, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSource(cSharpSource, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainSource(string cSharpSource, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSource(cSharpSource, out compileResult, out securityReport, options, securityMode)?.MainType;

        public ScriptType CompileAndLoadMainSources(string[] cSharpSources, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSources(cSharpSources, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainSources(string[] cSharpSources, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSources(cSharpSources, out compileResult, out securityReport, options, securityMode)?.MainType;


        public CompileAsync<ScriptType> CompileAndLoadMainSourceAsync(string cSharpSource, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSource(cSharpSource, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptType>(result, securityReport, assembly.MainType);
                }

                // Error occurred
                return new AsyncCompileState<ScriptType>(result, null);
            });
        }

        public CompileAsync<ScriptType> CompileAndLoadMainSourcesAsync(string[] cSharpSources, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSources(cSharpSources, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptType>(result, securityReport, assembly.MainType);
                }

                // Error occurred
                return new AsyncCompileState<ScriptType>(result, null);
            });
        }
        #endregion

        #region CompileAndLoadMainFile
        public ScriptType CompileAndLoadMainFile(string cSharpFile, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadFile(cSharpFile, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainFile(string cSharpFile, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadFile(cSharpFile, out compileResult, out securityReport, options, securityMode)?.MainType;

        public ScriptType CompileAndLoadMainFiles(string[] cSharpFiles, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadFiles(cSharpFiles, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainFiles(string[] cSharpFiles, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadFiles(cSharpFiles, out compileResult, out securityReport, options, securityMode)?.MainType;


        public CompileAsync<ScriptType> CompileAndLoadMainFileAsync(string cSharpFile, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileFile(cSharpFile, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptType>(result, securityReport, assembly.MainType);
                }

                // Error occurred
                return new AsyncCompileState<ScriptType>(result, null);
            });
        }

        public CompileAsync<ScriptType> CompileAndLoadMainFilesAsync(string[] cSharpFiles, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileFiles(cSharpFiles, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptType>(result, securityReport, assembly.MainType);
                }

                // Error occurred
                return new AsyncCompileState<ScriptType>(result, null);
            });
        }
        #endregion

        #region CompileAndLoadMainSyntaxTree
        public ScriptType CompileAndLoadMainSyntaxTree(SyntaxTree syntaxTree, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSyntaxTree(syntaxTree, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainSyntaxTree(SyntaxTree syntaxTree, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSyntaxTree(syntaxTree, out compileResult, out securityReport, options, securityMode)?.MainType;

        public ScriptType CompileAndLoadMainSyntaxTrees(SyntaxTree[] syntaxTrees, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSyntaxTrees(syntaxTrees, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainSyntaxTrees(SyntaxTree[] syntaxTrees, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadSyntaxTrees(syntaxTrees, out compileResult, out securityReport, options, securityMode)?.MainType;


        public CompileAsync<ScriptType> CompileAndLoadMainSyntaxTreeAsync(SyntaxTree syntaxTree, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSyntaxTree(syntaxTree, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptType>(result, securityReport, assembly.MainType);
                }

                // Error occurred
                return new AsyncCompileState<ScriptType>(result, null);
            });
        }

        public CompileAsync<ScriptType> CompileAndLoadMainSyntaxTreesAsync(SyntaxTree[] syntaxTrees, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileSyntaxTrees(syntaxTrees, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptType>(result, securityReport, assembly.MainType);
                }

                // Error occurred
                return new AsyncCompileState<ScriptType>(result, null);
            });
        }
        #endregion

        #region CompileAndLoadMainProject
        public ScriptType CompileAndLoadMainProject(CSharpProject project, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadProject(project, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainProject(CSharpProject project, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadProject(project, out compileResult, out securityReport, options, securityMode)?.MainType;
        #endregion

        #region CompileAndLoadMainFolder
        public ScriptType CompileAndLoadMainDirectory(string directoryPath, SearchOption searchOption, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadDirectory(directoryPath, searchOption, options, securityMode)?.MainType;
        public ScriptType CompileAndLoadMainDirectory(string directoryPath, SearchOption searchOption, out CompileResult compileResult, out CodeSecurityReport securityReport, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
            => CompileAndLoadDirectory(directoryPath, searchOption, out compileResult, out securityReport, options, securityMode)?.MainType;


        public CompileAsync<ScriptType> CompileAndLoadMainDirectoryAsync(string directoryPath, SearchOption searchOption, CompileOptions options = null, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            // Check options
            if (options == null)
                options = CompileOptions.FromSettings();

            return RunAsyncCompileAndLoad(() =>
            {
                CompileResult result = CompileDirectory(directoryPath, searchOption, options);

                // Check for success
                if (result.Success == true)
                {
                    // Validate security
                    ScriptAssembly assembly = RegisterAssembly(result.Assembly, result, out CodeSecurityReport securityReport, securityMode, options.MainTypeSelector, options.AssemblyContext);

                    // Create result
                    return new AsyncCompileState<ScriptType>(result, securityReport, assembly.MainType);
                }

                // Error occurred
                return new AsyncCompileState<ScriptType>(result, null);
            });
        }
        #endregion



        // Security check
        #region SecurityCheck
        /// <summary>
        /// Run code security checks on the specified <see cref="AssemblySource"/> to determine whether it passes code security validation.
        /// Note that security checks will be skipped if <see cref="ScriptSecurityMode.EnsureLoad"/> is passed.
        /// </summary>
        /// <param name="source">The assembly source to check</param>
        /// <param name="securityMode">The security mode used for the check</param>
        /// <param name="securityReport">The generated code security report if security validation did run</param>
        /// <param name="securityValidator">An optional <see cref="ICodeSecurityValidator"/> to use in place of the default code validation rules</param>
        /// <returns>True if the assembly passed code security verification or false if not</returns>
        public bool SecurityCheckAssembly(AssemblySource source, ScriptSecurityMode securityMode, out CodeSecurityReport securityReport, ICodeSecurityValidator securityValidator = null)
        {
            // Check for perform security check
            bool performSecurityCheck = securityMode == ScriptSecurityMode.EnsureSecurity;

            // Check for settings
            if (securityMode == ScriptSecurityMode.UseSettings)
                performSecurityCheck = RoslynCSharpSettings.UserSettings.SecurityCheckCode;

            // Check for security
            if(performSecurityCheck == true)
            {
                // Create the source
                using (SecurityAssemblySource securitySource = source.GetSecuritySource())
                {
                    // Get the validator
                    ICodeSecurityValidator validator = securityValidator == null
                        ? RoslynCSharpSettings.UserSettings.CodeSecurityRestrictions
                        : securityValidator;

                    // Run code security verification
                    bool success = securitySource.SecurityCheckAssembly(validator, out securityReport);

                    // Check for successful
                    if (success == false)
                    {
                        Debug.LogError(securityReport.GetSummaryText());
                        Debug.LogError(securityReport.GetAllText(true));

                        // Report all messages
                        //foreach(string securityMessage in securityReport.GetAllLines(true))
                        //    Debug.LogError(securityMessage);
                    }
                    else
                    {
                        // Write summary
                        Debug.Log(securityReport.GetSummaryText());
                    }

                    return success;
                }
            }

            // Security checks not required
            securityReport = null;
            return true;
        }
        #endregion

        public ScriptAssembly RegisterAssembly(ScriptAssembly asm)
        {
            // Check for null
            if (asm == null)
                throw new ArgumentNullException(nameof(asm));

            // Add to loaded
            lock (loadedAssemblies)
            {
                loadedAssemblies.Add(asm);
            }
            return asm;
        }

        private ScriptAssembly RegisterAssembly(AssemblySource source, CompileResult compileResult, out CodeSecurityReport securityReport, ScriptSecurityMode securityMode, IMainTypeSelector mainTypeSelector, IAssemblyContext assemblyContext)
        {
            // Run security checks
            if (SecurityCheckAssembly(source, securityMode, out securityReport) == false)
                return null;

            // Check for no context and use default
            if (assemblyContext == null)
                assemblyContext = this.AssemblyContext;

            // Create the assembly
            ScriptAssembly asm = assemblyContext.RegisterAssembly(this, source, compileResult, securityReport, securityMode, mainTypeSelector);

            // Now we can add to the security settings for referencing purposes
            if (securityMode == ScriptSecurityMode.UseSettings && RoslynCSharpSettings.UserSettings.WhitelistCompiledAndLoadedAssemblies == true)
                asm.AddToSecurityWhitelist();

            // Register assembly
            return RegisterAssembly(asm);
        }

        private CompileAsync RunAsyncCompile(Func<CompileResult> call)
        {
            // Check platform
#if UNITY_WEBGL && UNITY_EDITOR == false
            throw new NotSupportedException("Async compile requests are not supported on this platform. Please use the non-async equivalent!");
#else

            // Create async
            CompileAsync async = new();

            // Run async
            Task.Run(() =>
            {
                try
                {
                    // Run async
                    CompileResult result = call();

                    // Check for error
                    if (result.Success == true)
                    {
                        // Complete operation
                        async.Complete(true, result);
                    }
                    else
                    {
                        // Fail operation
                        async.Error("Compile request failed", result);
                    }
                }
                catch(Exception e)
                {
                    // Report exception
                    async.Error(e.ToString(), null);
                }
            });
            return async;
#endif
        }

        private CompileAsync<T> RunAsyncCompileAndLoad<T>(Func<AsyncCompileState<T>> call)
        {
            // Check platform
#if UNITY_WEBGL && UNITY_EDITOR == false
            throw new NotSupportedException("Async compile requests are not supported on this platform. Please use the non-async equivalent!");
#else

            // Create async
            CompileAsync<T> async = new();

            // Run async
            Task.Run(() =>
            {
                try
                {
                    // Run async
                    AsyncCompileState<T> result = call();

                    // Check for error
                    if (result.CompileResult.Success == true)
                    {
                        // Complete operation
                        async.Complete(true, result.CompileResult, result.SecurityReport, result.Result);
                    }
                    else
                    {
                        // Fail operation
                        async.Error("Compile request failed", result.CompileResult, result.SecurityReport);
                    }
                }
                catch (Exception e)
                {
                    // Report exception
                    async.Error(e.ToString(), null, null);
                }
            });
            return async;
#endif
        }

        private void CheckDisposed()
        {
            // Check for disposed
            if (appDomain == null)
                throw new ObjectDisposedException(nameof(ScriptDomain));
        }
    }
}

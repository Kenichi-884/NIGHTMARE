using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a set of compiler flags to change the compilation and output behaviour of a compile request.
    /// </summary>
    [Serializable]
    public class CompileFlags
    {
        // Properties
        /// <summary>
        /// Get the default compiler flags.
        /// Flags are mutable so always create a new instance.
        /// </summary>
        public static CompileFlags Default => new();

        /// <summary>
        /// The optional directory where the compiled assembly will be generated if <see cref="GenerateInMemory"/> is disabled.
        /// If the directory is null or empty then the compiled assembly will be generated in the current executing directory (Project folder in case of Unity editor, and AppFolder in case of standalone).
        /// </summary>
        public string OutputDirectory { get; set; } = null;
        /// <summary>
        /// The optional name of the output assembly.
        /// If the name is null or empty then a unique name will be generated using a guid format.
        /// </summary>
        public string OutputName { get; set; } = null;
        /// <summary>
        /// The override name of the output file, if <see cref="GenerateInMemory"/> is disabled.
        /// If this value is null, then <see cref="OutputName"/> will be used for the file name also.
        /// </summary>
        public string OutputFileName { get; set; } = null;
        /// <summary>
        /// Should unsafe code be allowed to compile.
        /// </summary>
        [field: SerializeField]
        public bool AllowUnsafe { get; set; } = false;
        /// <summary>
        /// Should the compiled assembly be optimized for best performance.
        /// </summary>
        [field: SerializeField]
        public bool AllowOptimize { get; set; } = false;
        /// <summary>
        /// Can the compilation request use multiple threads if available.
        /// </summary>
        [field: SerializeField]
        public bool AllowConcurrentCompile { get; set; } = true;
        /// <summary>
        /// Should the compiler produce identical results for the same input.
        /// </summary>
        public bool Deterministic { get; set; } = true;
        /// <summary>
        /// Should the compiler generate the compiled assembly in memory or on disk.
        /// Note that some platforms only support compile in memory, and for those platforms this option will be ignored.
        /// </summary>
        [field: SerializeField]
        public bool GenerateInMemory { get; set; } = true;
        /// <summary>
        /// Should the compiler generate portable PDB debug symbols.
        /// </summary>
        [field: SerializeField]
        public bool GenerateSymbols { get; set; } = true;
        /// <summary>
        /// Should the compiler generate an XML documentation file.
        /// </summary>
        [field: SerializeField]
        public bool GenerateDocumentation { get; set; } = false;
        /// <summary>
        /// The warning level used by the compiler.
        /// </summary>
        [field: SerializeField]
        public int WarningLevel { get; set; } = 4;
        /// <summary>
        /// The C# language version to target.
        /// </summary>
        [field: SerializeField]
        public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Default;
        /// <summary>
        /// The output kind to produce from the compilation.
        /// Should always be .dll expect for very specific cases.
        /// </summary>
        [field: SerializeField]
        public OutputKind OutputKind { get; set; } = OutputKind.DynamicallyLinkedLibrary;
        /// <summary>
        /// The platform that the compiled assembly should target.
        /// AnyCPU is recommended for almost all cases.
        /// </summary>
        [field: SerializeField]
        public Platform TargetPlatform { get; set; } = Platform.AnyCpu;
        /// <summary>
        /// The debug symbol format produced by the compiler.
        /// </summary>
        [field: SerializeField]
        public DebugInformationFormat DebugFormat { get; set; } = DebugInformationFormat.PortablePdb;

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="outputDirectory"></param>
        /// <param name="outputName"></param>
        /// <param name="allowUnsafe"></param>
        /// <param name="allowOptimize"></param>
        /// <param name="allowConcurrentCompile"></param>
        /// <param name="deterministic"></param>
        /// <param name="generateInMemory"></param>
        /// <param name="generateSymbols"></param>
        /// <param name="warningLevel"></param>
        /// <param name="languageVersion"></param>
        /// <param name="outputKind"></param>
        /// <param name="targetPlatform"></param>
        /// <param name="debugFormat"></param>
        public CompileFlags(string outputDirectory = null, string outputName = null, bool allowUnsafe = false, bool allowOptimize = false, bool allowConcurrentCompile = true, 
            bool deterministic = true, bool generateInMemory = true, bool generateSymbols = true, int warningLevel = 4, LanguageVersion languageVersion = LanguageVersion.Default,
            OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary, Platform targetPlatform = Platform.AnyCpu, DebugInformationFormat debugFormat = DebugInformationFormat.PortablePdb) 
        { 
            this.OutputDirectory = outputDirectory;
            this.OutputName = outputName;
            this.AllowUnsafe = allowUnsafe;
            this.AllowOptimize = allowOptimize;
            this.AllowConcurrentCompile = allowConcurrentCompile;
            this.Deterministic = deterministic;
            this.GenerateInMemory = generateInMemory;
            this.GenerateSymbols = generateSymbols;
            this.WarningLevel = warningLevel;
            this.LanguageVersion = languageVersion;
            this.OutputKind = outputKind;
            this.TargetPlatform = targetPlatform;
            this.DebugFormat = debugFormat;
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="other"></param>
        protected internal CompileFlags(CompileFlags other)
        {
            this.OutputDirectory = other.OutputDirectory;
            this.OutputName = other.OutputName;
            this.AllowUnsafe = other.AllowUnsafe;
            this.AllowOptimize = other.AllowOptimize;
            this.AllowConcurrentCompile = other.AllowConcurrentCompile;
            this.Deterministic = other.Deterministic;
            this.GenerateInMemory = other.GenerateInMemory;
            this.GenerateSymbols = other.GenerateSymbols;
            this.WarningLevel = other.WarningLevel;
            this.LanguageVersion = other.LanguageVersion;
            this.OutputKind = other.OutputKind;
            this.TargetPlatform = other.TargetPlatform;
            this.DebugFormat = other.DebugFormat;
        }

        // Methods
        /// <summary>
        /// Get the output file extension based on the <see cref="OutputKind"/>.
        /// </summary>
        /// <returns></returns>
        public string GetOutputExtension()
        {
            return OutputKind switch
            {
                OutputKind.DynamicallyLinkedLibrary => ".dll",
                _ => ".exe",
            };
        }

        /// <summary>
        /// Get the parser options from this object.
        /// </summary>
        /// <param name="defineSymbols">The optional parser define symbols</param>
        /// <returns>Parser options created from this object</returns>
        public CSharpParseOptions GetParseOptions(IEnumerable<string> defineSymbols)
        {
            // Get options
            return new CSharpParseOptions(
                LanguageVersion,
                GenerateDocumentation ? DocumentationMode.Parse : DocumentationMode.None,
                SourceCodeKind.Regular,
                defineSymbols);
        }

        /// <summary>
        /// Get the compilation options from this object.
        /// </summary>
        /// <returns>The compilation options created from this object</returns>
        public CSharpCompilationOptions GetCompileOptions()
        {
            // Get optimize level
            OptimizationLevel optimizeLevel = AllowOptimize == true
                ? OptimizationLevel.Release
                : OptimizationLevel.Debug;

            // Don't allow concurrent compile on WebGL because it freezes the game due to lack of threading support
#if UNITY_WEBGL && UNITY_EDITOR == false
            if (AllowConcurrentCompile == true)
            {
                Debug.LogWarning("AllowConcurrentCompile was enabled but it is not supported on this platform! Disabling AllowConcurrentCompile...");
                AllowConcurrentCompile = false;
            }
#endif

            // Get options
            return new CSharpCompilationOptions(
                OutputKind,
                false,                                  // Suppressed diagnostics
                null,                                   // Module name
                null,                                   // Main type name
                null,                                   // Script class name
                null,                                   // Using
                optimizeLevel,                          // Optimize level
                false,                                  // Check overflow
                AllowUnsafe,                            // Allow unsafe
                null,                                   // Krypto key container
                null,                                   // Krypto key file
                default,                                // Krypto public key
                null,                                   // Delay sign
                TargetPlatform,                         // Platform
                ReportDiagnostic.Default,               // Diagnostic option
                WarningLevel,                           // Warning level
                null,                                   // Specific diagnostic
                AllowConcurrentCompile,                 // Allow concurrent compile
                Deterministic,                          // Deterministic
                null,                                   // XML reference resolver
                null,                                   // Source reference resolver
                null,                                   // Metadata reference resolver
                null,                                   // Assembly identity comparer
                null,                                   // Strong name provider
                false,                                  // Public sign        
                MetadataImportOptions.Public);          // Meta import options
        }

        /// <summary>
        /// Create a new instance with settings copied from the specified Roslyn C# settings window.
        /// The resulting object is a clone of the main source settings, so can be modified without affecting the global settings.
        /// </summary>
        /// <returns></returns>
        public static CompileFlags FromSettings()
        {
            // Load settings
            RoslynCSharpSettings settings = RoslynCSharpSettings.UserSettings;

            // Create from settings
            return FromSettings(settings);
        }

        /// <summary>
        /// Create a new instance with settings copied from the specified Roslyn C# settings window.
        /// The resulting object is a clone of the main source settings, so can be modified without affecting the global settings.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static CompileFlags FromSettings(RoslynCSharpSettings settings)
        {
            // Check for null
            if(settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Create copy from flags
            return new CompileFlags(settings.CompilerFlags);
        }
    }
}

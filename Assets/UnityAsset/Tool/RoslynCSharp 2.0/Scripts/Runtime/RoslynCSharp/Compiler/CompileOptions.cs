using RoslynCSharp.ExecutionSecurity;
using RoslynCSharp.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a set of options provided to the compiler for a specific compile request.
    /// </summary>
    public class CompileOptions : CompileFlags
    {
        // Properties
        /// <summary>
        /// Get the default compiler options.
        /// Options are mutable so always create a new instance.
        /// </summary>
        public static new CompileOptions Default => new();

        /// <summary>
        /// Should compiler warnings and errors be logged to the Unity console.
        /// </summary>
        public LogLevel CompilerLogLevel { get; set; } = LogLevel.Errors;
        /// <summary>
        /// The define symbols used for this request.
        /// </summary>
        public IList<string> DefineSymbols { get; } = new List<string>();
        /// <summary>
        /// The referenced assemblies used for this request.
        /// </summary>
        public IList<ICompilationReference> References { get; } = new List<ICompilationReference>();
        /// <summary>
        /// The conditional references assemblies used for this request.
        /// Key is the conditional define symbol that must be set for this reference to become active, and the value is the actual reference.
        /// </summary>
        public IList<KeyValuePair<string, ICompilationReference>> ConditionalReferences { get; } = new List<KeyValuePair<string, ICompilationReference>>();
        /// <summary>
        /// The parser processors that will be executed after the parsing stage has completed.
        /// </summary>
        public IList<IParserProcessor> ParserProcessors { get; } = new List<IParserProcessor>();
        /// <summary>
        /// The compilation processors that will run after the compilation has completed.
        /// </summary>
        public IList<ICompilationProcessor> CompilationProcessors { get; } = new List<ICompilationProcessor>();
        /// <summary>
        /// The execution security settings used when compiling.
        /// </summary>
        public ExecutionSecuritySettings ExecutionSecuritySettings { get; set; }
        /// <summary>
        /// The selector used to identify the main type defined within an assembly.
        /// </summary>
        public IMainTypeSelector MainTypeSelector { get; set; }
        /// <summary>
        /// The assembly context used to initialize the assembly for loading.
        /// Allows support for different scripting backends other than Mono/Jit.
        /// </summary>
        public IAssemblyContext AssemblyContext { get; set; }

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="compilerFlags"></param>
        /// <param name="compilerLogLevel"></param>
        /// <param name="defineSymbols"></param>
        /// <param name="references"></param>
        /// <param name="conditionalReferences"></param>
        /// <param name="parserProcessors"></param>
        /// <param name="compilationProcessors"></param>
        /// <param name="executionSecuritySettings"></param>
        /// <param name="mainTypeSelector"></param>
        public CompileOptions(CompileFlags compilerFlags = null, LogLevel compilerLogLevel = LogLevel.Errors, IEnumerable<string> defineSymbols = null, IEnumerable<ICompilationReference> references = null, IEnumerable<KeyValuePair<string, ICompilationReference>> conditionalReferences = null, IEnumerable<IParserProcessor> parserProcessors = null, IEnumerable<ICompilationProcessor> compilationProcessors = null, ExecutionSecuritySettings executionSecuritySettings = null, IMainTypeSelector mainTypeSelector = null)
            : base(compilerFlags != null ? compilerFlags : CompileFlags.Default)
        {
            // Log level
            this.CompilerLogLevel = compilerLogLevel;

            // Defines
            if (defineSymbols != null)
            {
                foreach (string define in defineSymbols)
                    this.DefineSymbols.Add(define);
            }

            // References
            if (references != null)
            {
                foreach (ICompilationReference reference in references)
                    this.References.Add(reference);
            }

            // Conditional
            if(conditionalReferences != null)
            {
                foreach(KeyValuePair<string, ICompilationReference> reference in conditionalReferences)
                    this.ConditionalReferences.Add(reference);
            }

            // Parser processors
            if (parserProcessors != null)
            {
                foreach (IParserProcessor processor in parserProcessors)
                    this.ParserProcessors.Add(processor);
            }

            // Compilation processors
            if (compilationProcessors != null)
            {
                foreach (ICompilationProcessor processor in compilationProcessors)
                    this.CompilationProcessors.Add(processor);
            }

            // Execution security
            this.ExecutionSecuritySettings = executionSecuritySettings != null
                ? new ExecutionSecuritySettings(executionSecuritySettings)
                : new ExecutionSecuritySettings();

            // Create default parser processor
            if (this.ExecutionSecuritySettings.SecurityMode != ExecutionSecurityMode.None)
                this.ParserProcessors.Add(new ExecutionSecurityProcessor(this.ExecutionSecuritySettings));

            // Main type selector
            this.MainTypeSelector = mainTypeSelector != null
                ? mainTypeSelector
                : new DefaultMainTypeSelector();
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="extraDefineSymbols"></param>
        /// <param name="extraReferences"></param>
        /// <param name="extraConditionalReferences"></param>
        protected internal CompileOptions(CompileOptions other, IEnumerable<string> extraDefineSymbols = null, IEnumerable<ICompilationReference> extraReferences = null, IEnumerable<KeyValuePair<string, ICompilationReference>> extraConditionalReferences = null)
            : base(other)
        {
            this.CompilerLogLevel = other.CompilerLogLevel;

            // Defines
            foreach (string define in other.DefineSymbols)
                this.DefineSymbols.Add(define);

            if(extraDefineSymbols != null)
                foreach(string define in extraDefineSymbols)
                    this.DefineSymbols.Add(define);

            // References
            foreach(ICompilationReference reference in other.References)
                this.References.Add(reference);

            foreach(KeyValuePair<string, ICompilationReference> reference in other.ConditionalReferences)
                this.ConditionalReferences.Add(reference);

            if(extraReferences != null)
                foreach(ICompilationReference reference in extraReferences)
                    this.References.Add(reference);

            if(extraConditionalReferences != null)
                foreach(KeyValuePair<string, ICompilationReference> reference in extraConditionalReferences)
                    this.ConditionalReferences.Add(reference);
        
            // Parser processors
            foreach(IParserProcessor parserProcessor in other.ParserProcessors)
                this.ParserProcessors.Add(parserProcessor);

            // Compilation processors
            foreach(ICompilationProcessor compilationProcessor in other.CompilationProcessors)
                this.CompilationProcessors.Add(compilationProcessor);

            // Execution security settings
            this.ExecutionSecuritySettings = new ExecutionSecuritySettings(other.ExecutionSecuritySettings);

            // Mian type selector
            this.MainTypeSelector = other.MainTypeSelector;
        }

        // Methods
        /// <summary>
        /// Get all define symbols.
        /// This method performs filtering and only returns distinct non-empty define strings.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetDefines()
        {
            return DefineSymbols
                .Distinct()
                .Where(d => string.IsNullOrEmpty(d) == false);
        }

        /// <summary>
        /// Get all references.
        /// This will evaluate all conditional references based on the current <see cref="DefineSymbols"/>.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ICompilationReference> GetReferences()
        {
            // Get standard references
            foreach(ICompilationReference reference in References)
                yield return reference;

            // Get conditional references
            foreach(KeyValuePair<string, ICompilationReference> compilationReference in ConditionalReferences)
            {
                // Check for condition met
                if(IsDefined(compilationReference.Key) == true)
                    yield return compilationReference.Value;
            }
        }

        /// <summary>
        /// Check whether the specified define symbol is present.
        /// </summary>
        /// <param name="defineSymbol">The define symbol to check</param>
        /// <returns>True if defined or false if not</returns>
        public bool IsDefined(string defineSymbol)
        {
            return DefineSymbols.Contains(defineSymbol);
        }

        /// <summary>
        /// Create a new instance with settings copied from the Roslyn C# settings window.
        /// The resulting object is a clone of the main source settings, so can be modified without affecting the global settings.
        /// </summary>
        /// <param name="compilerLogLevel"></param>
        /// <param name="extraDefineSymbols"></param>
        /// <param name="extraReferences"></param>
        /// <returns></returns>
        public static CompileOptions FromSettings(LogLevel compilerLogLevel = LogLevel.Errors, IEnumerable<string> extraDefineSymbols = null, IEnumerable<ICompilationReference> extraReferences = null, IEnumerable<KeyValuePair<string, ICompilationReference>> extraConditionalReferences = null)
        {
            // Get user settings
            RoslynCSharpSettings settings = RoslynCSharpSettings.UserSettings;

            // Create from settings
            return FromSettings(settings, compilerLogLevel, extraDefineSymbols, extraReferences, extraConditionalReferences);
        }

        /// <summary>
        /// Create a new instance with settings copied from the specified Roslyn C# settings window.
        /// The resulting object is a clone of the main source settings, so can be modified without affecting the global settings.
        /// </summary>
        /// <param name="settings">The settings asset to copy the options from</param>
        /// <param name="compilerLogLevel"></param>
        /// <param name="extraDefineSymbols"></param>
        /// <param name="extraReferences"></param>
        /// <returns></returns>
        public static CompileOptions FromSettings(RoslynCSharpSettings settings, LogLevel compilerLogLevel = LogLevel.Errors, IEnumerable<string> extraDefineSymbols = null, IEnumerable<ICompilationReference> extraReferences = null, IEnumerable<KeyValuePair<string, ICompilationReference>> extraConditionalReferences = null)
        {
            // Check for null
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Get defines
            IEnumerable<string> defineSymbols = (extraDefineSymbols != null
                ? Enumerable.Concat(settings.DefineSymbols, extraDefineSymbols)
                : settings.DefineSymbols)
                    .Where(d => string.IsNullOrEmpty(d) == false);

            // Get references
            IEnumerable<ICompilationReference> references = (extraReferences != null
                ? Enumerable.Concat(settings.AssemblyReferences, extraReferences)
                : settings.AssemblyReferences)
                    .Where(a => a != null);

            IEnumerable<KeyValuePair<string, ICompilationReference>> settingsConditionalReferences = settings.ConditionalAssemblyReferences
                .Where(r => r.Reference != null)
                .Select(r => new KeyValuePair<string, ICompilationReference>(r.DefineCondition, r.Reference));

            // Get conditional references
            IEnumerable<KeyValuePair<string, ICompilationReference>> conditionalReferences = (extraConditionalReferences != null
                ? Enumerable.Concat(settingsConditionalReferences, extraConditionalReferences)
                : settingsConditionalReferences);

            // Create new
            return new CompileOptions(
                new CompileFlags(settings.CompilerFlags),
                compilerLogLevel,
                defineSymbols,
                references,
                conditionalReferences,
                null, null,
                settings.ExecutionSecuritySettings);
        }
    }
}

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a result of a specific compilation request by the compiler.
    /// </summary>
    public sealed class CompileResult
    {
        // Private
        private static readonly string[] ignoreErrors =
        {
            "CS1701",   // Retargeting version errors
            "CS1702",
        };

        private readonly bool successful;
        private readonly AssemblySource assembly;
        private readonly CompileError[] errors;

        // Properties
        /// <summary>
        /// Was the compilation successful.
        /// </summary>
        public bool Success => successful;
        /// <summary>
        /// Get the compiled assembly if compilation was successful, or null if not.
        /// </summary>
        public AssemblySource Assembly => assembly;
        /// <summary>
        /// Get all compilation messages, warnings and errors reported by the compiler.
        /// </summary>
        public CompileError[] Errors => errors;

        // Constructor
        internal CompileResult(AssemblySource assembly, IEnumerable<Diagnostic> diagnostics)
        {
            this.successful = assembly != null;
            this.assembly = assembly;
            this.errors = GetCompilationErrors(diagnostics);
        }

        // Methods
        /// <summary>
        /// Log all compiled diagnostic messages to the Unity console which meet the specified log level.
        /// </summary>
        /// <param name="logLevel">The log level used to restrict the amount of detail reported</param>
        /// <param name="logHeader">Should a header message be logged to the console indicating the start of the report</param>
        public void ReportCompilationErrors(LogLevel logLevel, bool logHeader = true)
        {
            bool didLogHeader = false;
            void LogHeader()
            {
                if (didLogHeader == false)
                {
                    if (logHeader == true)
                    {
                        UnityEngine.Debug.Log("__Roslyn C# Compile Output__");
                    }
                    didLogHeader = true;
                }
            }

            // Process all
            foreach (CompileError error in errors)
            {
                // Check for error
                if(error.IsError == true && logLevel >= LogLevel.Errors)
                {
                    LogHeader();
                    Debug.LogError(error.ToString());
                }
                // Check for warning
                else if(error.IsWarning == true && logLevel >= LogLevel.Warnings)
                {
                    LogHeader();
                    Debug.LogWarning(error.ToString());
                }
                // Check for message
                else if(error.IsInfo == true && logLevel >= LogLevel.Messages)
                {
                    LogHeader();
                    Debug.Log(error.ToString());
                }
            }
        }

        private CompileError[] GetCompilationErrors(IEnumerable<Diagnostic> diagnostics)
        {
            // Only get messages we are interested in
            return diagnostics
                .Where(d => ignoreErrors.Contains(d.Id) == false)
                .Where(d => d.Severity != DiagnosticSeverity.Hidden)
                .Select(d => new CompileError(d))
                .ToArray();
        }
    }
}

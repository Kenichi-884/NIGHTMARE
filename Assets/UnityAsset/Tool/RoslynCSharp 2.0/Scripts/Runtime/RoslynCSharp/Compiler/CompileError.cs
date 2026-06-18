using Microsoft.CodeAnalysis;

namespace RoslynCSharp
{ 
    /// <summary>
    /// Represents a compiler message, warning or error relating to a specific compilation request.
    /// </summary>
    public sealed class CompileError
    {
        // Private   
        private readonly Diagnostic diagnostic;

        // Properties
        /// <summary>
        /// Get the error message code.
        /// </summary>
        public string Code => diagnostic.Id;
        /// <summary>
        /// Get the error message text.
        /// </summary>
        public string Message => diagnostic.GetMessage();
        /// <summary>
        /// Get the associated source file if available for the error.
        /// </summary>
        public string SourceFile => diagnostic.Location.SourceTree.FilePath;
        /// <summary>
        /// Get the associated line number if available for the error.
        /// </summary>
        public int SourceLine => diagnostic.Location.GetLineSpan().StartLinePosition.Line;
        /// <summary>
        /// Get the associated column index if available for the error.
        /// </summary>
        public int SourceColumn => diagnostic.Location.GetLineSpan().StartLinePosition.Character;
        /// <summary>
        /// Returns true if this is a compiler message.
        /// </summary>
        public bool IsInfo => diagnostic.Severity == DiagnosticSeverity.Info;
        /// <summary>
        /// Returns true if this is a compiler warning.
        /// </summary>
        public bool IsWarning => diagnostic.Severity == DiagnosticSeverity.Warning;
        /// <summary>
        /// Returns true if this is a compiler error.
        /// </summary>
        public bool IsError => diagnostic.Severity == DiagnosticSeverity.Error;
        /// <summary>
        /// Returns true if this is a warning elevated to an error due to strict compile.
        /// </summary>
        public bool IsWarningAsError => diagnostic.IsWarningAsError;
        /// <summary>
        /// Returns true if this error is suppressed.
        /// </summary>
        public bool IsSuppressed => diagnostic.IsSuppressed;

        // Constructor
        internal CompileError(Diagnostic diagnostic)
        {
            this.diagnostic = diagnostic;
        }

        // Methods
        /// <summary>
        /// Get this error as a string representation.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return diagnostic.ToString();
        }
    }
}

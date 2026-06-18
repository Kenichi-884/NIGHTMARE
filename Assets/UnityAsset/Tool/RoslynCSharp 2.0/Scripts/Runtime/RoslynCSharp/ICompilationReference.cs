using Microsoft.CodeAnalysis;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents an object that can be referenced as a dependency by the compiler.
    /// </summary>
    public interface ICompilationReference
    {
        // Properties
        /// <summary>
        /// Get the compiler reference metadata.
        /// </summary>
        MetadataReference CompilationReference { get; }
    }
}

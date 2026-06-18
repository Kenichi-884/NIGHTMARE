using Microsoft.CodeAnalysis;

namespace RoslynCSharp
{
    /// <summary>
    /// Called by the compiler when parsing has completed.
    /// Allows for additional syntax tree processing to occur during the compilation request.
    /// </summary>
    public interface IParserProcessor
    {
        // Properties
        /// <summary>
        /// Get the processor priority in the case that multiple processors are used.
        /// Default priority is 100 and implementation with a higher priority will run after those with a lower priority.
        /// </summary>
        int Priority { get; }

        // Methods
        /// <summary>
        /// Called by the compiler with the parsed syntax tree.
        /// </summary>
        /// <param name="syntaxTrees">The syntax trees that were parsed from the compile request</param>
        /// <returns>A modified array of syntax trees with the same or differing number of trees returned (Add or remove syntax trees is supported), or the same syntax tree array if no modification needs to take place</returns>
        SyntaxTree[] OnPostProcess(SyntaxTree[] syntaxTrees);
    }
}

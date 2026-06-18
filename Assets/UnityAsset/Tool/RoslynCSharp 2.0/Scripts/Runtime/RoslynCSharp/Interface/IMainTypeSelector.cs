using System.Collections.Generic;

namespace RoslynCSharp
{
    /// <summary>
    /// Interface for selecting the main type defined inside an assembly.
    /// Use <see cref="ScriptAssembly.DefaultMainTypeSelector"/> for default type selection/
    /// </summary>
    public interface IMainTypeSelector
    {
        // Methods
        /// <summary>
        /// Select the main type from the given assembly context.
        /// </summary>
        /// <param name="definingAssembly">The assembly where the types are defined</param>
        /// <param name="allTypes">All non-nested types defined within the assembly, both public and non-public</param>
        /// <returns>The type identified as the main type in this assembly</returns>
        ScriptType SelectMainType(ScriptAssembly definingAssembly, IReadOnlyList<ScriptType> allTypes);
    }
}

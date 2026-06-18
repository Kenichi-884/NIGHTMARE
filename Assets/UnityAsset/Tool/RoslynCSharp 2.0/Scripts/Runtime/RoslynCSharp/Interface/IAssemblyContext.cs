using Trivial.CodeSecurity;

namespace RoslynCSharp
{
    /// <summary>
    /// Used to register a loaded assembly with a script domain.
    /// </summary>
    public interface IAssemblyContext
    {
        /// <summary>
        /// Register the specified loaded assembly.
        /// </summary>
        /// <param name="domain">The script domain the assembly will be registered with</param>
        /// <param name="source">The assembly load source</param>
        /// <param name="compileResult">The compilation result</param>
        /// <param name="securityReport">The code security report for the assembly</param>
        /// <param name="securityMode">The code security mode that was used</param>
        /// <param name="mainTypeSelector">The main type selector</param>
        /// <returns>The loaded <see cref="ScriptAssembly"/> that will be registered with the domain</returns>
        ScriptAssembly RegisterAssembly(ScriptDomain domain, AssemblySource source, CompileResult compileResult, CodeSecurityReport securityReport, ScriptSecurityMode securityMode, IMainTypeSelector mainTypeSelector);
    }
}

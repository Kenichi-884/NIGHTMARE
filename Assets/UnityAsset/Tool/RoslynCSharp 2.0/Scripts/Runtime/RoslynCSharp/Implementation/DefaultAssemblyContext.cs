using Trivial.CodeSecurity;

namespace RoslynCSharp.Implementation
{
    internal sealed class DefaultAssemblyContext : IAssemblyContext
    {
        // Methods
        public ScriptAssembly RegisterAssembly(ScriptDomain domain, AssemblySource source, CompileResult compileResult, CodeSecurityReport securityReport, ScriptSecurityMode securityMode, IMainTypeSelector mainTypeSelector)
        {
            // Create default implementation
            return ScriptAssembly.CreateDefault(domain, source, compileResult, securityReport, mainTypeSelector);
        }
    }
}

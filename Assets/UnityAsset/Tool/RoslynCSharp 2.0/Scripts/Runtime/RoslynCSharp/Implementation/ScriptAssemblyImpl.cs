using System.Linq;
using System.Reflection;
using Trivial.CodeSecurity;

namespace RoslynCSharp.Implementation
{
    internal sealed class ScriptAssemblyImpl : ScriptAssembly
    {
        // Private
        private readonly Assembly assembly;
        private ScriptType[] types;

        // Properties
        public override Assembly SystemAssembly => assembly;
        public override ScriptType[] Types => types == null ? (types = LoadScriptTypes(assembly)) : types;

        // Constructor
        public ScriptAssemblyImpl(ScriptDomain domain, AssemblySource source, CompileResult compileResult, CodeSecurityReport securityReport, IMainTypeSelector mainTypeSelector)
            : base(domain, source, compileResult, securityReport, mainTypeSelector)
        {
            // Load the assembly
            this.assembly = source.LoadAssembly(domain.AssemblyLoader);
        }

        // Methods
        private ScriptType[] LoadScriptTypes(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t => t.IsNested == false)
                .Select(t => new ScriptTypeImpl(this, null, t))
                .ToArray();
        }
    }
}

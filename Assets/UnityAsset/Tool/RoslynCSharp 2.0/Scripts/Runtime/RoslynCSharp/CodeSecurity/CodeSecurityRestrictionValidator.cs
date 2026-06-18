using System;
using Trivial.CodeSecurity.Restrictions;

namespace RoslynCSharp.CodeSecurity
{
    [Serializable]
    public sealed class CodeSecurityRestrictionValidator : RestrictionValidator<CodeSecurityAssemblyRestriction>
    {
        // Methods
#if UNITY_EDITOR
        internal bool UpdateRestrictionChangesFromAssemblyReferenceAsset()
        {
            bool modified = false;

            // Process all
            foreach (CodeSecurityAssemblyRestriction assemblyRestriction in AssemblyRestrictions)
            {
                // Check for modified
                modified |= assemblyRestriction.UpdateRestrictionChangesFromAssemblyReferenceAsset();
            }
            return modified;
        }
#endif

        public static CodeSecurityRestrictionValidator CreatedDefaultRestrictions()
        {
            CodeSecurityRestrictionValidator restrictions = new CodeSecurityRestrictionValidator();

            // Initialize
            restrictions.InitializeDefaultRestrictions();
            return restrictions;
        }
    }
}

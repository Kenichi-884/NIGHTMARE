using Trivial.CodeSecurity;
using Trivial.CodeSecurity.Reference;
using Trivial.CodeSecurity.Restrictions;

namespace RoslynCSharp.CodeSecurity
{
    /// <summary>
    /// Provides the default code security validation behaviour with the option to override any checks and fallback to defaults for others.
    /// Useful if you are only interested in restricting certain types for example, but want to retain the default behaviour for other aspects.
    /// </summary>
    public class DefaultCodeValidator : ICodeSecurityValidator
    {
        // Private
        private CodeSecurityRestrictionValidator settingsValidator = null;

        // Properties
        private CodeSecurityRestrictionValidator SettingsValidator
        {
            get
            {
                LoadSettings();
                return settingsValidator;
            }
        }

        /// <summary>
        /// Should platform invoke feature be allowed in security checked code to call native code.
        /// </summary>
        public virtual bool AllowPInvoke => SettingsValidator.AllowPInvoke;
        /// <summary>
        /// Should unsafe code be allowed in security checked code to use things like unsafe contexts, fixed buffers, and pointers.
        /// </summary>
        public virtual bool AllowUnsafeCode => SettingsValidator.AllowUnsafe;

        // Methods
        /// <summary>
        /// Check whether the specified assembly reference is allowed to be referenced.
        /// </summary>
        /// <param name="assemblyReference">The assembly reference to check</param>
        /// <returns>True if the reference is allowed of false if it is considered illegal</returns>
        public virtual bool IsAssemblyReferenceAllowed(AssemblyReference assemblyReference) => SettingsValidator.IsAssemblyReferenceAllowed(assemblyReference);
        /// <summary>
        /// Check whether the specified named type reference is allowed to be referenced.
        /// Note that a type reference is passed here but only the Namespace property should be considered and the type reference is just for context.
        /// </summary>
        /// <param name="namedTypeReference">The named type reference to check</param>
        /// <returns>True if the reference is allowed of false if it is considered illegal</returns>
        public virtual bool IsNamespaceReferenceAllowed(TypeReference namedTypeReference) => SettingsValidator.IsNamespaceReferenceAllowed(namedTypeReference);
        /// <summary>
        /// Check whether the specified type reference is allowed to be referenced.
        /// </summary>
        /// <param name="typeReference">The type reference to check</param>
        /// <returns>True if the reference is allowed of false if it is considered illegal</returns>
        public virtual bool IsTypeReferenceAllowed(TypeReference typeReference) => SettingsValidator.IsTypeReferenceAllowed(typeReference);
        /// <summary>
        /// Check whether the specified member reference is allowed to be referenced.
        /// </summary>
        /// <param name="memberReference">The member reference to check which could be a field, property, method or event reference</param>
        /// <returns>True if the reference is allowed of false if it is considered illegal</returns>
        public virtual bool IsMemberReferenceAllowed(MemberReference memberReference) => SettingsValidator.IsMemberReferenceAllowed(memberReference);

        private void LoadSettings()
        {
            // Load settings on demand - cannot do it in the constructor because it uses Unity API
            if (settingsValidator == null)
                settingsValidator = RoslynCSharpSettings.UserSettings.CodeSecurityRestrictions;
        }
    }
}

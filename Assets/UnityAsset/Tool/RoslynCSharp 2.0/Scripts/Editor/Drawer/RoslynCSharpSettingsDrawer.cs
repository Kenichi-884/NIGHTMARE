using RoslynCSharp.CodeSecurity;
using System;
using System.Reflection;
using Trivial.CodeSecurity.Restrictions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace RoslynCSharp
{
    public sealed class RoslynCSharpSettingsDrawer
    {
        // Events
        public readonly UnityEvent RequestRepaint = new();

        // Private
        private readonly SerializedObject settings;
        private SerializedProperty logLevelProperty = null;
        private SerializedProperty compilerFlagsProperty = null;
        private SerializedProperty compilerDefineSymbolsProperty = null;
        private SerializedProperty compilerAssemblyReferencesProperty = null;
        private SerializedProperty compilerConditionalAssemblyReferencesProperty = null;
        private SerializedProperty securityCheckCodeProperty = null;
        private SerializedProperty whitelistCompiledAndLoadedAssembliesProperty = null;
        private SerializedProperty allowPInvokeProperty = null;
        private SerializedProperty allowUnsafeProperty = null;
        private SerializedProperty executionSettingsProperty = null;

        private RestrictionsValidatorDrawer restrictionsDrawer = null;
        private RestrictionsValidatorElementDrawer restrictionsElementDrawer = null;

        // Constructor
        public RoslynCSharpSettingsDrawer(SerializedObject settings)
        {
            // Check for null
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Check type
            if (settings.targetObject.GetType() != typeof(RoslynCSharpSettings))
                throw new ArgumentException("Serialized object must be for type: RoslynCSharpSettings");

            this.settings = settings;
        }

        // Methods
        public bool DrawGeneralOptions()
        {
            // Check for null
            if(logLevelProperty == null)
                logLevelProperty = SerializedPropertyUtil.GetPropertyField(settings, nameof(RoslynCSharpSettings.LogLevel));

            EditorGUI.BeginChangeCheck();
            { 
                // Draw property
                EditorGUILayout.PropertyField(logLevelProperty);
            }
            bool modified = EditorGUI.EndChangeCheck();

            // Check for modified
            if (modified == true)
            {
                settings.ApplyModifiedProperties();
                settings.Update();
            }

            return modified;
        }

        public bool DrawCompilerFlags()
        {
            // Check for null
            if (compilerFlagsProperty == null)
                compilerFlagsProperty = SerializedPropertyUtil.GetPropertyField(settings, nameof(RoslynCSharpSettings.CompilerFlags));

            EditorGUI.BeginChangeCheck();
            {
                // Draw all properties
                foreach (SerializedProperty property in SerializedPropertyUtil.GetChildProperties(compilerFlagsProperty.Copy()))
                {
                    EditorGUILayout.PropertyField(property);
                }
            }
            bool modified = EditorGUI.EndChangeCheck();

            // Check for modified
            if (modified == true)
            {
                settings.ApplyModifiedProperties();
                settings.Update();
            }

            return modified;
        }

        public bool DrawCompilerDefineSymbols()
        {
            // Check for null
            if(compilerDefineSymbolsProperty == null)
                compilerDefineSymbolsProperty = settings.FindProperty(nameof(RoslynCSharpSettings.defineSymbols));

            EditorGUI.BeginChangeCheck();
            { 
                // Draw
                EditorGUILayout.PropertyField(compilerDefineSymbolsProperty);
            }
            bool modified = EditorGUI.EndChangeCheck();

            // Check for modified
            if (modified == true)
            {
                settings.ApplyModifiedProperties();
                settings.Update();
            }

            return modified;
        }

        public bool DrawCompilerAssemblyReferences()
        {
            // Check for null
            if(compilerAssemblyReferencesProperty == null)
                compilerAssemblyReferencesProperty = settings.FindProperty(nameof(RoslynCSharpSettings.assemblyReferences));

            if (compilerConditionalAssemblyReferencesProperty == null)
                compilerConditionalAssemblyReferencesProperty = settings.FindProperty(nameof(RoslynCSharpSettings.conditionalAssemblyReferences));

            EditorGUI.BeginChangeCheck();
            {
                // Draw references
                EditorGUILayout.PropertyField(compilerAssemblyReferencesProperty);

                // Draw conditional references
                EditorGUILayout.PropertyField(compilerConditionalAssemblyReferencesProperty);
            }
            bool modified = EditorGUI.EndChangeCheck();

            // Check for modified
            if(modified == true)
            {
                settings.ApplyModifiedProperties();
                settings.Update();
            }

            return modified;
        }

        public bool DrawCodeSecurityOptions(bool drawHint = true)
        {
            // Check for null
            if (securityCheckCodeProperty == null)
                securityCheckCodeProperty = SerializedPropertyUtil.GetPropertyField(settings, nameof(RoslynCSharpSettings.SecurityCheckCode));

            // Find whitelist option
            if (whitelistCompiledAndLoadedAssembliesProperty == null)
                whitelistCompiledAndLoadedAssembliesProperty = SerializedPropertyUtil.GetPropertyField(settings, nameof(RoslynCSharpSettings.WhitelistCompiledAndLoadedAssemblies));

            // Find validation options
            if(allowPInvokeProperty == null)
                allowPInvokeProperty = SerializedPropertyUtil.GetNestedPropertyField(settings, nameof(RoslynCSharpSettings.CodeSecurityRestrictions), nameof(RestrictionValidator.AllowPInvoke));

            if (allowUnsafeProperty == null)
                allowUnsafeProperty = SerializedPropertyUtil.GetNestedPropertyField(settings, nameof(RoslynCSharpSettings.CodeSecurityRestrictions), nameof(RestrictionValidator.AllowUnsafe));

            // Draw hint
            if (drawHint == true)
                EditorGUILayout.HelpBox("Code security checks are run on the CIL bytecode of an assembly before executable code is loaded. It checks for references to metadata assembly, namespace and type references that are marked as illegal and will generate a detailed report of each occurrence in the case that the checked code is considered illegal. Any code found to be illegal will not be loaded or executed", MessageType.Info);

            // Draw property
            EditorGUI.BeginChangeCheck();
            {
                // Security property
                EditorGUILayout.PropertyField(securityCheckCodeProperty);

                // Whitelist property
                EditorGUILayout.PropertyField(whitelistCompiledAndLoadedAssembliesProperty);

                EditorGUI.BeginDisabledGroup(securityCheckCodeProperty.boolValue == false);
                {
                    // Pinvoke
                    EditorGUILayout.PropertyField(allowPInvokeProperty);

                    // Allow unsafe
                    EditorGUILayout.PropertyField(allowUnsafeProperty);
                }
                EditorGUI.EndDisabledGroup();

                // Spacer
                EditorGUILayout.Space();
            }
            bool modified = EditorGUI.EndChangeCheck();

            // Check for modified
            if(modified == true)
            {
                settings.ApplyModifiedProperties();
                settings.Update();
            }

            return modified;
        }

        public bool DrawCodeSecurityRestrictions()
        {
            bool modified = false;

            // Check for null
            if (securityCheckCodeProperty == null)
                securityCheckCodeProperty = SerializedPropertyUtil.GetPropertyField(settings, nameof(RoslynCSharpSettings.SecurityCheckCode));

            // Init drawers
            if (restrictionsDrawer == null) restrictionsDrawer = new RestrictionsValidatorDrawer(settings.targetObject);
            if (restrictionsElementDrawer == null) restrictionsElementDrawer = new RestrictionsValidatorElementDrawer(settings.targetObject);

            // Get settings
            CodeSecurityRestrictionValidator securitySettings = ((RoslynCSharpSettings)settings.targetObject).CodeSecurityRestrictions;

            // Disabled?
            EditorGUI.BeginDisabledGroup(securityCheckCodeProperty.boolValue == false);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // Title
                GUILayout.Space(3);
                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Code Restrictions");
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(3);

                CodeSecurityAssemblyRestriction deleteRestriction = null;

                // Draw restrictions
                foreach (CodeSecurityAssemblyRestriction assemblyRestriction in securitySettings.RuntimeAndAssemblyRestrictions)
                {
                    float height = restrictionsDrawer.CalculateAssemblyRestrictionTreeViewRequiredHeight(assemblyRestriction, restrictionsElementDrawer);
                    Rect drawRect = GUILayoutUtility.GetRect(Screen.width, height);

                    drawRect.width = Screen.width;
                    drawRect.height = height;

                    // Draw the tree view
                    if (restrictionsDrawer.DrawAssemblyRestrictionTreeView(assemblyRestriction, restrictionsElementDrawer, drawRect, Vector2.zero) == true)
                    {
                        deleteRestriction = assemblyRestriction;
                    }
                }

                // Check for delete
                if (deleteRestriction != null)
                {
                    // Remove from settings
                    securitySettings.AssemblyRestrictions.Remove(deleteRestriction);
                    securitySettings.ClearCache();
                    modified = true;

                    // Save changes
                    EditorUtility.SetDirty(settings.targetObject);

                    // Clear reference
                    deleteRestriction = null;

                    // Update settings
                    settings.ApplyModifiedProperties();
                    settings.Update();

                    restrictionsDrawer = null;
                    restrictionsElementDrawer = null;

                                     
                }
            }
            GUILayout.EndVertical();


            // Select assembly button
            if (GUILayout.Button("Add Assembly File", GUILayout.Height(30)) == true)
            {
                // Show selection dialog
                AssemblyReferenceUtil.ShowFileAssembleSelectionDialog((string asmPath) =>
                {
                    // Add assembly from restrictions
                    Assembly asmModule = Assembly.LoadFrom(asmPath);

                    // Create the assembly
                    CodeSecurityAssemblyRestriction asm = new CodeSecurityAssemblyRestriction(asmModule);

                    // Check for already added
                    if (securitySettings.AssemblyRestrictions.Exists(a => a.Name == asm.Name) == false)
                    {
                        // Add to restrictions and sort
                        securitySettings.AssemblyRestrictions.Add(asm);
                        securitySettings.Sort();

                        // Save changes
                        EditorUtility.SetDirty(settings.targetObject);
                        modified = true;
                    }
                    else
                        Debug.LogError("An assembly restriction already exists for assembly: " + asm.Name);
                });
            }

            if (GUILayout.Button("Add Loaded Assembly", GUILayout.Height(30)) == true)
            {
                // Show selection menu
                AssemblyReferenceUtil.ShowLoadedAssemblySelectionContextMenu((Assembly selectedAsm) =>
                {
                    // Create the assembly
                    CodeSecurityAssemblyRestriction asm = new CodeSecurityAssemblyRestriction(selectedAsm);

                    // Check for already added
                    if (securitySettings.AssemblyRestrictions.Exists(a => a.Name == asm.Name) == false)
                    {
                        // Add to restrictions and sort
                        securitySettings.AssemblyRestrictions.Add(asm);
                        securitySettings.Sort();

                        // Save changes
                        EditorUtility.SetDirty(settings.targetObject);
                        modified = true;
                    }
                    else
                        Debug.LogError("An assembly restriction already exists for assembly: " + asm.Name);
                });
            }

            // Reset button
            if (GUILayout.Button("Reset Security Restrictions", GUILayout.Height(30)) == true)
            {
                if (EditorUtility.DisplayDialog("Reset Security Restrictions?", "This action cannot be undone! You will lose all current security restriction settings that you have made and the default restrictions will be setup instead!", "Ok", "Cancel") == true)
                {
                    // Reset to defaults
                    ((RoslynCSharpSettings)settings.targetObject).CodeSecurityRestrictions = CodeSecurityRestrictionValidator.CreatedDefaultRestrictions();
                    EditorUtility.SetDirty(settings.targetObject);
                    modified = true;
                }
            }

            EditorGUI.EndDisabledGroup();
            return modified;
        }

        public bool DrawExecutionSecurityOptions(bool drawHint = true)
        {
            // Check for null
            if (executionSettingsProperty == null)
                executionSettingsProperty = SerializedPropertyUtil.GetPropertyField(settings, nameof(RoslynCSharpSettings.ExecutionSecuritySettings));

            // Draw hint
            if (drawHint == true)
                EditorGUILayout.HelpBox("Execution security checks are injected into compile code during the compilation phase so that looping expressions which could potentially freeze the host game will timeout or exit forcefully if a certain execution time or iteration count is exceeded", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            {
                // Draw all properties
                foreach (SerializedProperty property in SerializedPropertyUtil.GetChildProperties(executionSettingsProperty.Copy()))
                {
                    EditorGUILayout.PropertyField(property);
                }
            }
            bool modified = EditorGUI.EndChangeCheck();

            // Check for modified
            if (modified == true)
            {
                settings.ApplyModifiedProperties();
                settings.Update();
            }

            return modified;
        }
    }
}

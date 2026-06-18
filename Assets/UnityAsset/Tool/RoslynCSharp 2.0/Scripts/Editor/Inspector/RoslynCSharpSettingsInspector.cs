using System;
using UnityEditor;
using UnityEngine;

namespace RoslynCSharp
{
    [CustomEditor(typeof(RoslynCSharpSettings))]
    internal sealed class RoslynCSharpSettingsInspector : Editor
    {
        // Type
        public enum SettingsTab
        {
            Compiler,
            Security,
            Execution,
        }

        // Private
        private static readonly GUIContent[] tabContents =
        {
            new GUIContent("Compiler", "Settings used for all compilation requests unless options are manually provided as part of the compile API call"),
            new GUIContent("Security", "Security settings related to static code security analysis by inspecting the compile IL bytecode for potential harmful code"),
            new GUIContent("Execution", "Security settings related to runtime execution security such as preventing user code from blocking the application due to long or infinite loops"),
        };

        private RoslynCSharpSettingsDrawer drawer;
        private SettingsTab selectedTab = SettingsTab.Compiler;

        // Methods
        private void OnEnable()
        {
            // Create drawer
            drawer = new RoslynCSharpSettingsDrawer(serializedObject);
            drawer.RequestRepaint.AddListener(Repaint);
        }

        public override void OnInspectorGUI()
        {
            // Draw general
            drawer.DrawGeneralOptions();
            GUILayout.Space(20);

            // Display centered
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            {
                // Display tab
                selectedTab = (SettingsTab)GUILayout.Toolbar((int)selectedTab, tabContents, (EditorGUIUtility.currentViewWidth > 250
                        ? new[] { GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.75f) }
                        : Array.Empty<GUILayoutOption>()));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Separator space
            GUILayout.Space(15);
            GUIUtil.DrawSeparator(5);

            // Select tab
            switch (selectedTab)
            {
                case SettingsTab.Compiler:
                    {
                        // Draw compiler options
                        bool modified = drawer.DrawCompilerFlags();

                        // Draw compiler defines
                        GUILayout.Space(15);
                        GUIUtil.DrawSeparator(5);
                        modified |= drawer.DrawCompilerDefineSymbols();

                        // Draw compiler references
                        GUILayout.Space(15);
                        GUIUtil.DrawSeparator(5);
                        modified |= drawer.DrawCompilerAssemblyReferences();

                        // Check for modified
                        if (modified == true)
                            Repaint();
                        break;
                    }
                case SettingsTab.Security:
                    {
                        // Draw security options
                        bool modified = drawer.DrawCodeSecurityOptions();

                        // Draw security restrictions
                        modified |= drawer.DrawCodeSecurityRestrictions();

                        // Check for modified
                        if (modified == true)
                            Repaint();
                        break;
                    }
                case SettingsTab.Execution:
                    {
                        // Draw execution settings
                        bool modified = drawer.DrawExecutionSecurityOptions();

                        // Check for modified
                        if (modified == true)
                            Repaint();
                        break;
                    }
            }
           

            
        }
    }
}

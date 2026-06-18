using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RoslynCSharp
{
    [CustomEditor(typeof(AssemblyReferenceAsset))]
    internal sealed class AssemblyReferenceAssetInspector : Editor
    {
        // Private
        private AssemblyReferenceAssetDrawer drawer = null;

        // Methods
        private void OnEnable()
        {
            // Create drawer
            drawer = new AssemblyReferenceAssetDrawer(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            // Draw assembly info
            DrawAssemblyInfo();

            // Draw warnings
            DrawAssemblyWarnings();

            // Display buttons
            DrawAssemblySelectButtons();

            // Check for repaint
            if (serializedObject.UpdateIfRequiredOrScript() == true)
                Repaint();
        }

        private void DrawAssemblyInfo()
        {
            // Begin area
            GUILayout.BeginVertical();//EditorStyles.helpBox);
            {
                // Header
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                {
                    GUILayout.Label("Assembly Info", EditorStyles.largeLabel);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);

                // Begin disabled
                EditorGUI.BeginDisabledGroup(true);
                {
                    drawer.DisplayAssemblyName();
                    drawer.DisplayAssemblyPath();
                    drawer.DisplayAssemblyLastWriteTime();
                }
                EditorGUI.EndDisabledGroup();
            }
            GUILayout.EndVertical();
        }

        private void DrawAssemblyWarnings()
        {
            AssemblyReferenceAsset asset = target as AssemblyReferenceAsset;

            // Check for path
            if(asset.HasValidImage == true && asset.ReferenceAssembly.AssemblyExists == false)
            {
                EditorGUILayout.HelpBox("The assembly path does not exist. The assembly reference is still valid however changes to the source assembly will not be auto-detected on modification", MessageType.Warning);
            }
        }

        private void DrawAssemblySelectButtons()
        {
            GUILayout.Space(20);

            // Select file option
            if(GUILayout.Button("Select Assembly File", GUILayout.Height(30)) == true)
            {
                // Show selection dialog
                AssemblyReferenceUtil.ShowFileAssembleSelectionDialog((string asmPath) =>
                {
                    // Update file path
                    ((AssemblyReferenceAsset)target).UpdateAssemblyReference(asmPath, Path.GetFileNameWithoutExtension(asmPath));

                    // Mark as dirty
                    EditorUtility.SetDirty(target);
                    serializedObject.Update();

                    // Trigger repaint
                    Repaint();
                });
            }

            // Select loaded option
            if(GUILayout.Button("Select Loaded Assembly", GUILayout.Height(30)) == true)
            {
                // Show selection menu
                AssemblyReferenceUtil.ShowLoadedAssemblySelectionContextMenu((Assembly asmSelected) =>
                {
                    // Check for location
                    if (string.IsNullOrEmpty(asmSelected.Location) == true || File.Exists(asmSelected.Location) == false)
                    {
                        Debug.LogError("The selected assembly could not be referenced because it's source location could not be determined. Please add the assembly using the full path!");
                        return;
                    }

                    // Get path
                    string asmPath = asmSelected.Location
                        .Replace('\\', '/');

                    // Get relative path
                    string asmRelativePath = FileUtil.GetProjectRelativePath(asmPath);

                    // Check for relative
                    if(string.IsNullOrEmpty(asmRelativePath) == false && File.Exists(asmRelativePath) == true)
                        asmPath = asmRelativePath;

                    // Update the assembly
                    ((AssemblyReferenceAsset)target).UpdateAssemblyReference(asmPath, asmSelected.GetName().Name);

                    // Mark as dirty
                    EditorUtility.SetDirty(target);
                    serializedObject.Update();

                    // Trigger repaint
                    Repaint();
                });
            }
        }
    }
}

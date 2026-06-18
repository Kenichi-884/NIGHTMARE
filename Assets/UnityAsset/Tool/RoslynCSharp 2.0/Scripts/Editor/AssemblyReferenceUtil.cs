using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RoslynCSharp
{
    internal static class AssemblyReferenceUtil
    {
        // Private
        private static readonly Version zeroVersion = new Version(0, 0, 0, 0);

        // Methods
        public static void ShowFileAssembleSelectionDialog(Action<string> onSelected)
        {
            // Show the file select dialog
            string path = EditorUtility.OpenFilePanel("Open Assembly File", "Assets", "dll");

            if (string.IsNullOrEmpty(path) == false)
            {
                // Check for file exists
                if (File.Exists(path) == false)
                {
                    Debug.LogError("Assembly file does not exist: " + path);
                    return;
                }

                // Use relative path if possible
                string relativePath = path.Replace('\\', '/');
                relativePath = FileUtil.GetProjectRelativePath(relativePath);

                if (string.IsNullOrEmpty(relativePath) == false && File.Exists(relativePath) == true)
                    path = relativePath;

                // Get the path
                if (onSelected != null)
                    onSelected(path);
            }
        }

        public static void ShowLoadedAssemblySelectionContextMenu(Action<Assembly> onSelected)
        {
            GenericMenu menu = new GenericMenu();

            foreach ((string, Assembly) option in GetAssemblyMenuOptions()
                .OrderBy(m => m.Item1.Contains('/') == false)
                .ThenBy(m => m.Item1))
            {
                // Add an item
                menu.AddItem(new GUIContent(option.Item1), false, (object value) =>
                {
                    // Get the selected assembly
                    Assembly selectedAsm = (Assembly)value;

                    // Trigger event
                    if (onSelected != null)
                        onSelected(selectedAsm);
                }, option.Item2);
            }

            // SHow the menu
            menu.ShowAsContext();
        }

        private static IEnumerable<(string, Assembly)> GetAssemblyMenuOptions()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Get the name info
                AssemblyName name = asm.GetName();

                // Build menu name
                string menuName = name.Version != zeroVersion
                    ? string.Format("{0}, {1}", name.Name, name.Version)
                    : name.Name;
                    //asm.GetName().Name, asm.GetName().Version);
                
                // Check for Unity editor assemblies
                if(menuName.StartsWith("UnityEditor") == true)
                {
                    menuName = "Unity Editor Assemblies/" + menuName;
                }
                // Check for Unity module assemblies
                else if (menuName.StartsWith("UnityEngine") == true)
                {
                    menuName = "Unity Module Assemblies/" + menuName;
                }
                else if(menuName.StartsWith("Unity") == true)
                {
                    menuName = "Unity Package Assemblies/" + menuName;
                }
                // Check for system assemblies
                else if (menuName.StartsWith("System") == true || menuName.StartsWith("mscorlib") == true || menuName.StartsWith("netstandard") == true)
                {
                    menuName = "System Assemblies/" + menuName;
                }
                // Check for Roslyn assemblies
                else if (menuName.StartsWith("Roslyn") == true || menuName.StartsWith("Trivial") == true)
                {
                    menuName = "Roslyn Assemblies/" + menuName;
                }
                // Check for Microsoft assemblies
                else if (menuName.StartsWith("Microsoft") == true)
                {
                    menuName = "Microsoft Assemblies/" + menuName;
                }
                // Check for Mono assemblies
                else if (menuName.StartsWith("Mono") == true)
                {
                    menuName = "Mono Assemblies/" + menuName;
                }

                // Get the menu name
                yield return (menuName, asm);
            }
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace RoslynCSharp
{
    internal static class EditorMenu
    {
        // Methods
        [InitializeOnLoadMethod]
        public static void InitializeUserSettings()
        {
            RoslynCSharpSettings.CreateOrLoadRoslynUserSettings();
        }

        [MenuItem("Tools/Roslyn C# 2.0/Settings", priority = 10)]
        public static void ShowSettingsWindow()
        {
            // Create or load settings when required
            Selection.activeObject = RoslynCSharpSettings.CreateOrLoadRoslynUserSettings();
        }

        [MenuItem("Tools/Roslyn C# 2.0/Scripting Reference", priority = 49)]
        public static void ShowScriptingReference()
        {
            Application.OpenURL("https://trivialinteractive.co.uk/products/scriptingreference/roslyncsharp20");
        }

        [MenuItem("Tools/Roslyn C# 2.0/Code Samples", priority = 50)]
        public static void ShowSamples()
        {
            Application.OpenURL("https://github.com/TrivialInteractive/Roslyn-CSharp-2.0-Samples");
        }
    }
}

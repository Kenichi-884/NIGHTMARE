#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditorInternal;
using UnityEngine;

namespace DecalCollider.Editor
{
    [InitializeOnLoad]
    public static class ShaderGraphDependency
    {
        private const string Pkg = "com.unity.shadergraph";
        private static ListRequest _list;
        private static AddRequest _add;
        private static bool _askedUser;

        static ShaderGraphDependency()
        {
            // Query installed packages (includes cache)
            _list = Client.List(true);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            // 1) Handle the list request
            if (_list is { IsCompleted: true })
            {
                bool hasShaderGraph = false;
                if (_list.Status == StatusCode.Success && _list.Result != null)
                {
                    foreach (var p in _list.Result)
                    {
                        if (p is { name: Pkg }) { hasShaderGraph = true; break; }
                    }
                }
                else if (_list.Status == StatusCode.Failure)
                {
                    Debug.LogWarning("[Decal Shader] Could not query Package Manager: " +
                                     _list.Error.message);
                }

                // If missing, ask to install once
                if (!hasShaderGraph && !_askedUser)
                {
                    _askedUser = true;
                    if (EditorUtility.DisplayDialog(
                            "Decal Shader — Setup",
                            "This asset requires Shader Graph. Would you like to install it via the Unity Package Manager?",
                            "Install", "Later"))
                    {
                        // Start add operation
                        _add = Client.Add(Pkg);

                        // Bring up the Package Manager so Unity starts/resolves immediately
                        EditorApplication.ExecuteMenuItem("Window/Package Manager");

                        // Small nudge to repaint/refresh the Editor right away
                        EditorApplication.delayCall += () =>
                        {
                            InternalEditorUtility.RepaintAllViews();
                            AssetDatabase.Refresh();
                        };
                    }
                }

                // Done with listing either way
                _list = null;
            }

            // 2) Handle the add request (if any)
            if (_add is { IsCompleted: true })
            {
                if (_add.Status == StatusCode.Success)
                {
                    Debug.Log("[Decal Shader] Shader Graph installed successfully.");

                    // Force the editor to pick up the new package immediately
                    AssetDatabase.Refresh();
                    CompilationPipeline.RequestScriptCompilation();
                    EditorApplication.delayCall += InternalEditorUtility.RepaintAllViews;
                }
                else if (_add.Status == StatusCode.Failure)
                {
                    Debug.LogError("[Decal Shader] Failed to install Shader Graph: " +
                                   _add.Error.message);
                }

                _add = null;
            }

            // 3) Unsubscribe once we're fully done
            if (_list == null && _add == null)
            {
                EditorApplication.update -= Tick;
            }
        }
    }
}
#endif
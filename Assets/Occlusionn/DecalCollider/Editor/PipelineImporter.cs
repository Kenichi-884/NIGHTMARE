using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DecalCollider.Editor
{
    [InitializeOnLoad]
    public static class PipelineImporter
    {
        private const string AssetsRoot = "Assets";
        private const string VendorFolderName = "Occlusionn";
        private const string PackageFolderName = "DecalCollider";
        private const string RenderersFolderName = "Renderers";
        private const string TrackingFileName = ".installed.txt";

        static PipelineImporter()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                return;
            }

            EditorApplication.update -= OnUpdate;
            RunInstaller();
        }

        private static void RunInstaller()
        {
            string packageRootPath = FindDecalColliderRoot();
            if (string.IsNullOrEmpty(packageRootPath))
            {
                return;
            }

            if (EnsurePackageUnderOcclusionn(ref packageRootPath))
            {
                // Asset move triggers reimport/domain reload. The next load will continue installation.
                return;
            }

            CheckAndImportPipelinePackage(packageRootPath);
        }

        private static void CheckAndImportPipelinePackage(string packageRootPath)
        {
            string renderersPath = $"{packageRootPath}/{RenderersFolderName}";
            string trackingFilePath = $"{renderersPath}/{TrackingFileName}";
            if (File.Exists(ToAbsolutePath(trackingFilePath)))
            {
                return;
            }

            string packageToImport = ResolvePipelinePackageName();
            if (string.IsNullOrEmpty(packageToImport))
            {
                return;
            }

            string fullPath = $"{renderersPath}/{packageToImport}";
            if (!File.Exists(ToAbsolutePath(fullPath)))
            {
                Debug.LogWarning("[Decal Collider] Pipeline package not found at: " + fullPath);
                return;
            }

            AssetDatabase.ImportPackage(fullPath, false);
            CreateTrackingFile(trackingFilePath);
        }

        private static string FindDecalColliderRoot()
        {
            string[] candidateGuids = AssetDatabase.FindAssets($"{PackageFolderName} t:folder", new[] { AssetsRoot });
            foreach (string guid in candidateGuids)
            {
                string folderPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(folderPath))
                {
                    continue;
                }

                if (!string.Equals(Path.GetFileName(folderPath), PackageFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder($"{folderPath}/{RenderersFolderName}"))
                {
                    return folderPath;
                }
            }

            return string.Empty;
        }

        private static bool EnsurePackageUnderOcclusionn(ref string packageRootPath)
        {
            string occlusionnFolderPath = GetOrCreateOcclusionnFolder();
            if (string.IsNullOrEmpty(occlusionnFolderPath))
            {
                return false;
            }

            if (IsSameOrChildPath(packageRootPath, occlusionnFolderPath))
            {
                return false;
            }

            string targetPath = $"{occlusionnFolderPath}/{PackageFolderName}";
            if (AssetDatabase.IsValidFolder(targetPath))
            {
                Debug.LogWarning(
                    $"[Decal Collider] Target already exists at '{targetPath}'. Skipping auto move from '{packageRootPath}'.");
                packageRootPath = targetPath;
                return false;
            }

            string moveError = AssetDatabase.MoveAsset(packageRootPath, targetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError(
                    $"[Decal Collider] Failed to move asset into '{occlusionnFolderPath}': {moveError}");
                return false;
            }

            packageRootPath = targetPath;
            Debug.Log($"[Decal Collider] Moved package to '{targetPath}'.");
            AssetDatabase.Refresh();
            return true;
        }

        private static string GetOrCreateOcclusionnFolder()
        {
            string[] folderGuids = AssetDatabase.FindAssets($"{VendorFolderName} t:folder", new[] { AssetsRoot });
            foreach (string guid in folderGuids)
            {
                string folderPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.Equals(Path.GetFileName(folderPath), VendorFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return folderPath;
                }
            }

            string defaultPath = $"{AssetsRoot}/{VendorFolderName}";
            if (AssetDatabase.IsValidFolder(defaultPath))
            {
                return defaultPath;
            }

            string createdGuid = AssetDatabase.CreateFolder(AssetsRoot, VendorFolderName);
            if (string.IsNullOrEmpty(createdGuid))
            {
                return string.Empty;
            }

            AssetDatabase.Refresh();
            return NormalizePath(AssetDatabase.GUIDToAssetPath(createdGuid));
        }

        private static string ResolvePipelinePackageName()
        {
            RenderPipelineAsset activePipeline = GraphicsSettings.currentRenderPipeline;
            if (activePipeline == null)
            {
                activePipeline = QualitySettings.renderPipeline;
            }

            if (activePipeline == null)
            {
                activePipeline = GraphicsSettings.defaultRenderPipeline;
            }

            if (activePipeline == null)
            {
                return "Built-In.unitypackage";
            }

            string pipelineType = activePipeline.GetType().FullName ?? string.Empty;
            if (pipelineType.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pipelineType.IndexOf("URP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "URP.unitypackage";
            }

            if (pipelineType.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pipelineType.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pipelineType.IndexOf("HDRP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "HDRP.unitypackage";
            }

            return "Built-In.unitypackage";
        }

        private static void CreateTrackingFile(string trackingFilePath)
        {
            string directory = Path.GetDirectoryName(trackingFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(ToAbsolutePath(directory));
            }

            File.WriteAllText(
                ToAbsolutePath(trackingFilePath),
                "Installed on: " + DateTime.Now.ToString(CultureInfo.InvariantCulture));

            AssetDatabase.ImportAsset(trackingFilePath);
            AssetDatabase.Refresh();
        }

        private static string ToAbsolutePath(string assetRelativePath)
        {
            return Path.GetFullPath(assetRelativePath);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/");
        }

        private static bool IsSameOrChildPath(string path, string parentPath)
        {
            string normalizedPath = NormalizePath(path).TrimEnd('/');
            string normalizedParent = NormalizePath(parentPath).TrimEnd('/');
            return normalizedPath.Equals(normalizedParent, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(normalizedParent + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}

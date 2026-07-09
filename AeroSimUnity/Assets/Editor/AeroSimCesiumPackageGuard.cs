using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AeroSim.Editor
{
    [InitializeOnLoad]
    internal static class AeroSimCesiumPackageGuard
    {
        internal const string ExpectedUnityVersion = "2022.3.62f3c1";
        internal const string SupportedUnityVersionPrefix = "2022.3.62";
        internal const string CesiumVersion = "1.24.0";
        private const string PackageName = "com.cesium.unity";
        private const string BaselineAssetPath = "Assets/Editor/CesiumPackageGuardData/CesiumDefaultTilesetMaterial.mat.txt";
        private const string RelativeMaterialPath = "Source/Runtime/Resources/CesiumDefaultTilesetMaterial.mat";
        private const string RunOnceSessionKey = "AeroSim.CesiumPackageGuard.RunOnce";
        private const string VersionDialogSessionKey = "AeroSim.CesiumPackageGuard.VersionDialogShown";

        static AeroSimCesiumPackageGuard()
        {
            if (SessionState.GetBool(RunOnceSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(RunOnceSessionKey, true);
            EditorApplication.delayCall += RunGuard;
        }

        [MenuItem("AeroSim/Cesium/Sync Package Material Baseline")]
        private static void SyncPackageMaterialBaselineFromMenu()
        {
            RunGuard(logWhenAlreadyHealthy: true);
        }

        private static void RunGuard()
        {
            RunGuard(logWhenAlreadyHealthy: false);
        }

        private static void RunGuard(bool logWhenAlreadyHealthy)
        {
            WarnIfUnityVersionDiffers();

            string projectRoot = GetProjectRoot();
            string baselinePath = Path.Combine(projectRoot, BaselineAssetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(baselinePath))
            {
                Debug.LogWarning($"[AeroSim] Cesium package baseline is missing: {baselinePath}");
                return;
            }

            string[] targetPaths = BuildKnownTargetPaths(
                projectRoot,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                CesiumVersion,
                RelativeMaterialPath);

            int changedCount = 0;
            foreach (string targetPath in targetPaths.Where(File.Exists))
            {
                if (TryCopyBaselineIfDifferent(baselinePath, targetPath))
                {
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                Debug.Log($"[AeroSim] Synchronized {changedCount} Cesium package material file(s) to the team baseline. Refreshing Unity assets.");
                ClearConsole();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            else if (logWhenAlreadyHealthy)
            {
                Debug.Log("[AeroSim] Cesium package material baseline is already in sync.");
            }
        }

        private static void WarnIfUnityVersionDiffers()
        {
            if (string.Equals(Application.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal))
            {
                return;
            }

            bool isSupported = IsUnityVersionSupported(Application.unityVersion);
            string message = isSupported
                ? $"当前 Unity 版本为 {Application.unityVersion}，项目推荐版本为 {ExpectedUnityVersion}。\n\n" +
                  $"当前版本仍属于允许运行的 {SupportedUnityVersionPrefix} 同家族版本，Cesium 材质守护会继续生效。\n" +
                  "但如果要改场景、Prefab、材质等资源，仍建议尽量切回仓库锁定版本后再提交。"
                : $"当前 Unity 版本为 {Application.unityVersion}，项目推荐版本为 {ExpectedUnityVersion}。\n\n" +
                  $"当前版本不在允许运行的 {SupportedUnityVersionPrefix} 同家族范围内。\n" +
                  "不同编辑器版本会触发资源重序列化，并可能再次出现 Cesium immutable package 材质报错。\n" +
                  "请尽快切回仓库锁定的 Unity 版本后再继续改资源或提交。";

            Debug.LogWarning($"[AeroSim] {message.Replace('\n', ' ')}");

            if (!Application.isBatchMode && !SessionState.GetBool(VersionDialogSessionKey, false))
            {
                SessionState.SetBool(VersionDialogSessionKey, true);
                EditorUtility.DisplayDialog("AeroSim Unity 版本提醒", message, "知道了");
            }
        }

        internal static bool IsUnityVersionSupported(string unityVersion)
        {
            return !string.IsNullOrWhiteSpace(unityVersion)
                && unityVersion.StartsWith(SupportedUnityVersionPrefix, StringComparison.Ordinal);
        }

        internal static string[] BuildKnownTargetPaths(string projectRoot, string localAppDataRoot, string cesiumVersion, string relativeMaterialPath)
        {
            string packageFolder = $"{PackageName}@{cesiumVersion}";
            string normalizedRelativePath = relativeMaterialPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            return new[]
            {
                Path.Combine(projectRoot, "Library", "PackageCache", packageFolder, normalizedRelativePath),
                Path.Combine(localAppDataRoot, "Unity", "cache", "packages", "unity.pkg.cesium.com", packageFolder, normalizedRelativePath)
            };
        }

        internal static string NormalizeTextForComparison(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.EndsWith("\n", StringComparison.Ordinal) ? normalized : normalized + "\n";
        }

        private static bool TryCopyBaselineIfDifferent(string baselinePath, string targetPath)
        {
            string desiredText = File.ReadAllText(baselinePath);
            string currentText = File.ReadAllText(targetPath);

            if (string.Equals(
                NormalizeTextForComparison(desiredText),
                NormalizeTextForComparison(currentText),
                StringComparison.Ordinal))
            {
                return false;
            }

            File.WriteAllBytes(targetPath, File.ReadAllBytes(baselinePath));
            return true;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void ClearConsole()
        {
            Type logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            MethodInfo clearMethod = logEntriesType?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            clearMethod?.Invoke(null, null);
        }
    }
}

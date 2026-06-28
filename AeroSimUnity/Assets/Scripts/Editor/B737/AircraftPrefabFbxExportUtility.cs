using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports the current Unity prefab hierarchy as a new FBX file.
///
/// Why this is separate from packaging:
/// A Unity prefab can have a different hierarchy from the original imported FBX. Copying or
/// packaging the prefab does not rewrite the source FBX hierarchy. To make an FBX whose node
/// tree follows the prefab, Unity must export a new FBX from the prefab contents.
///
/// This script calls Unity's official FBX Exporter package by reflection, so the project will
/// still compile even when the package is not installed. Install "FBX Exporter" from Package
/// Manager if the menu reports that the exporter is missing.
/// </summary>
public static class AircraftPrefabFbxExportUtility
{
    private const string PackageRootPrefix = "Assets/Aircraft/B737";
    private const string PackagePrefabFolderMarker = "/Prefabs/";
    private const string PackageModelFolderName = "Models";
    private const string PackageFbxName = "B737_Aircraft.fbx";
    private const string TempExportFolder = "Assets/__B737FbxExportTemp";

    /// <summary>
    /// Manual entry:
    /// Unity top menu > Tools > B737 > Export Selected Prefab As FBX
    /// </summary>
    [MenuItem("Tools/B737/Export Selected Prefab As FBX")]
    public static void ExportSelectedPrefabAsFbx()
    {
        string prefabPath = GetSelectedPrefabPath();

        if (string.IsNullOrEmpty(prefabPath))
        {
            ShowPackagePrefabRequiredMessage();
            return;
        }

        ExportPrefabAsFbx(prefabPath);
    }

    /// <summary>
    /// Batch-mode entry kept for automation. It still requires the selected/target prefab to
    /// live inside an independent package, because overwriting the original source FBX would
    /// make the project harder to recover.
    /// </summary>
    public static void ExportSelectedPackagePrefabAsFbx()
    {
        ExportSelectedPrefabAsFbx();
    }

    private static void ExportPrefabAsFbx(string prefabPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogError($"[AircraftPrefabFbxExportUtility] Prefab not found: {prefabPath}");
            return;
        }

        string exportAssetPath = ResolvePackageFbxPath(prefabPath);

        if (string.IsNullOrEmpty(exportAssetPath))
        {
            ShowPackagePrefabRequiredMessage();
            return;
        }

        MethodInfo exportObjectMethod = FindFbxExportObjectMethod();

        if (exportObjectMethod == null)
        {
            string message =
                "FBX Exporter package is not installed or its API was not found.\n\n" +
                "Install it in Unity:\n" +
                "Window > Package Manager > Unity Registry > FBX Exporter > Install";

            Debug.LogError($"[AircraftPrefabFbxExportUtility] {message}");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("FBX Exporter missing", message, "OK");
            }

            return;
        }

        CreateFolderRecursive(TempExportFolder);
        string tempExportAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{TempExportFolder}/{PackageFbxName}");
        string tempExportFullPath = Path.GetFullPath(tempExportAssetPath);

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            exportObjectMethod.Invoke(null, new object[] { tempExportFullPath, prefabRoot });
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogError($"[AircraftPrefabFbxExportUtility] FBX export failed: {exception.InnerException}");
            return;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.Refresh();
        PreserveExportedFbxHierarchyOnImport(tempExportAssetPath);
        int temporaryMeshCount = LoadMeshesByName(tempExportAssetPath).Count;

        if (temporaryMeshCount == 0)
        {
            Debug.LogError(
                "[AircraftPrefabFbxExportUtility] Temporary FBX export contained 0 meshes. " +
                "The package FBX was not overwritten.");
            AssetDatabase.DeleteAsset(TempExportFolder);
            AssetDatabase.Refresh();
            return;
        }

        File.Copy(Path.GetFullPath(tempExportAssetPath), Path.GetFullPath(exportAssetPath), true);
        AssetDatabase.DeleteAsset(TempExportFolder);
        AssetDatabase.ImportAsset(exportAssetPath, ImportAssetOptions.ForceUpdate);
        PreserveExportedFbxHierarchyOnImport(exportAssetPath);
        int meshSlotsRewired = RewirePrefabMeshesToExportedFbx(prefabPath, exportAssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[AircraftPrefabFbxExportUtility] Package FBX replaced from prefab hierarchy.\n" +
            $"Prefab: {prefabPath}\n" +
            $"FBX: {exportAssetPath}\n" +
            $"Temporary exported mesh count: {temporaryMeshCount}\n" +
            "Import settings: preserve hierarchy enabled, sort hierarchy by name disabled\n" +
            $"Mesh slots rewired: {meshSlotsRewired}");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Package FBX replaced",
                $"Replaced package FBX from prefab hierarchy:\n{exportAssetPath}\n\nTemporary exported mesh count: {temporaryMeshCount}\nImport settings were updated to preserve hierarchy.\nMesh slots rewired: {meshSlotsRewired}",
                "OK");
        }
    }

    private static string GetSelectedPrefabPath()
    {
        UnityEngine.Object selectedObject = Selection.activeObject;

        if (selectedObject == null)
        {
            return null;
        }

        string selectedPath = AssetDatabase.GetAssetPath(selectedObject);

        if (string.IsNullOrEmpty(selectedPath) || !selectedPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return selectedPath;
    }

    private static MethodInfo FindFbxExportObjectMethod()
    {
        const string exporterTypeName = "UnityEditor.Formats.Fbx.Exporter.ModelExporter";

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type exporterType = assembly.GetType(exporterTypeName);

            if (exporterType == null)
            {
                continue;
            }

            MethodInfo method = exporterType.GetMethod(
                "ExportObject",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(UnityEngine.Object) },
                null);

            if (method != null)
            {
                return method;
            }
        }

        return null;
    }

    private static string ResolvePackageFbxPath(string prefabPath)
    {
        int markerIndex = prefabPath.IndexOf(PackagePrefabFolderMarker, StringComparison.OrdinalIgnoreCase);

        if (!prefabPath.StartsWith(PackageRootPrefix, StringComparison.OrdinalIgnoreCase) || markerIndex <= 0)
        {
            return null;
        }

        string packageRoot = prefabPath.Substring(0, markerIndex);
        string fbxPath = $"{packageRoot}/{PackageModelFolderName}/{PackageFbxName}";

        return File.Exists(Path.GetFullPath(fbxPath)) ? fbxPath : null;
    }

    private static void CreateFolderRecursive(string unityFolderPath)
    {
        string[] parts = unityFolderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    private static void PreserveExportedFbxHierarchyOnImport(string fbxPath)
    {
        ModelImporter modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;

        if (modelImporter == null)
        {
            Debug.LogWarning($"[AircraftPrefabFbxExportUtility] ModelImporter not found for exported FBX: {fbxPath}");
            return;
        }

        // Preserve Hierarchy keeps Unity from collapsing empty root/parent nodes during import.
        // Sort Hierarchy By Name changes child order in the imported model, so disable it when
        // the FBX is meant to mirror the prefab hierarchy as closely as possible.
        modelImporter.preserveHierarchy = true;
        modelImporter.sortHierarchyByName = false;
        modelImporter.SaveAndReimport();
    }

    private static int RewirePrefabMeshesToExportedFbx(string prefabPath, string fbxPath)
    {
        Dictionary<string, Mesh> exportedMeshesByName = LoadMeshesByName(fbxPath);

        if (exportedMeshesByName.Count == 0)
        {
            Debug.LogWarning($"[AircraftPrefabFbxExportUtility] No meshes found in exported FBX: {fbxPath}");
            return 0;
        }

        int changedSlots = 0;
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            foreach (MeshFilter meshFilter in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh currentMesh = meshFilter.sharedMesh;

                if (currentMesh == null)
                {
                    continue;
                }

                if (!exportedMeshesByName.TryGetValue(currentMesh.name, out Mesh exportedMesh))
                {
                    continue;
                }

                if (currentMesh == exportedMesh)
                {
                    continue;
                }

                meshFilter.sharedMesh = exportedMesh;
                EditorUtility.SetDirty(meshFilter);
                changedSlots++;
            }

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh currentMesh = skinnedMeshRenderer.sharedMesh;

                if (currentMesh == null)
                {
                    continue;
                }

                if (!exportedMeshesByName.TryGetValue(currentMesh.name, out Mesh exportedMesh))
                {
                    continue;
                }

                if (currentMesh == exportedMesh)
                {
                    continue;
                }

                skinnedMeshRenderer.sharedMesh = exportedMesh;
                EditorUtility.SetDirty(skinnedMeshRenderer);
                changedSlots++;
            }

            if (changedSlots > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return changedSlots;
    }

    private static Dictionary<string, Mesh> LoadMeshesByName(string fbxPath)
    {
        var meshesByName = new Dictionary<string, Mesh>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

        foreach (UnityEngine.Object asset in assets)
        {
            Mesh mesh = asset as Mesh;

            if (mesh == null)
            {
                continue;
            }

            meshesByName[mesh.name] = mesh;
        }

        return meshesByName;
    }

    private static void ShowPackagePrefabRequiredMessage()
    {
        string message =
            "Please select the prefab inside an independent package before exporting.\n\n" +
            "Expected prefab path example:\n" +
            "Assets/Aircraft/B737/Prefabs/B737.prefab\n\n" +
            "This tool intentionally refuses to overwrite the original source FBX.";

        Debug.LogError($"[AircraftPrefabFbxExportUtility] {message}");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Select package prefab", message, "OK");
        }
    }
}

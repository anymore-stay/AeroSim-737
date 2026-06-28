using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Saves the current Boeing 737 prefab as an independent version snapshot.
///
/// The working prefab is treated as the editable source of truth and is never modified. Each
/// snapshot gets its own folder with:
/// - an FBX exported from the current prefab hierarchy,
/// - a Unity prefab that keeps scripts/cameras/settings and references the snapshot FBX,
/// - copied materials and textures,
/// - an optional .unitypackage export for sharing.
/// </summary>
public sealed class AircraftPrefabIsolationUtility : EditorWindow
{
    private const string DefaultSourceAssetFolder = "Assets/Aircraft/B737";
    private const string DefaultSourcePrefabPath = "Assets/Aircraft/B737/Prefabs/B737.prefab";
    private const string SavedVersionsRoot = "Assets/Aircraft/B737/SavedVersions";
    private const string PackageExportFolder = "B737_CustomPackage_Exports";

    private GameObject sourcePrefab;
    private Vector2 scroll;

    [MenuItem("Tools/B737/Save Version Snapshot")]
    public static void SaveVersionSnapshotFromMenu()
    {
        AircraftPrefabIsolationUtility window = GetWindow<AircraftPrefabIsolationUtility>("B737 快照");
        window.minSize = new Vector2(520f, 260f);
        window.TryUseCurrentSelection();
        window.Show();
    }

    private void OnEnable()
    {
        if (sourcePrefab == null)
        {
            sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSourcePrefabPath);
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("波音 737 版本快照", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择要作为新独立快照来源的 Prefab。可以选择本项目里的原始飞机 Prefab，也可以选择别人从快照包导入后的 Prefab。",
            MessageType.Info);

        sourcePrefab = (GameObject)EditorGUILayout.ObjectField(
            "源 Prefab",
            sourcePrefab,
            typeof(GameObject),
            false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("使用当前选择"))
            {
                TryUseCurrentSelection();
            }

            if (GUILayout.Button("使用默认 Prefab"))
            {
                sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSourcePrefabPath);
            }
        }

        string sourcePrefabPath = GetPrefabAssetPath(sourcePrefab);
        if (string.IsNullOrEmpty(sourcePrefabPath))
        {
            EditorGUILayout.HelpBox("请从 Project 窗口选择一个 Prefab 资源。", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox($"源路径：\n{sourcePrefabPath}", MessageType.None);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(sourcePrefabPath)))
        {
            if (GUILayout.Button("保存版本快照", GUILayout.Height(36f)))
            {
                SaveBoeing737VersionSnapshot(sourcePrefabPath);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void TryUseCurrentSelection()
    {
        GameObject selectedPrefab = Selection.activeObject as GameObject;
        if (string.IsNullOrEmpty(GetPrefabAssetPath(selectedPrefab)))
        {
            return;
        }

        sourcePrefab = selectedPrefab;
        Repaint();
    }

    public static void SaveBoeing737VersionSnapshot()
    {
        SaveBoeing737VersionSnapshot(DefaultSourcePrefabPath);
    }

    private static void SaveBoeing737VersionSnapshot(string sourcePrefabPath)
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);

        if (sourcePrefab == null)
        {
            Debug.LogError($"[AircraftPrefabIsolationUtility] 未找到源 Prefab：{sourcePrefabPath}");
            return;
        }

        string sourceAssetFolder = ResolveSourceAssetFolder(sourcePrefabPath);

        MethodInfo exportObjectMethod = FindFbxExportObjectMethod();

        if (exportObjectMethod == null)
        {
            string message =
                "没有安装 FBX Exporter 包，或者没有找到它的导出 API。\n\n" +
                "请在 Unity 中安装：\n" +
                "Window > Package Manager > Unity Registry > FBX Exporter > Install";
            Debug.LogError($"[AircraftPrefabIsolationUtility] {message}");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("缺少 FBX Exporter", message, "确定");
            }

            return;
        }

        string versionName = GetNextVersionName();
        string versionRoot = $"{SavedVersionsRoot}/{versionName}";
        string materialsFolder = $"{versionRoot}/Materials";
        string texturesFolder = $"{versionRoot}/Textures";
        string modelsFolder = $"{versionRoot}/Models";
        string fbmFolder = $"{modelsFolder}/B737_Aircraft.fbm";
        string snapshotFbxPath = $"{versionRoot}/{versionName}.fbx";
        string snapshotPrefabPath = $"{versionRoot}/{versionName}.prefab";

        CreateFolderRecursive(SavedVersionsRoot);
        CreateFolderRecursive(versionRoot);
        CreateFolderRecursive(modelsFolder);
        CopyFolderIfExists($"{sourceAssetFolder}/Materials", materialsFolder);
        CopyFolderIfExists($"{sourceAssetFolder}/Textures", texturesFolder);
        CopyFolderIfExists($"{sourceAssetFolder}/Models/B737_Aircraft.fbm", fbmFolder);
        AssetDatabase.Refresh();

        int materialAssetsCopied = CopyMaterialsUsedBySourcePrefab(sourcePrefabPath, materialsFolder);
        AssetDatabase.Refresh();

        Dictionary<string, Material> snapshotMaterialsByName = LoadMaterialsByName(versionRoot);
        int textureSlotsRewired = RewireSnapshotMaterialTextures(versionRoot, sourceAssetFolder, snapshotMaterialsByName);

        int temporaryMeshCount = ExportPrefabHierarchyToFbx(sourcePrefabPath, exportObjectMethod, snapshotFbxPath);

        if (temporaryMeshCount == 0)
        {
            Debug.LogError(
                "[AircraftPrefabIsolationUtility] 快照 FBX 中没有任何网格，未创建快照 Prefab。");
            return;
        }

        Dictionary<string, Mesh> snapshotMeshesByName = LoadMeshesByName(snapshotFbxPath);

        int materialSlotsRewired;
        int meshSlotsRewired;
        int nullMeshSlots;
        CreateSnapshotPrefab(
            snapshotPrefabPath,
            sourcePrefabPath,
            snapshotMaterialsByName,
            snapshotMeshesByName,
            out materialSlotsRewired,
            out meshSlotsRewired,
            out nullMeshSlots);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string unityPackagePath = ExportUnityPackage(versionRoot, versionName);

        Debug.Log(
            "[AircraftPrefabIsolationUtility] 波音 737 版本快照已创建。\n" +
            $"版本：{versionName}\n" +
            $"源 Prefab：{sourcePrefabPath}\n" +
            $"根目录：{versionRoot}\n" +
            $"FBX: {snapshotFbxPath}\n" +
            $"Prefab：{snapshotPrefabPath}\n" +
            $"UnityPackage：{unityPackagePath}\n" +
            $"导出的 FBX 网格数量：{temporaryMeshCount}\n" +
            $"额外复制的材质数量：{materialAssetsCopied}\n" +
            $"重连的材质槽数量：{materialSlotsRewired}\n" +
            $"重连的贴图槽数量：{textureSlotsRewired}\n" +
            $"重连的网格槽数量：{meshSlotsRewired}\n" +
            $"保存后为空的网格槽数量：{nullMeshSlots}");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "波音 737 版本快照已保存",
                $"已保存版本：{versionName}\n\nPrefab：\n{snapshotPrefabPath}\n\nFBX：\n{snapshotFbxPath}\n\nUnityPackage：\n{unityPackagePath}",
                "确定");
        }
    }

    private static int ExportPrefabHierarchyToFbx(string sourcePrefabPath, MethodInfo exportObjectMethod, string snapshotFbxPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(sourcePrefabPath);

        try
        {
            exportObjectMethod.Invoke(null, new object[] { Path.GetFullPath(snapshotFbxPath), prefabRoot });
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogError($"[AircraftPrefabIsolationUtility] FBX 导出失败：{exception.InnerException}");
            return 0;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.Refresh();
        PreserveExportedFbxHierarchyOnImport(snapshotFbxPath);
        return LoadMeshesByName(snapshotFbxPath).Count;
    }

    private static void CreateSnapshotPrefab(
        string snapshotPrefabPath,
        string sourcePrefabPath,
        Dictionary<string, Material> snapshotMaterialsByName,
        Dictionary<string, Mesh> snapshotMeshesByName,
        out int materialSlotsRewired,
        out int meshSlotsRewired,
        out int nullMeshSlots)
    {
        materialSlotsRewired = 0;
        meshSlotsRewired = 0;
        nullMeshSlots = 0;

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(sourcePrefabPath);

        try
        {
            materialSlotsRewired = RewireRendererMaterials(prefabRoot, snapshotMaterialsByName);
            meshSlotsRewired = RewireMeshes(prefabRoot, snapshotMeshesByName);
            nullMeshSlots = CountNullMeshSlots(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, snapshotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static int RewireRendererMaterials(GameObject prefabRoot, Dictionary<string, Material> materialsByName)
    {
        int changedSlots = 0;

        foreach (Renderer renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool rendererChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material currentMaterial = materials[i];

                if (currentMaterial == null)
                {
                    continue;
                }

                string cleanName = RemoveUnityInstanceSuffix(currentMaterial.name);

                if (!materialsByName.TryGetValue(cleanName, out Material snapshotMaterial))
                {
                    continue;
                }

                if (currentMaterial == snapshotMaterial)
                {
                    continue;
                }

                materials[i] = snapshotMaterial;
                rendererChanged = true;
                changedSlots++;
            }

            if (rendererChanged)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }

        return changedSlots;
    }

    private static int RewireMeshes(GameObject prefabRoot, Dictionary<string, Mesh> meshesByName)
    {
        int changedSlots = 0;

        foreach (MeshFilter meshFilter in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh currentMesh = meshFilter.sharedMesh;

            if (currentMesh == null)
            {
                continue;
            }

            if (!TryGetSnapshotMesh(meshesByName, currentMesh.name, out Mesh snapshotMesh))
            {
                continue;
            }

            if (currentMesh == snapshotMesh)
            {
                continue;
            }

            meshFilter.sharedMesh = snapshotMesh;
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

            if (!TryGetSnapshotMesh(meshesByName, currentMesh.name, out Mesh snapshotMesh))
            {
                continue;
            }

            if (currentMesh == snapshotMesh)
            {
                continue;
            }

            skinnedMeshRenderer.sharedMesh = snapshotMesh;
            EditorUtility.SetDirty(skinnedMeshRenderer);
            changedSlots++;
        }

        return changedSlots;
    }

    private static int CountNullMeshSlots(GameObject prefabRoot)
    {
        int nullMeshSlots = 0;

        foreach (MeshFilter meshFilter in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
            {
                nullMeshSlots++;
            }
        }

        foreach (SkinnedMeshRenderer skinnedMeshRenderer in prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinnedMeshRenderer.sharedMesh == null)
            {
                nullMeshSlots++;
            }
        }

        return nullMeshSlots;
    }

    private static int RewireSnapshotMaterialTextures(string versionRoot, string sourceAssetFolder, Dictionary<string, Material> materialsByName)
    {
        int changedSlots = 0;

        foreach (Material material in materialsByName.Values)
        {
            foreach (string propertyName in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(propertyName);

                if (texture == null)
                {
                    continue;
                }

                string texturePath = AssetDatabase.GetAssetPath(texture);

                if (string.IsNullOrEmpty(texturePath) || !texturePath.StartsWith(sourceAssetFolder))
                {
                    continue;
                }

                string copiedTexturePath = texturePath.StartsWith(sourceAssetFolder)
                    ? $"{versionRoot}/{texturePath.Substring(sourceAssetFolder.Length).TrimStart('/')}"
                    : CopyExternalTextureToSnapshot(versionRoot, texturePath);

                Texture copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(copiedTexturePath);

                if (copiedTexture == null || copiedTexture == texture)
                {
                    continue;
                }

                material.SetTexture(propertyName, copiedTexture);
                EditorUtility.SetDirty(material);
                changedSlots++;
            }
        }

        return changedSlots;
    }

    private static int CopyMaterialsUsedBySourcePrefab(string sourcePrefabPath, string materialsFolder)
    {
        int copiedCount = 0;
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);

        if (sourcePrefab == null)
        {
            return 0;
        }

        foreach (Renderer renderer in sourcePrefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                string cleanName = RemoveUnityInstanceSuffix(material.name);
                string destinationPath = $"{materialsFolder}/{MakeSafeFileName(cleanName)}.mat";

                if (AssetDatabase.LoadAssetAtPath<Material>(destinationPath) != null)
                {
                    continue;
                }

                string sourcePath = AssetDatabase.GetAssetPath(material);

                if (!string.IsNullOrEmpty(sourcePath) && File.Exists(Path.GetFullPath(sourcePath)))
                {
                    if (AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    {
                        copiedCount++;
                    }
                }
                else
                {
                    Material copiedMaterial = new Material(material)
                    {
                        name = cleanName
                    };
                    AssetDatabase.CreateAsset(copiedMaterial, destinationPath);
                    copiedCount++;
                }
            }
        }

        return copiedCount;
    }

    private static Dictionary<string, Material> LoadMaterialsByName(string rootFolder)
    {
        var materialsByName = new Dictionary<string, Material>();
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { rootFolder });

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null)
            {
                materialsByName[material.name] = material;
            }
        }

        return materialsByName;
    }

    private static Dictionary<string, Mesh> LoadMeshesByName(string fbxPath)
    {
        var meshesByName = new Dictionary<string, Mesh>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

        foreach (UnityEngine.Object asset in assets)
        {
            Mesh mesh = asset as Mesh;

            if (mesh != null)
            {
                meshesByName[mesh.name] = mesh;
                string normalizedName = NormalizeMeshNameForFbxExporter(mesh.name);

                if (!meshesByName.ContainsKey(normalizedName))
                {
                    meshesByName[normalizedName] = mesh;
                }
            }
        }

        return meshesByName;
    }

    private static bool TryGetSnapshotMesh(Dictionary<string, Mesh> meshesByName, string sourceMeshName, out Mesh snapshotMesh)
    {
        if (meshesByName.TryGetValue(sourceMeshName, out snapshotMesh))
        {
            return true;
        }

        string normalizedName = NormalizeMeshNameForFbxExporter(sourceMeshName);
        return meshesByName.TryGetValue(normalizedName, out snapshotMesh);
    }

    private static string NormalizeMeshNameForFbxExporter(string meshName)
    {
        if (string.IsNullOrEmpty(meshName))
        {
            return meshName;
        }

        string normalized = meshName.Replace('.', '_').Replace('-', '_').Replace(' ', '_');

        if (char.IsDigit(normalized[0]))
        {
            normalized = "_" + normalized;
        }

        return normalized;
    }

    private static string CopyExternalTextureToSnapshot(string versionRoot, string texturePath)
    {
        string texturesFolder = $"{versionRoot}/Textures";
        CreateFolderRecursive(texturesFolder);

        string fileName = Path.GetFileName(texturePath);
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath($"{texturesFolder}/{MakeSafeFileName(fileName)}");

        if (AssetDatabase.LoadAssetAtPath<Texture>(destinationPath) == null)
        {
            AssetDatabase.CopyAsset(texturePath, destinationPath);
        }

        return destinationPath;
    }

    private static void PreserveExportedFbxHierarchyOnImport(string fbxPath)
    {
        ModelImporter modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;

        if (modelImporter == null)
        {
            Debug.LogWarning($"[AircraftPrefabIsolationUtility] 未找到 FBX 的 ModelImporter：{fbxPath}");
            return;
        }

        modelImporter.preserveHierarchy = true;
        modelImporter.sortHierarchyByName = false;
        modelImporter.SaveAndReimport();
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

    private static string GetNextVersionName()
    {
        CreateFolderRecursive(SavedVersionsRoot);

        int maxVersion = 0;
        string savedVersionsFullPath = Path.GetFullPath(SavedVersionsRoot);

        if (Directory.Exists(savedVersionsFullPath))
        {
            foreach (string directory in Directory.GetDirectories(savedVersionsFullPath, "B737_v*"))
            {
                string folderName = Path.GetFileName(directory);

                if (folderName.Length <= "B737_v".Length)
                {
                    continue;
                }

                string numberText = folderName.Substring("B737_v".Length);

                if (int.TryParse(numberText, out int parsedVersion))
                {
                    maxVersion = Mathf.Max(maxVersion, parsedVersion);
                }
            }
        }

        return $"B737_v{maxVersion + 1:000}";
    }

    private static void CopyFolderIfExists(string sourceFolder, string destinationFolder)
    {
        if (!AssetDatabase.IsValidFolder(sourceFolder))
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(sourceFolder, destinationFolder))
        {
            Debug.LogWarning($"[AircraftPrefabIsolationUtility] 文件夹复制失败：{sourceFolder} -> {destinationFolder}");
        }
    }

    private static string ExportUnityPackage(string versionRoot, string versionName)
    {
        string exportFolderFullPath = Path.Combine(Directory.GetCurrentDirectory(), PackageExportFolder);

        if (!Directory.Exists(exportFolderFullPath))
        {
            Directory.CreateDirectory(exportFolderFullPath);
        }

        string unityPackageFullPath = Path.Combine(exportFolderFullPath, $"{versionName}.unitypackage");

        AssetDatabase.ExportPackage(
            versionRoot,
            unityPackageFullPath,
            ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

        return unityPackageFullPath.Replace("\\", "/");
    }

    private static string GetPrefabAssetPath(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return path;
    }

    private static string ResolveSourceAssetFolder(string sourcePrefabPath)
    {
        if (string.Equals(sourcePrefabPath, DefaultSourcePrefabPath, StringComparison.OrdinalIgnoreCase) &&
            AssetDatabase.IsValidFolder(DefaultSourceAssetFolder))
        {
            return DefaultSourceAssetFolder;
        }

        string[] dependencies = AssetDatabase.GetDependencies(sourcePrefabPath, true);
        foreach (string dependency in dependencies)
        {
            if (!dependency.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string folder = Path.GetDirectoryName(dependency)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(folder))
            {
                continue;
            }

            string rootFolder = folder.EndsWith("/Models", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(folder)?.Replace("\\", "/")
                : folder;

            if (string.IsNullOrEmpty(rootFolder))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder($"{rootFolder}/Materials") ||
                AssetDatabase.IsValidFolder($"{rootFolder}/Textures") ||
                AssetDatabase.IsValidFolder($"{rootFolder}/Models/B737_Aircraft.fbm"))
            {
                return rootFolder;
            }
        }

        string prefabFolder = Path.GetDirectoryName(sourcePrefabPath)?.Replace("\\", "/");
        return string.IsNullOrEmpty(prefabFolder) ? "Assets" : prefabFolder;
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

    private static string MakeSafeFileName(string rawName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string safeName = rawName;

        foreach (char invalidChar in invalidChars)
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return string.IsNullOrEmpty(safeName) ? "Asset" : safeName;
    }

    private static string RemoveUnityInstanceSuffix(string materialName)
    {
        const string instanceSuffix = " (Instance)";
        return materialName.EndsWith(instanceSuffix)
            ? materialName.Substring(0, materialName.Length - instanceSuffix.Length)
            : materialName;
    }
}

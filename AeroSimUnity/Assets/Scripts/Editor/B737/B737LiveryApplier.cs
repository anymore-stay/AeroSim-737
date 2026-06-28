#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class B737LiveryApplier : EditorWindow
{
    private const string MenuPath = "Tools/B737/Apply Livery Folder";
    private const string GeneratedMaterialFolder = "Assets/Aircraft/B737/Materials/Generated_Livery_Materials";
    private const string ImportedLiveryFolder = "Assets/Aircraft/B737/Liveries";
    private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tga",
        ".tif",
        ".tiff",
        ".dds"
    };
    private static readonly Dictionary<string, string[]> TextureStemAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "738fuselage", new[] { "fuselage" } },
        { "738fuselage_NML", new[] { "fuselage_NML", "fuselage_normal", "fuselage-nml", "fuselage-normal" } },
        { "738fuselage_LIT", new[] { "fuselage_LIT", "fuselage_lit", "fuselage-lit" } },
        { "738tail", new[] { "tail" } },
        { "738tail_NML", new[] { "tail_NML", "tail_normal" } },
        { "738tail_LIT", new[] { "tail_LIT", "tail_lit", "tail-lit" } },
        { "738cfm56", new[] { "cfm56", "engine" } },
        { "738cfm56_NML", new[] { "cfm56_NML", "cfm56_normal", "engine_NML", "engine_normal" } },
        { "738wing", new[] { "wing" } },
        { "738wing_NML", new[] { "wing_NML", "wing_normal" } },
        { "738gear", new[] { "gear", "maingear", "nosegear" } },
        { "738gear_NML", new[] { "gear_NML", "gear_normal", "maingear_NML", "maingear_normal", "nosegear_NML", "nosegear_normal" } },
        { "738gear_LIT", new[] { "gear_LIT", "gear_lit", "maingear_LIT", "maingear_lit", "nosegear_LIT", "nosegear_lit" } },
        { "738alpha", new[] { "alpha" } },
        { "738alpha_LIT", new[] { "alpha_LIT", "alpha_lit" } }
    };
    private static readonly HashSet<string> PartialAtlasTextureStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "leftwing",
        "rightwing",
        "fin",
        "winglet"
    };

    private enum TextureCompatibilityStatus
    {
        Ready,
        UnsafePartialAtlas,
        Unknown
    }

    private enum ApplyScope
    {
        SelectedAircraftOnly,
        SharedProjectMaterials
    }

    private enum TextureSlot
    {
        Albedo,
        Normal,
        Lit
    }

    [Serializable]
    private sealed class LiveryPart
    {
        public readonly string DisplayName;
        public readonly string[] MaterialNames;
        public readonly string AlbedoFile;
        public readonly string NormalFile;
        public readonly string LitFile;
        public readonly string[] AlbedoAliases;
        public readonly string[] NormalAliases;
        public readonly string[] LitAliases;
        public bool Enabled;

        public LiveryPart(
            string displayName,
            string[] materialNames,
            string albedoFile,
            string normalFile = null,
            string litFile = null,
            bool enabled = true,
            string[] albedoAliases = null,
            string[] normalAliases = null,
            string[] litAliases = null)
        {
            DisplayName = displayName;
            MaterialNames = materialNames;
            AlbedoFile = albedoFile;
            NormalFile = normalFile;
            LitFile = litFile;
            AlbedoAliases = albedoAliases ?? new string[0];
            NormalAliases = normalAliases ?? new string[0];
            LitAliases = litAliases ?? new string[0];
            Enabled = enabled;
        }
    }

    private sealed class TextureMatch
    {
        public readonly string Path;
        public readonly string SearchTerm;
        public readonly bool IsDirectApplySafe;

        public TextureMatch(string path, string searchTerm, bool isDirectApplySafe)
        {
            Path = path;
            SearchTerm = searchTerm;
            IsDirectApplySafe = isDirectApplySafe;
        }

        public string FileName
        {
            get { return System.IO.Path.GetFileName(Path); }
        }
    }

    private sealed class DetectedTextureInfo
    {
        public readonly string Path;
        public readonly string FileName;
        public readonly string Role;
        public readonly string SizeText;
        public readonly TextureCompatibilityStatus Status;
        public readonly string Detail;

        public DetectedTextureInfo(
            string path,
            string role,
            string sizeText,
            TextureCompatibilityStatus status,
            string detail)
        {
            Path = path;
            FileName = System.IO.Path.GetFileName(path);
            Role = role;
            SizeText = sizeText;
            Status = status;
            Detail = detail;
        }
    }

    private readonly List<LiveryPart> parts = new List<LiveryPart>
    {
        new LiveryPart(
            "Fuselage / 机身",
            new[] { "XP_738fuselage" },
            "738fuselage.png",
            "738fuselage_NML.png",
            "738fuselage_LIT.png",
            true,
            new[] { "fuselage" },
            new[] { "fuselage_NML", "fuselage_normal" },
            new[] { "fuselage_LIT", "fuselage_lit" }),
        new LiveryPart(
            "Tail and Winglets / 尾翼与翼梢",
            new[] { "XP_738tailwinglets" },
            "738tail.png",
            "738tail_NML.png",
            "738tail_LIT.png",
            true,
            new[] { "tail" },
            new[] { "tail_NML", "tail_normal" },
            new[] { "tail_LIT", "tail_lit" }),
        new LiveryPart(
            "Engines / 发动机",
            new[] { "XP_738cfm56L", "XP_738cfm56R" },
            "738cfm56.png",
            "738cfm56_NML.png",
            null,
            true,
            new[] { "cfm56", "engine" },
            new[] { "cfm56_NML", "cfm56_normal", "engine_NML", "engine_normal" }),
        new LiveryPart(
            "Wings / 机翼",
            new[] { "XP_738wings" },
            "738wing.png",
            "738wing_NML.png",
            null,
            false,
            new[] { "wing" },
            new[] { "wing_NML", "wing_normal" }),
        new LiveryPart(
            "Landing Gear / 起落架",
            new[] { "XP_738maingear", "XP_738nosegear" },
            "738gear.png",
            "738gear_NML.png",
            "738gear_LIT.png",
            false,
            new[] { "gear", "maingear", "nosegear" },
            new[] { "gear_NML", "gear_normal", "maingear_NML", "maingear_normal", "nosegear_NML", "nosegear_normal" },
            new[] { "gear_LIT", "gear_lit", "maingear_LIT", "maingear_lit", "nosegear_LIT", "nosegear_lit" }),
        new LiveryPart(
            "Alpha / Exterior Small Parts / 透明与外部小部件",
            new[] { "XP_738alpha", "XP_738glass" },
            "738alpha.png",
            null,
            "738alpha_LIT.png",
            false,
            new[] { "alpha" },
            null,
            new[] { "alpha_LIT", "alpha_lit" }),
        new LiveryPart(
            "Main Cockpit Panel / 驾驶舱主面板",
            new[] { "XP_738_cockpit_main" },
            "738cockpit_main_panel.png",
            null,
            "738cockpit_main_panel_LIT.png",
            false)
    };

    private DefaultAsset liveryFolderAsset;
    private GameObject aircraftRoot;
    private ApplyScope applyScope = ApplyScope.SelectedAircraftOnly;
    private bool applyAlbedo = true;
    private bool applyNormal = true;
    private bool applyLit = false;
    private bool setTextureImportTypes = true;
    private Vector2 scroll;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        B737LiveryApplier window = GetWindow<B737LiveryApplier>("B737 Livery");
        window.minSize = new Vector2(560, 520);
        window.TryUseCurrentSelection();
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("B737-800 Livery Applier", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择一个包含 objects 贴图的涂装文件夹，然后按部位替换 737 材质。默认会复制材质并只应用到当前选中的飞机，适合场景里同时放多架不同涂装的飞机。",
            MessageType.Info);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Input / 输入", EditorStyles.boldLabel);
            aircraftRoot = (GameObject)EditorGUILayout.ObjectField(
                "Selected Aircraft Root",
                aircraftRoot,
                typeof(GameObject),
                true);
            liveryFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
                "Livery Objects Folder",
                liveryFolderAsset,
                typeof(DefaultAsset),
                false);

            if (GUILayout.Button("Use Current Selection / 使用当前选择"))
            {
                TryUseCurrentSelection();
            }
            if (GUILayout.Button("Choose Folder From Disk / 从磁盘选择涂装文件夹"))
            {
                ChooseLiveryFolderFromDisk();
            }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Mode / 模式", EditorStyles.boldLabel);
            applyScope = (ApplyScope)EditorGUILayout.EnumPopup("Apply Scope", applyScope);
            applyAlbedo = EditorGUILayout.ToggleLeft("Replace Albedo / 替换主贴图", applyAlbedo);
            applyNormal = EditorGUILayout.ToggleLeft("Replace Normal Map / 替换法线贴图", applyNormal);
            applyLit = EditorGUILayout.ToggleLeft("Replace LIT as Emission / 替换夜间发光贴图", applyLit);
            setTextureImportTypes = EditorGUILayout.ToggleLeft("Auto set texture import type / 自动设置贴图导入类型", setTextureImportTypes);
        }

        DrawParts();
        DrawPreview();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Exterior Defaults / 选择常用外部涂装", GUILayout.Height(28)))
            {
                SelectExteriorDefaults();
            }
            if (GUILayout.Button("Auto Select Found Parts", GUILayout.Height(28)))
            {
                SelectPartsWithDetectedTextures();
            }
            if (GUILayout.Button("Select All / 全选", GUILayout.Height(28)))
            {
                SetAllParts(true);
            }
            if (GUILayout.Button("Clear / 清空", GUILayout.Height(28)))
            {
                SetAllParts(false);
            }
        }

        EditorGUILayout.Space();
        GUI.enabled = CanApply();
        if (GUILayout.Button("Apply Livery / 应用涂装", GUILayout.Height(36)))
        {
            ApplyLivery();
        }
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    private void DrawParts()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Parts / 局部替换部位", EditorStyles.boldLabel);
            foreach (LiveryPart part in parts)
            {
                part.Enabled = EditorGUILayout.ToggleLeft(part.DisplayName, part.Enabled);
            }
        }
    }

    private void DrawPreview()
    {
        string folder = GetLiveryFolderPath();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Texture Detection / 贴图识别", EditorStyles.boldLabel);
            if (string.IsNullOrEmpty(folder))
            {
                EditorGUILayout.HelpBox("请选择 Assets 里的涂装 objects 文件夹。", MessageType.Warning);
                return;
            }

            foreach (LiveryPart part in parts)
            {
                if (!part.Enabled)
                {
                    continue;
                }

                EditorGUILayout.LabelField(part.DisplayName, EditorStyles.miniBoldLabel);
                DrawTextureStatus(folder, "Albedo", part.AlbedoFile, part.AlbedoAliases, TextureSlot.Albedo, applyAlbedo);
                DrawTextureStatus(folder, "Normal", part.NormalFile, part.NormalAliases, TextureSlot.Normal, applyNormal);
                DrawTextureStatus(folder, "LIT", part.LitFile, part.LitAliases, TextureSlot.Lit, applyLit);
            }

            DrawDetectedTextureInventory(folder);
        }
    }

    private void DrawDetectedTextureInventory(string folder)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detected Textures In Folder", EditorStyles.miniBoldLabel);
        List<DetectedTextureInfo> detectedTextures = FindTextureAssetPaths(folder)
            .Select(ClassifyDetectedTexture)
            .OrderBy(item => item.Status)
            .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (detectedTextures.Count == 0)
        {
            EditorGUILayout.HelpBox("No texture files found in this folder.", MessageType.Warning);
            return;
        }

        foreach (DetectedTextureInfo texture in detectedTextures.Take(40))
        {
            MessageType messageType = texture.Status == TextureCompatibilityStatus.UnsafePartialAtlas
                ? MessageType.Warning
                : MessageType.Info;
            EditorGUILayout.HelpBox(
                $"{texture.FileName} | {texture.Role} | {texture.SizeText} | {texture.Detail}",
                messageType);
        }

        if (detectedTextures.Count > 40)
        {
            EditorGUILayout.HelpBox($"Showing first 40 of {detectedTextures.Count} texture files.", MessageType.None);
        }
    }

    private void DrawTextureStatus(
        string folder,
        string slot,
        string fileName,
        string[] aliases,
        TextureSlot textureSlot,
        bool slotEnabled)
    {
        if (!slotEnabled || string.IsNullOrEmpty(fileName))
        {
            return;
        }

        TextureMatch match = FindTextureMatch(folder, fileName, aliases, textureSlot);
        if (match == null)
        {
            EditorGUILayout.HelpBox($"{slot}: {fileName} - missing", MessageType.None);
            return;
        }

        EditorGUILayout.HelpBox($"{slot}: found {match.FileName} (match: {match.SearchTerm})", MessageType.Info);
    }

    private void TryUseCurrentSelection()
    {
        if (Selection.activeGameObject != null)
        {
            aircraftRoot = Selection.activeGameObject;
        }

        UnityEngine.Object selected = Selection.activeObject;
        string selectedPath = selected == null ? "" : AssetDatabase.GetAssetPath(selected);
        if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
        {
            liveryFolderAsset = selected as DefaultAsset;
        }
    }

    private void ChooseLiveryFolderFromDisk()
    {
        string selectedFolder = EditorUtility.OpenFolderPanel(
            "Choose B737 livery objects folder",
            "",
            "");
        if (string.IsNullOrEmpty(selectedFolder))
        {
            return;
        }

        selectedFolder = selectedFolder.Replace("\\", "/");
        string assetPath = TryConvertAbsolutePathToAssetPath(selectedFolder);
        if (string.IsNullOrEmpty(assetPath))
        {
            assetPath = CopyExternalLiveryFolderIntoAssets(selectedFolder);
        }

        AssetDatabase.Refresh();
        assetPath = ResolveAssetObjectsFolder(assetPath);
        liveryFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
        if (liveryFolderAsset == null)
        {
            EditorUtility.DisplayDialog(
                "B737 Livery Applier",
                "已选择文件夹，但 Unity 没有成功导入为 Assets 文件夹。请确认里面包含贴图文件。",
                "OK");
        }
    }

    private static string TryConvertAbsolutePathToAssetPath(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace("\\", "/");
        if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return "Assets" + absolutePath.Substring(dataPath.Length);
    }

    private static string ResolveAssetObjectsFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return assetPath;
        }

        string normalized = assetPath.Replace("\\", "/").TrimEnd('/');
        if (Path.GetFileName(normalized).Equals("objects", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        string objectsPath = normalized + "/objects";
        return AssetDatabase.IsValidFolder(objectsPath) ? objectsPath : normalized;
    }

    private static string CopyExternalLiveryFolderIntoAssets(string selectedFolder)
    {
        DirectoryInfo selectedInfo = new DirectoryInfo(selectedFolder);
        DirectoryInfo sourceInfo = ResolveObjectsFolder(selectedInfo);
        string liveryName = sourceInfo.Name.Equals("objects", StringComparison.OrdinalIgnoreCase) && sourceInfo.Parent != null
            ? sourceInfo.Parent.Name
            : sourceInfo.Name;
        string safeName = SanitizeFileName(liveryName);
        string targetAssetFolder = ImportedLiveryFolder + "/" + safeName + "/objects";
        string targetAbsoluteFolder = ToProjectAbsolutePath(targetAssetFolder);
        Directory.CreateDirectory(targetAbsoluteFolder);

        foreach (string sourceFile in Directory.GetFiles(sourceInfo.FullName, "*.*", SearchOption.AllDirectories))
        {
            if (!TextureExtensions.Contains(Path.GetExtension(sourceFile)))
            {
                continue;
            }

            string relative = sourceFile.Substring(sourceInfo.FullName.Length).TrimStart('\\', '/');
            string targetAbsolutePath = Path.Combine(targetAbsoluteFolder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolutePath));
            File.Copy(sourceFile, targetAbsolutePath, true);
        }

        return targetAssetFolder;
    }

    private static DirectoryInfo ResolveObjectsFolder(DirectoryInfo selectedInfo)
    {
        if (selectedInfo.Name.Equals("objects", StringComparison.OrdinalIgnoreCase))
        {
            return selectedInfo;
        }

        DirectoryInfo objectsInfo = new DirectoryInfo(Path.Combine(selectedInfo.FullName, "objects"));
        return objectsInfo.Exists ? objectsInfo : selectedInfo;
    }

    private static string ToProjectAbsolutePath(string assetPath)
    {
        string normalizedAssetPath = assetPath.Replace("\\", "/").TrimStart('/');
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", normalizedAssetPath));
    }

    private bool CanApply()
    {
        if (applyScope == ApplyScope.SelectedAircraftOnly && aircraftRoot == null)
        {
            return false;
        }

        return !string.IsNullOrEmpty(GetLiveryFolderPath()) && parts.Any(part => part.Enabled);
    }

    private void ApplyLivery()
    {
        string folder = GetLiveryFolderPath();
        if (string.IsNullOrEmpty(folder))
        {
            EditorUtility.DisplayDialog("B737 Livery Applier", "请选择一个 Assets 内的涂装 objects 文件夹。", "OK");
            return;
        }

        if (applyScope == ApplyScope.SelectedAircraftOnly && aircraftRoot == null)
        {
            EditorUtility.DisplayDialog("B737 Livery Applier", "请选择场景里的飞机根物体。", "OK");
            return;
        }

        ApplyResult result = new ApplyResult();
        EnsureMaterialFolderExists();

        foreach (LiveryPart part in parts.Where(item => item.Enabled))
        {
            ApplyPart(folder, part, result);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "B737 Livery Applier",
            $"涂装应用完成。\n已处理材质: {result.ChangedMaterials}\n跳过材质: {result.MissingMaterials}\n缺少贴图: {result.MissingTextures}\n跳过部位: {result.SkippedParts}\n复制材质: {result.ClonedMaterials}\n\n{result.ReportText()}",
            "OK");
    }

    private void ApplyPart(string folder, LiveryPart part, ApplyResult result)
    {
        Texture2D albedo = LoadOptionalTexture(folder, part.AlbedoFile, part.AlbedoAliases, TextureSlot.Albedo, applyAlbedo, false, result);
        Texture2D normal = LoadOptionalTexture(folder, part.NormalFile, part.NormalAliases, TextureSlot.Normal, applyNormal, true, result);
        Texture2D lit = LoadOptionalTexture(folder, part.LitFile, part.LitAliases, TextureSlot.Lit, applyLit, false, result);

        if (!HasAnyTexture(albedo, normal, lit))
        {
            result.SkippedParts++;
            result.AddLine($"No selected textures found: {part.DisplayName}");
            return;
        }

        foreach (string materialName in part.MaterialNames)
        {
            List<MaterialTarget> targets = FindMaterialTargets(materialName);
            if (targets.Count == 0)
            {
                result.MissingMaterials++;
                result.AddLine($"未找到材质: {materialName}");
                continue;
            }

            foreach (MaterialTarget target in targets)
            {
                Material material = ResolveEditableMaterial(target, materialName, result);
                if (material == null)
                {
                    result.MissingMaterials++;
                    result.AddLine($"无法编辑材质: {materialName}");
                    continue;
                }

                ApplyTextures(material, albedo, normal, lit);
                EditorUtility.SetDirty(material);
                result.ChangedMaterials++;
                result.AddLine($"已替换: {material.name}");
            }
        }
    }

    private static bool HasAnyTexture(Texture2D albedo, Texture2D normal, Texture2D lit)
    {
        return albedo != null || normal != null || lit != null;
    }

    private Texture2D LoadOptionalTexture(
        string folder,
        string fileName,
        string[] aliases,
        TextureSlot textureSlot,
        bool enabled,
        bool normalMap,
        ApplyResult result)
    {
        if (!enabled || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        TextureMatch match = FindTextureMatch(folder, fileName, aliases, textureSlot);
        if (match == null)
        {
            result.MissingTextures++;
            result.AddLine($"缺少贴图: {fileName}");
            return null;
        }

        if (setTextureImportTypes)
        {
            FixTextureImporter(match.Path, normalMap);
        }

        result.AddLine($"Texture {fileName} => {match.FileName}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(match.Path);
    }

    private List<MaterialTarget> FindMaterialTargets(string materialName)
    {
        if (applyScope == ApplyScope.SelectedAircraftOnly)
        {
            return FindMaterialTargetsInAircraft(materialName);
        }

        return FindSharedProjectMaterials(materialName)
            .Select(material => new MaterialTarget(null, -1, material))
            .ToList();
    }

    private List<MaterialTarget> FindMaterialTargetsInAircraft(string materialName)
    {
        List<MaterialTarget> targets = new List<MaterialTarget>();
        Renderer[] renderers = aircraftRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] sharedMaterials = renderer.sharedMaterials;
            for (int index = 0; index < sharedMaterials.Length; index++)
            {
                Material material = sharedMaterials[index];
                if (material != null && MaterialNameMatches(material, materialName))
                {
                    targets.Add(new MaterialTarget(renderer, index, material));
                }
            }
        }
        return targets;
    }

    private List<Material> FindSharedProjectMaterials(string materialName)
    {
        string[] guids = AssetDatabase.FindAssets(materialName + " t:Material");
        List<Material> materials = new List<Material>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && MaterialNameMatches(material, materialName))
            {
                materials.Add(material);
            }
        }
        return materials;
    }

    private Material ResolveEditableMaterial(MaterialTarget target, string materialName, ApplyResult result)
    {
        if (applyScope == ApplyScope.SharedProjectMaterials)
        {
            return target.Material;
        }

        string clonePath = CreateMaterialClonePath(materialName);
        Material editable = AssetDatabase.LoadAssetAtPath<Material>(clonePath);
        if (editable == null)
        {
            editable = new Material(target.Material);
            editable.name = materialName + "_" + SanitizeFileName(GetLiveryName()) + "_Livery";
            AssetDatabase.CreateAsset(editable, clonePath);
            result.ClonedMaterials++;
        }

        Material[] materials = target.Renderer.sharedMaterials;
        materials[target.MaterialIndex] = editable;
        target.Renderer.sharedMaterials = materials;
        EditorUtility.SetDirty(target.Renderer);
        return editable;
    }

    private string CreateMaterialClonePath(string materialName)
    {
        string liveryName = SanitizeFileName(GetLiveryName());
        string safeMaterialName = SanitizeFileName(materialName);
        return $"{GeneratedMaterialFolder}/{liveryName}_{safeMaterialName}.mat";
    }

    private string GetLiveryName()
    {
        string folder = GetLiveryFolderPath();
        if (string.IsNullOrEmpty(folder))
        {
            return "Livery";
        }

        string trimmed = folder.TrimEnd('/', '\\');
        string folderName = Path.GetFileName(trimmed);
        if (folderName.Equals("objects", StringComparison.OrdinalIgnoreCase))
        {
            string parent = Path.GetDirectoryName(trimmed);
            if (!string.IsNullOrEmpty(parent))
            {
                return Path.GetFileName(parent);
            }
        }

        return folderName;
    }

    private static bool MaterialNameMatches(Material material, string expectedName)
    {
        string name = NormalizeMaterialName(material.name);
        expectedName = NormalizeMaterialName(expectedName);
        return NameContainsMaterialToken(name, expectedName);
    }

    private static string NormalizeMaterialName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "";
        }

        string normalized = name.Trim();
        const string instanceSuffix = " (Instance)";
        if (normalized.EndsWith(instanceSuffix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - instanceSuffix.Length);
        }

        return normalized;
    }

    private static bool NameContainsMaterialToken(string name, string expectedName)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(expectedName))
        {
            return false;
        }

        return name.Equals(expectedName, StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith(expectedName + "_", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_" + expectedName, StringComparison.OrdinalIgnoreCase) ||
               name.IndexOf("_" + expectedName + "_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyTextures(Material material, Texture2D albedo, Texture2D normal, Texture2D lit)
    {
        EnsureCompatibleShader(material);

        if (albedo != null)
        {
            SetTextureIfPropertyExists(material, "_BaseMap", albedo);
            SetTextureIfPropertyExists(material, "_MainTex", albedo);
            SetColorIfPropertyExists(material, "_BaseColor", Color.white);
            SetColorIfPropertyExists(material, "_Color", Color.white);
        }

        if (normal != null)
        {
            SetTextureIfPropertyExists(material, "_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        if (lit != null)
        {
            SetTextureIfPropertyExists(material, "_EmissionMap", lit);
            SetColorIfPropertyExists(material, "_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    private static void EnsureCompatibleShader(Material material)
    {
        Shader shader = FindCompatibleLitShader();
        if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        SetFloatIfPropertyExists(material, "_Metallic", 0.0f);
        SetFloatIfPropertyExists(material, "_Smoothness", 0.45f);
        SetFloatIfPropertyExists(material, "_Glossiness", 0.45f);
        SetFloatIfPropertyExists(material, "_Surface", 0.0f);
        SetFloatIfPropertyExists(material, "_Blend", 0.0f);
        SetFloatIfPropertyExists(material, "_AlphaClip", 0.0f);
        SetFloatIfPropertyExists(material, "_SrcBlend", (float)BlendMode.One);
        SetFloatIfPropertyExists(material, "_DstBlend", (float)BlendMode.Zero);
        SetFloatIfPropertyExists(material, "_ZWrite", 1.0f);
        material.renderQueue = -1;
    }

    private static Shader FindCompatibleLitShader()
    {
        bool hasRenderPipelineAsset = GraphicsSettings.currentRenderPipeline != null;
        string[] srpFirst =
        {
            "B737/UnlitTexture",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Tuanjie Render Pipeline/Lit",
            "Tuanjie/Universal Render Pipeline/Lit",
            "Standard",
            "Diffuse"
        };
        string[] builtinFirst =
        {
            "B737/UnlitTexture",
            "Standard",
            "Diffuse",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Tuanjie Render Pipeline/Lit",
            "Tuanjie/Universal Render Pipeline/Lit"
        };

        foreach (string shaderName in hasRenderPipelineAsset ? srpFirst : builtinFirst)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private string GetLiveryFolderPath()
    {
        if (liveryFolderAsset == null)
        {
            return "";
        }

        string path = AssetDatabase.GetAssetPath(liveryFolderAsset);
        return AssetDatabase.IsValidFolder(path) ? path : "";
    }

    private static string FindTexturePath(string folder, string fileName)
    {
        TextureMatch match = FindTextureMatch(folder, fileName, null, GuessTextureSlot(fileName));
        return match == null || !match.IsDirectApplySafe ? null : match.Path;
    }

    private static TextureMatch FindTextureMatch(
        string folder,
        string fileName,
        string[] aliases,
        TextureSlot textureSlot)
    {
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        string[] searchTerms = BuildTextureSearchTerms(fileName, aliases);
        foreach (string term in searchTerms)
        {
            TextureMatch directMatch = FindDirectTextureMatch(folder, term, textureSlot);
            if (directMatch != null && directMatch.IsDirectApplySafe)
            {
                return directMatch;
            }
        }

        List<string> texturePaths = FindTextureAssetPaths(folder);
        foreach (string term in searchTerms)
        {
            TextureMatch exactStemMatch = FindTextureMatchByStem(texturePaths, term, textureSlot, false);
            if (exactStemMatch != null && exactStemMatch.IsDirectApplySafe)
            {
                return exactStemMatch;
            }
        }

        foreach (string term in searchTerms)
        {
            TextureMatch compatibleStemMatch = FindTextureMatchByStem(texturePaths, term, textureSlot, true);
            if (compatibleStemMatch != null && compatibleStemMatch.IsDirectApplySafe)
            {
                return compatibleStemMatch;
            }
        }

        return null;
    }

    private static TextureSlot GuessTextureSlot(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        if (IsLitStem(stem))
        {
            return TextureSlot.Lit;
        }

        if (IsNormalStem(stem))
        {
            return TextureSlot.Normal;
        }

        return TextureSlot.Albedo;
    }

    private static DetectedTextureInfo ClassifyDetectedTexture(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        string baseStem = GetBaseTextureStem(stem);
        string role = DetectTextureRole(baseStem);
        TextureCompatibilityStatus status;
        string detail;

        if (PartialAtlasTextureStems.Contains(baseStem))
        {
            status = TextureCompatibilityStatus.UnsafePartialAtlas;
            detail = "Not safe direct atlas replacement";
        }
        else if (role == "Unknown")
        {
            status = TextureCompatibilityStatus.Unknown;
            detail = "Detected, but no mapped B737 part";
        }
        else
        {
            status = TextureCompatibilityStatus.Ready;
            detail = "Can apply directly";
        }

        return new DetectedTextureInfo(path, role, GetTextureSizeText(path), status, detail);
    }

    private static string DetectTextureRole(string baseStem)
    {
        if (StemIsAny(baseStem, "738fuselage", "fuselage")) return "Fuselage";
        if (StemIsAny(baseStem, "738tail", "tail")) return "Tail atlas";
        if (StemIsAny(baseStem, "738wing", "wing")) return "Wing atlas";
        if (StemIsAny(baseStem, "738cfm56", "cfm56", "engine")) return "Engines";
        if (StemIsAny(baseStem, "738gear", "gear", "maingear", "nosegear")) return "Landing gear";
        if (StemIsAny(baseStem, "738alpha", "alpha")) return "Alpha/exterior small parts";
        if (StemIsAny(baseStem, "leftwing", "rightwing")) return "Partial wing texture";
        if (StemIsAny(baseStem, "fin", "winglet")) return "Partial tail/winglet texture";
        if (baseStem.IndexOf("seat", StringComparison.OrdinalIgnoreCase) >= 0) return "Cabin seats";
        return "Unknown";
    }

    private static bool StemIsAny(string stem, params string[] values)
    {
        return values.Any(value => stem.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetTextureSizeText(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return "size unknown";
        }

        int width;
        int height;
        importer.GetSourceTextureWidthAndHeight(out width, out height);
        return width > 0 && height > 0 ? $"{width}x{height}" : "size unknown";
    }

    private static string[] BuildTextureSearchTerms(string fileName, string[] aliases)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return new string[0];
        }

        string stem = Path.GetFileNameWithoutExtension(fileName);
        List<string> terms = new List<string>();
        AddSearchTerm(terms, stem);

        string[] tableAliases;
        if (TextureStemAliases.TryGetValue(stem, out tableAliases))
        {
            AddSearchTerms(terms, tableAliases);
        }

        AddSearchTerms(terms, aliases);
        return terms.ToArray();
    }

    private static void AddSearchTerms(List<string> terms, string[] values)
    {
        if (values == null)
        {
            return;
        }

        foreach (string value in values)
        {
            AddSearchTerm(terms, value);
        }
    }

    private static void AddSearchTerm(List<string> terms, string value)
    {
        if (string.IsNullOrEmpty(value) || terms.Any(item => item.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        terms.Add(value);
    }

    private static TextureMatch FindDirectTextureMatch(string folder, string searchTerm, TextureSlot textureSlot)
    {
        foreach (string extension in TextureExtensions)
        {
            string path = Path.Combine(folder, searchTerm + extension).Replace("\\", "/");
            if (File.Exists(path) && TextureNameAllowed(searchTerm, textureSlot))
            {
                return new TextureMatch(path, searchTerm, IsDirectApplySafeMatch(searchTerm, searchTerm));
            }
        }

        return null;
    }

    private static List<string> FindTextureAssetPaths(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        List<string> paths = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string pathFileName = Path.GetFileName(path);
            if (TextureExtensions.Contains(Path.GetExtension(pathFileName)))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static TextureMatch FindTextureMatchByStem(
        List<string> texturePaths,
        string searchTerm,
        TextureSlot textureSlot,
        bool allowCompatiblePrefix)
    {
        foreach (string path in texturePaths)
        {
            string candidateStem = Path.GetFileNameWithoutExtension(path);
            bool matches = allowCompatiblePrefix
                ? TextureStemMatches(candidateStem, searchTerm)
                : candidateStem.Equals(searchTerm, StringComparison.OrdinalIgnoreCase);
            if (matches && TextureNameAllowed(candidateStem, textureSlot))
            {
                return new TextureMatch(path, searchTerm, IsDirectApplySafeMatch(candidateStem, searchTerm));
            }
        }

        return null;
    }

    private static bool IsDirectApplySafeMatch(string candidateStem, string searchTerm)
    {
        string candidateBaseStem = GetBaseTextureStem(candidateStem);
        string expectedBaseStem = GetBaseTextureStem(searchTerm);
        return !PartialAtlasTextureStems.Contains(candidateBaseStem) &&
               !PartialAtlasTextureStems.Contains(expectedBaseStem);
    }

    private static bool TextureNameAllowed(string candidateStem, TextureSlot textureSlot)
    {
        switch (textureSlot)
        {
            case TextureSlot.Albedo:
                return !IsNormalStem(candidateStem) && !IsLitStem(candidateStem);
            case TextureSlot.Normal:
                return IsNormalStem(candidateStem);
            case TextureSlot.Lit:
                return IsLitStem(candidateStem);
            default:
                return true;
        }
    }

    private static string GetBaseTextureStem(string stem)
    {
        if (string.IsNullOrEmpty(stem))
        {
            return "";
        }

        string[] suffixes =
        {
            "_NML",
            "-NML",
            "_NORMAL",
            "-NORMAL",
            "_NORMALMAP",
            "-NORMALMAP",
            "_LIT",
            "-LIT",
            "_EMISSIVE",
            "-EMISSIVE",
            "_EMISSION",
            "-EMISSION"
        };

        foreach (string suffix in suffixes)
        {
            if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return stem.Substring(0, stem.Length - suffix.Length);
            }
        }

        return stem;
    }

    private static bool IsNormalStem(string stem)
    {
        return stem.EndsWith("_NML", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("-NML", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("_NORMAL", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("-NORMAL", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("_NORMALMAP", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("-NORMALMAP", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLitStem(string stem)
    {
        return stem.EndsWith("_LIT", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("-LIT", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("_EMISSIVE", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("-EMISSIVE", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("_EMISSION", StringComparison.OrdinalIgnoreCase) ||
               stem.EndsWith("-EMISSION", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TextureStemMatches(string candidateStem, string expectedStem)
    {
        if (candidateStem.Equals(expectedStem, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!candidateStem.StartsWith(expectedStem + "_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = candidateStem.Substring(expectedStem.Length);
        bool expectedIsSpecialMap =
            expectedStem.EndsWith("_NML", StringComparison.OrdinalIgnoreCase) ||
            expectedStem.EndsWith("_LIT", StringComparison.OrdinalIgnoreCase);
        if (!expectedIsSpecialMap &&
            (suffix.StartsWith("_NML", StringComparison.OrdinalIgnoreCase) ||
             suffix.StartsWith("_NORMAL", StringComparison.OrdinalIgnoreCase) ||
             suffix.StartsWith("_LIT", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static void FixTextureImporter(string path, bool normalMap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        TextureImporterType desiredType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != desiredType)
        {
            importer.textureType = desiredType;
            changed = true;
        }

        if (!normalMap && !importer.sRGBTexture)
        {
            importer.sRGBTexture = true;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void EnsureMaterialFolderExists()
    {
        string[] parts = GeneratedMaterialFolder.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }
            current = next;
        }
    }

    private void SelectExteriorDefaults()
    {
        foreach (LiveryPart part in parts)
        {
            part.Enabled =
                part.MaterialNames.Contains("XP_738fuselage") ||
                part.MaterialNames.Contains("XP_738tailwinglets") ||
                part.MaterialNames.Contains("XP_738cfm56L") ||
                part.MaterialNames.Contains("XP_738cfm56R");
        }
    }

    private void SelectPartsWithDetectedTextures()
    {
        string folder = GetLiveryFolderPath();
        if (string.IsNullOrEmpty(folder))
        {
            EditorUtility.DisplayDialog("B737 Livery Applier", "Choose a livery objects folder first.", "OK");
            return;
        }

        foreach (LiveryPart part in parts)
        {
            part.Enabled = PartHasCompatibleDetectedTexture(folder, part);
        }
    }

    private bool PartHasDetectedTexture(string folder, LiveryPart part)
    {
        return PartHasCompatibleDetectedTexture(folder, part);
    }

    private bool PartHasCompatibleDetectedTexture(string folder, LiveryPart part)
    {
        return
            (applyAlbedo && HasSafeTextureMatch(folder, part.AlbedoFile, part.AlbedoAliases, TextureSlot.Albedo)) ||
            (applyNormal && HasSafeTextureMatch(folder, part.NormalFile, part.NormalAliases, TextureSlot.Normal)) ||
            (applyLit && HasSafeTextureMatch(folder, part.LitFile, part.LitAliases, TextureSlot.Lit));
    }

    private static bool HasSafeTextureMatch(string folder, string fileName, string[] aliases, TextureSlot textureSlot)
    {
        TextureMatch match = FindTextureMatch(folder, fileName, aliases, textureSlot);
        return match != null && match.IsDirectApplySafe;
    }

    private void SetAllParts(bool enabled)
    {
        foreach (LiveryPart part in parts)
        {
            part.Enabled = enabled;
        }
    }

    private static string SanitizeFileName(string value)
    {
        string sanitized = value;
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '_');
        }
        return string.IsNullOrEmpty(sanitized) ? "Livery" : sanitized;
    }

    private static void SetTextureIfPropertyExists(Material material, string propertyName, Texture texture)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfPropertyExists(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloatIfPropertyExists(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private sealed class MaterialTarget
    {
        public readonly Renderer Renderer;
        public readonly int MaterialIndex;
        public readonly Material Material;

        public MaterialTarget(Renderer renderer, int materialIndex, Material material)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
            Material = material;
        }
    }

    private sealed class ApplyResult
    {
        public int ChangedMaterials;
        public int MissingMaterials;
        public int MissingTextures;
        public int SkippedParts;
        public int ClonedMaterials;
        private readonly List<string> lines = new List<string>();

        public void AddLine(string line)
        {
            if (lines.Count < 18)
            {
                lines.Add(line);
            }
        }

        public string ReportText()
        {
            if (lines.Count == 0)
            {
                return "没有详细信息。";
            }

            return string.Join("\n", lines.ToArray());
        }
    }
}
#endif

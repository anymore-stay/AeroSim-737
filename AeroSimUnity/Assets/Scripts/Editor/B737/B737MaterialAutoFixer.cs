#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class B737MaterialAutoFixer
{
    private const string MenuPath = "Tools/B737/Fix Materials From Manifest";

    private sealed class MaterialSlots
    {
        public readonly List<string> Albedo = new List<string>();
        public readonly List<string> Normal = new List<string>();
        public readonly List<string> Lit = new List<string>();
    }

    [MenuItem(MenuPath)]
    public static void FixMaterialsFromManifest()
    {
        string manifestPath = FindManifestPath();
        if (string.IsNullOrEmpty(manifestPath))
        {
            if (FixMaterialsWithoutManifest())
            {
                return;
            }

            EditorUtility.DisplayDialog(
                "B737 Material Fixer",
                "没有找到 Boeing_B737-800_visual_cockpit_material_texture_manifest.csv，也没有找到可自动修复的 B737 材质目录。",
                "OK");
            return;
        }

        string root = Path.GetDirectoryName(manifestPath).Replace("\\", "/");
        Dictionary<string, MaterialSlots> manifest = ReadManifest(manifestPath);

        FixTextureImportSettings(root);

        int fixedMaterials = 0;
        int missingMaterials = 0;
        int missingTextures = 0;

        foreach (KeyValuePair<string, MaterialSlots> entry in manifest)
        {
            Material material = FindMaterial(root, entry.Key);
            if (material == null)
            {
                missingMaterials++;
                continue;
            }

            MaterialSlots slots = entry.Value;
            Texture2D albedo = FindTexture(root, slots.Albedo.FirstOrDefault());
            Texture2D normal = FindTexture(root, slots.Normal.FirstOrDefault());
            Texture2D lit = FindTexture(root, slots.Lit.FirstOrDefault());

            if (slots.Albedo.Count > 0 && albedo == null) missingTextures++;
            if (slots.Normal.Count > 0 && normal == null) missingTextures++;
            if (slots.Lit.Count > 0 && lit == null) missingTextures++;

            ApplyTextures(material, albedo, normal, lit);
            EditorUtility.SetDirty(material);
            fixedMaterials++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "B737 Material Fixer",
            $"材质修复完成。\n已处理材质: {fixedMaterials}\n未找到材质: {missingMaterials}\n未找到贴图: {missingTextures}\n\n如果未找到材质很多，请先选中 FBX，在 Inspector 的 Materials 页里 Extract Materials。",
            "OK");
    }

    private static bool FixMaterialsWithoutManifest()
    {
        List<string> roots = FindFallbackRoots();
        if (roots.Count == 0)
        {
            return false;
        }

        int fixedMaterials = 0;
        int fixedTextures = 0;

        foreach (string root in roots)
        {
            FixTextureImportSettings(root);

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { root });
            foreach (string guid in materialGuids)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    continue;
                }

                ApplyExistingTexturesToCompatibleShader(material);
                EditorUtility.SetDirty(material);
                fixedMaterials++;
            }

            fixedTextures += AssetDatabase.FindAssets("t:Texture2D", new[] { root }).Length;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "B737 Material Fixer",
            $"没有找到 manifest，已改用当前项目目录自动修复。\n已处理材质: {fixedMaterials}\n已扫描贴图: {fixedTextures}\n目录数: {roots.Count}\n\n如果场景里旧飞机仍然发紫，再运行一次 Tools/B737/Replace Scene Aircraft Materials。",
            "OK");

        return true;
    }

    private static string FindManifestPath()
    {
        string[] guids = AssetDatabase.FindAssets("Boeing_B737-800_visual_cockpit_material_texture_manifest t:TextAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("Boeing_B737-800_visual_cockpit_material_texture_manifest.csv", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        string fallback = "Assets/Unity_Export_v2_Visual_Cockpit/Boeing_B737-800_visual_cockpit_material_texture_manifest.csv";
        return File.Exists(fallback) ? fallback : null;
    }

    private static List<string> FindFallbackRoots()
    {
        HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in materialGuids)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
            if (string.IsNullOrEmpty(materialPath) || materialPath.IndexOf("/materials/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            string materialsFolder = Path.GetDirectoryName(materialPath).Replace("\\", "/");
            string root = Path.GetDirectoryName(materialsFolder).Replace("\\", "/");
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }

            bool hasTextures = AssetDatabase.IsValidFolder(root + "/Textures") ||
                               AssetDatabase.IsValidFolder(root + "/Textures_All");
            bool hasFbm = Directory.Exists(Path.Combine(root, "B737_Aircraft.fbm")) ||
                          Directory.Exists(Path.Combine(root, "Models", "B737_Aircraft.fbm"));
            if (hasTextures || hasFbm)
            {
                roots.Add(root);
            }
        }

        return roots.OrderBy(path => path).ToList();
    }

    private static Dictionary<string, MaterialSlots> ReadManifest(string manifestPath)
    {
        Dictionary<string, MaterialSlots> result = new Dictionary<string, MaterialSlots>();
        foreach (string rawLine in File.ReadAllLines(manifestPath))
        {
            string line = rawLine.Trim('\uFEFF', ' ', '\t', '\r', '\n');
            if (string.IsNullOrEmpty(line) || line.StartsWith("material,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = line.Split(',');
            if (parts.Length < 3)
            {
                continue;
            }

            string materialName = parts[0].Trim();
            string slot = parts[1].Trim().ToLowerInvariant();
            string[] textures = parts[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrEmpty(item))
                .ToArray();

            if (!result.TryGetValue(materialName, out MaterialSlots slots))
            {
                slots = new MaterialSlots();
                result[materialName] = slots;
            }

            if (slot == "albedo") slots.Albedo.AddRange(textures);
            if (slot == "normal") slots.Normal.AddRange(textures);
            if (slot == "lit") slots.Lit.AddRange(textures);
        }

        return result;
    }

    private static void FixTextureImportSettings(string root)
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            string fileName = Path.GetFileName(path);
            bool changed = false;

            if (fileName.IndexOf("_NML", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("_normal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
            }
            else
            {
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                }
                if (!importer.sRGBTexture)
                {
                    importer.sRGBTexture = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static Material FindMaterial(string root, string materialName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { root });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.name == materialName)
            {
                return material;
            }
        }

        return null;
    }

    private static Texture2D FindTexture(string root, string textureName)
    {
        if (string.IsNullOrEmpty(textureName))
        {
            return null;
        }

        string textureStem = Path.GetFileNameWithoutExtension(textureName);
        string[] guids = AssetDatabase.FindAssets(textureStem + " t:Texture2D", new[] { root });

        string preferredPath = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("/" + textureName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.IndexOf("/Textures/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                preferredPath = path;
                break;
            }

            if (path.IndexOf("/Textures_All/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                preferredPath = path;
                break;
            }

            preferredPath = preferredPath ?? path;
        }

        return string.IsNullOrEmpty(preferredPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPath);
    }

    private static void ApplyTextures(Material material, Texture2D albedo, Texture2D normal, Texture2D lit)
    {
        EnsureCompatibleShader(material);

        if (albedo != null)
        {
            SetTextureIfPropertyExists(material, "_BaseMap", albedo);
            SetTextureIfPropertyExists(material, "_MainTex", albedo);
            SetColorIfPropertyExists(material, "_BaseColor", Color.white);
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

    private static void ApplyExistingTexturesToCompatibleShader(Material material)
    {
        Texture albedo = GetFirstTexture(material, "_BaseMap", "_MainTex");
        Texture normal = GetFirstTexture(material, "_BumpMap");
        Texture lit = GetFirstTexture(material, "_EmissionMap");
        Color baseColor = GetFirstColor(material, Color.white, "_BaseColor", "_Color");
        Color emissionColor = GetFirstColor(material, Color.black, "_EmissionColor");

        EnsureCompatibleShader(material);

        if (albedo != null)
        {
            SetTextureIfPropertyExists(material, "_BaseMap", albedo);
            SetTextureIfPropertyExists(material, "_MainTex", albedo);
            SetColorIfPropertyExists(material, "_BaseColor", baseColor);
            SetColorIfPropertyExists(material, "_Color", baseColor);
        }

        if (normal != null)
        {
            SetTextureIfPropertyExists(material, "_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        if (lit != null)
        {
            SetTextureIfPropertyExists(material, "_EmissionMap", lit);
            SetColorIfPropertyExists(material, "_EmissionColor", emissionColor.maxColorComponent > 0f ? emissionColor : Color.white);
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

        if (LooksLikeGlassMaterial(material))
        {
            ConfigureGlassMaterial(material);
            return;
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

    private static bool LooksLikeGlassMaterial(Material material)
    {
        if (material == null || string.IsNullOrEmpty(material.name))
        {
            return false;
        }

        string name = material.name.ToLowerInvariant();
        return name.Contains("glass");
    }

    private static void ConfigureGlassMaterial(Material material)
    {
        float smoothness = GetFirstFloat(material, 0.55f, "_Smoothness", "_Glossiness");
        float metallic = GetFirstFloat(material, 0.0f, "_Metallic");
        Color glassColor = GetFirstColor(material, new Color(1f, 1f, 1f, 0.25f), "_BaseColor", "_Color");
        Color specColor = GetFirstColor(material, new Color(0.2f, 0.2f, 0.2f, 1f), "_SpecColor");

        if (glassColor.a <= 0.001f)
        {
            glassColor.a = 0.25f;
        }

        SetFloatIfPropertyExists(material, "_Metallic", metallic);
        SetFloatIfPropertyExists(material, "_Smoothness", smoothness);
        SetFloatIfPropertyExists(material, "_Glossiness", smoothness);
        SetFloatIfPropertyExists(material, "_Surface", 1.0f);
        SetFloatIfPropertyExists(material, "_Blend", 1.0f);
        SetFloatIfPropertyExists(material, "_AlphaClip", 0.0f);
        SetFloatIfPropertyExists(material, "_Cull", 0.0f);
        SetFloatIfPropertyExists(material, "_SrcBlend", (float)BlendMode.One);
        SetFloatIfPropertyExists(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPropertyExists(material, "_ZWrite", 0.0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");

        SetColorIfPropertyExists(material, "_BaseColor", glassColor);
        SetColorIfPropertyExists(material, "_Color", glassColor);
        SetColorIfPropertyExists(material, "_SpecColor", specColor);

        Texture emission = GetFirstTexture(material, "_EmissionMap");
        Color emissionColor = emission != null
            ? Color.white
            : Color.black;
        SetColorIfPropertyExists(material, "_EmissionColor", emissionColor);

        if (emission != null)
        {
            material.EnableKeyword("_EMISSION");
        }
        else
        {
            material.DisableKeyword("_EMISSION");
        }
    }

    private static Shader FindCompatibleLitShader()
    {
        bool hasRenderPipelineAsset = GraphicsSettings.currentRenderPipeline != null;
        string[] srpFirst =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Standard",
            "Diffuse"
        };
        string[] builtinFirst =
        {
            "Standard",
            "Diffuse",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit"
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

    private static Texture GetFirstTexture(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    private static Color GetFirstColor(Material material, Color fallback, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetColor(propertyName);
            }
        }

        return fallback;
    }

    private static float GetFirstFloat(Material material, float fallback, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetFloat(propertyName);
            }
        }

        return fallback;
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
}
#endif

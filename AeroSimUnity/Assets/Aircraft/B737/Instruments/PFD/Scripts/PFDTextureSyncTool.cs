#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PFDTextureSyncTool
{
    private const string TexturesOriginalFolder = "Assets/Aircraft/B737/Instruments/PFD/Textures/Original";
    private const string UsedFolder = "Assets/Aircraft/B737/Instruments/PFD/Textures/Used";
    private const string PreviewRgbFolder = "Assets/Aircraft/B737/Instruments/PFD/Textures/PreviewRGB";

    [MenuItem("AeroSim/PFD/Sync Textures Original To Used + PreviewRGB")]
    public static void ConvertTexturesOriginalFolder()
    {
        ConvertFolder(TexturesOriginalFolder);
    }

    private static void ConvertFolder(string sourceFolder)
    {
        if (!AssetDatabase.IsValidFolder(sourceFolder))
        {
            EditorUtility.DisplayDialog("PFD Texture Sync", $"Source folder does not exist:\n{sourceFolder}", "OK");
            return;
        }

        EnsureFolder("Assets/Aircraft/B737/Instruments/PFD/Textures");
        EnsureFolder(UsedFolder);
        EnsureFolder(PreviewRgbFolder);

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceFolder });
        var convertedCount = 0;

        foreach (var guid in guids)
        {
            var sourceAssetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!sourceAssetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var usedAssetPath = BuildOutputAssetPath(
                sourceFolder,
                UsedFolder,
                sourceAssetPath);
            var previewAssetPath = BuildOutputAssetPath(
                sourceFolder,
                PreviewRgbFolder,
                sourceAssetPath);

            CopyAssetFile(sourceAssetPath, usedAssetPath);
            WritePreviewRgbTexture(sourceAssetPath, previewAssetPath);
            convertedCount++;
        }

        AssetDatabase.Refresh();

        ConfigureFolderSprites(UsedFolder);
        ConfigureFolderSprites(PreviewRgbFolder);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "PFD Texture Sync",
            $"Converted {convertedCount} PNG file(s).\n\nSource:\n{sourceFolder}\n\nOutput:\n{UsedFolder}\n{PreviewRgbFolder}",
            "OK");
    }

    private static void CopyAssetFile(string sourceAssetPath, string targetAssetPath)
    {
        var sourceFullPath = ToFullPath(sourceAssetPath);
        var targetFullPath = ToFullPath(targetAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath));
        File.Copy(sourceFullPath, targetFullPath, true);
    }

    private static string BuildOutputAssetPath(
        string sourceFolder,
        string outputFolder,
        string sourceAssetPath)
    {
        var normalizedSourceFolder = sourceFolder.Replace("\\", "/").TrimEnd('/');
        var normalizedOutputFolder = outputFolder.Replace("\\", "/").TrimEnd('/');
        var normalizedSourceAssetPath = sourceAssetPath.Replace("\\", "/");
        var sourcePrefix = normalizedSourceFolder + "/";

        if (!normalizedSourceAssetPath.StartsWith(
                sourcePrefix,
                System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.ArgumentException(
                $"Source asset is outside source folder: {sourceAssetPath}",
                nameof(sourceAssetPath));
        }

        var relativeAssetPath = normalizedSourceAssetPath.Substring(sourcePrefix.Length);
        return $"{normalizedOutputFolder}/{relativeAssetPath}";
    }

    private static void WritePreviewRgbTexture(string sourceAssetPath, string targetAssetPath)
    {
        var bytes = File.ReadAllBytes(ToFullPath(sourceAssetPath));
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!texture.LoadImage(bytes))
        {
            Object.DestroyImmediate(texture);
            Debug.LogWarning($"PFD Texture Sync failed to read PNG: {sourceAssetPath}");
            return;
        }

        var pixels = texture.GetPixels32();
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i].a = 255;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        var targetFullPath = ToFullPath(targetAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath));
        File.WriteAllBytes(targetFullPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    private static void ConfigureFolderSprites(string folder)
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ConfigureSpriteImporter(assetPath);
        }
    }

    private static void ConfigureSpriteImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
        var child = Path.GetFileName(folderPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }

    private static string ToFullPath(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
    }
}
#endif

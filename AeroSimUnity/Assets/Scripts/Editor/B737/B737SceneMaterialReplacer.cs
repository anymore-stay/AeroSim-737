#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class B737SceneMaterialReplacer : EditorWindow
{
    private const string MenuPath = "Tools/B737/Replace Scene Aircraft Materials";
    private const string OneClickMenuPath = "Tools/B737/One Click Replace Materials From Selection";

    private GameObject targetAircraftRoot;
    private GameObject sourceAircraftRoot;
    private bool includeInactive = true;
    private bool recordUndo = true;
    private Vector2 scroll;

    private sealed class SourceIndex
    {
        public readonly Dictionary<string, List<Renderer>> RenderersByPath = new Dictionary<string, List<Renderer>>();
        public readonly Dictionary<string, List<Renderer>> RenderersByNameAndMesh = new Dictionary<string, List<Renderer>>();
        public readonly Dictionary<string, List<Renderer>> RenderersByName = new Dictionary<string, List<Renderer>>();
        public readonly Dictionary<string, List<Renderer>> RenderersByMesh = new Dictionary<string, List<Renderer>>();
        public readonly Dictionary<string, Material> MaterialsByName = new Dictionary<string, Material>();
        public int SourceRendererCount;
        public int SourceMaterialCount;
    }

    private sealed class ReplaceResult
    {
        public int TargetRendererCount;
        public int RendererMaterialSetReplaced;
        public int MaterialSlotsReplaced;
        public int RendererUnmatched;
        public readonly List<string> UnmatchedRendererNames = new List<string>();
    }

    [MenuItem(MenuPath)]
    public static void Open()
    {
        B737SceneMaterialReplacer window = GetWindow<B737SceneMaterialReplacer>("B737 Materials");
        window.minSize = new Vector2(520f, 360f);
        window.TryUseSelection();
        window.Show();
    }

    [MenuItem(OneClickMenuPath)]
    public static void ReplaceFromSelection()
    {
        GameObject target;
        GameObject source;
        if (!TryResolveSelectionPair(out target, out source))
        {
            EditorUtility.DisplayDialog(
                "B737 Material Replacer",
                "Select the purple/old aircraft as the active object, and also select the normal/new aircraft as the source.\n\nTip: click the normal aircraft first, then Ctrl-click the purple aircraft last.",
                "OK");
            return;
        }

        ReplaceMaterials(target, source, true, true, true);
    }

    [MenuItem(OneClickMenuPath, true)]
    public static bool ValidateReplaceFromSelection()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length >= 2;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("B737-800 Scene Material Replacer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use this when the old aircraft in the scene is pink/purple, but a newly dragged aircraft looks correct. The tool copies material references from the correct aircraft to the old aircraft without changing transforms, hierarchy, or animation setup.",
            MessageType.Info);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            targetAircraftRoot = (GameObject)EditorGUILayout.ObjectField(
                "Target Purple Aircraft",
                targetAircraftRoot,
                typeof(GameObject),
                true);

            sourceAircraftRoot = (GameObject)EditorGUILayout.ObjectField(
                "Source Normal Aircraft",
                sourceAircraftRoot,
                typeof(GameObject),
                true);

            if (GUILayout.Button("Use Current Selection"))
            {
                TryUseSelection();
            }
        }

        includeInactive = EditorGUILayout.ToggleLeft("Include inactive child objects", includeInactive);
        recordUndo = EditorGUILayout.ToggleLeft("Record Undo", recordUndo);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(targetAircraftRoot == null || sourceAircraftRoot == null || targetAircraftRoot == sourceAircraftRoot))
        {
            if (GUILayout.Button("Replace Target Materials Now", GUILayout.Height(38f)))
            {
                ReplaceMaterials(targetAircraftRoot, sourceAircraftRoot, includeInactive, recordUndo, false);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Recommended workflow:\n1. Keep the purple aircraft in the scene.\n2. Drag in the normal aircraft prefab/FBX once.\n3. Put the purple aircraft in Target and the normal aircraft in Source.\n4. Click Replace Target Materials Now.\n5. If it looks correct, delete or hide the temporary normal aircraft.",
            MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void TryUseSelection()
    {
        GameObject target;
        GameObject source;
        if (TryResolveSelectionPair(out target, out source))
        {
            targetAircraftRoot = target;
            sourceAircraftRoot = source;
            Repaint();
            return;
        }

        if (Selection.activeGameObject != null)
        {
            targetAircraftRoot = Selection.activeGameObject;
            Repaint();
        }
    }

    private static bool TryResolveSelectionPair(out GameObject target, out GameObject source)
    {
        target = null;
        source = null;

        GameObject active = Selection.activeGameObject;
        GameObject[] selected = Selection.gameObjects;
        if (active == null || selected == null || selected.Length < 2)
        {
            return false;
        }

        target = active;
        source = selected.FirstOrDefault(item => item != null && item != active);
        return target != null && source != null;
    }

    private static void ReplaceMaterials(
        GameObject targetRoot,
        GameObject sourceRoot,
        bool includeInactiveChildren,
        bool shouldRecordUndo,
        bool fromSelectionShortcut)
    {
        if (targetRoot == null || sourceRoot == null)
        {
            EditorUtility.DisplayDialog("B737 Material Replacer", "Target and Source aircraft roots are required.", "OK");
            return;
        }

        if (targetRoot == sourceRoot)
        {
            EditorUtility.DisplayDialog("B737 Material Replacer", "Target and Source cannot be the same object.", "OK");
            return;
        }

        SourceIndex sourceIndex = BuildSourceIndex(sourceRoot, includeInactiveChildren);
        Renderer[] targetRenderers = targetRoot.GetComponentsInChildren<Renderer>(includeInactiveChildren);
        ReplaceResult result = new ReplaceResult();
        result.TargetRendererCount = targetRenderers.Length;

        foreach (Renderer targetRenderer in targetRenderers)
        {
        Renderer sourceRenderer = FindMatchingSourceRenderer(sourceIndex, targetRoot, targetRenderer);
            if (sourceRenderer != null && CopyRendererMaterialSet(targetRenderer, sourceRenderer, shouldRecordUndo))
            {
                result.RendererMaterialSetReplaced++;
                result.MaterialSlotsReplaced += CountNonNullMaterials(sourceRenderer.sharedMaterials);
                continue;
            }

            int slotReplaced = ReplaceByMaterialName(sourceIndex, targetRenderer, shouldRecordUndo);
            if (slotReplaced > 0)
            {
                result.MaterialSlotsReplaced += slotReplaced;
                continue;
            }

            result.RendererUnmatched++;
            if (result.UnmatchedRendererNames.Count < 20)
            {
                result.UnmatchedRendererNames.Add(GetRelativePath(targetRoot.transform, targetRenderer.transform));
            }
        }

        EditorUtility.SetDirty(targetRoot);
        PrefabUtility.RecordPrefabInstancePropertyModifications(targetRoot);
        AssetDatabase.SaveAssets();

        string shortcutNote = fromSelectionShortcut
            ? "\n\nSelection shortcut used: active object was Target, the other selected object was Source."
            : string.Empty;
        string unmatchedSample = result.UnmatchedRendererNames.Count == 0
            ? string.Empty
            : "\n\nFirst unmatched renderers:\n" + string.Join("\n", result.UnmatchedRendererNames.ToArray());

        EditorUtility.DisplayDialog(
            "B737 Material Replacer",
            "Material replacement finished.\n\n" +
            "Source renderers: " + sourceIndex.SourceRendererCount + "\n" +
            "Source materials: " + sourceIndex.SourceMaterialCount + "\n" +
            "Target renderers: " + result.TargetRendererCount + "\n" +
            "Renderer material sets replaced: " + result.RendererMaterialSetReplaced + "\n" +
            "Material slots replaced: " + result.MaterialSlotsReplaced + "\n" +
            "Unmatched target renderers: " + result.RendererUnmatched +
            shortcutNote +
            unmatchedSample,
            "OK");
    }

    private static SourceIndex BuildSourceIndex(GameObject sourceRoot, bool includeInactiveChildren)
    {
        SourceIndex index = new SourceIndex();
        Renderer[] sourceRenderers = sourceRoot.GetComponentsInChildren<Renderer>(includeInactiveChildren);
        index.SourceRendererCount = sourceRenderers.Length;

        foreach (Renderer renderer in sourceRenderers)
        {
            Add(index.RenderersByPath, NormalizePath(GetRelativePath(sourceRoot.transform, renderer.transform)), renderer);
            Add(index.RenderersByNameAndMesh, BuildNameAndMeshKey(renderer), renderer);
            Add(index.RenderersByName, NormalizeName(renderer.name), renderer);

            string meshKey = NormalizeName(GetMeshName(renderer));
            if (!string.IsNullOrEmpty(meshKey))
            {
                Add(index.RenderersByMesh, meshKey, renderer);
            }

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                string materialKey = NormalizeMaterialName(material.name);
                if (!string.IsNullOrEmpty(materialKey) && !index.MaterialsByName.ContainsKey(materialKey))
                {
                    index.MaterialsByName.Add(materialKey, material);
                }
            }
        }

        index.SourceMaterialCount = index.MaterialsByName.Count;
        return index;
    }

    private static Renderer FindMatchingSourceRenderer(
        SourceIndex sourceIndex,
        GameObject targetRoot,
        Renderer targetRenderer)
    {
        string relativePath = NormalizePath(GetRelativePath(targetRoot.transform, targetRenderer.transform));
        Renderer byPath = GetUnique(sourceIndex.RenderersByPath, relativePath);
        if (byPath != null)
        {
            return byPath;
        }

        Renderer byNameAndMesh = GetUnique(sourceIndex.RenderersByNameAndMesh, BuildNameAndMeshKey(targetRenderer));
        if (byNameAndMesh != null)
        {
            return byNameAndMesh;
        }

        string meshKey = NormalizeName(GetMeshName(targetRenderer));
        Renderer byMesh = string.IsNullOrEmpty(meshKey) ? null : GetUnique(sourceIndex.RenderersByMesh, meshKey);
        if (byMesh != null)
        {
            return byMesh;
        }

        return GetUnique(sourceIndex.RenderersByName, NormalizeName(targetRenderer.name));
    }

    private static bool CopyRendererMaterialSet(Renderer targetRenderer, Renderer sourceRenderer, bool shouldRecordUndo)
    {
        Material[] sourceMaterials = sourceRenderer.sharedMaterials;
        if (sourceMaterials == null || sourceMaterials.Length == 0)
        {
            return false;
        }

        if (AreSameMaterials(targetRenderer.sharedMaterials, sourceMaterials))
        {
            return false;
        }

        if (shouldRecordUndo)
        {
            Undo.RecordObject(targetRenderer, "Replace B737 Renderer Materials");
        }

        targetRenderer.sharedMaterials = sourceMaterials.ToArray();
        EditorUtility.SetDirty(targetRenderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(targetRenderer);
        return true;
    }

    private static int ReplaceByMaterialName(SourceIndex sourceIndex, Renderer targetRenderer, bool shouldRecordUndo)
    {
        Material[] targetMaterials = targetRenderer.sharedMaterials;
        if (targetMaterials == null || targetMaterials.Length == 0)
        {
            return 0;
        }

        bool changed = false;
        int replaced = 0;
        for (int i = 0; i < targetMaterials.Length; i++)
        {
            Material targetMaterial = targetMaterials[i];
            if (targetMaterial == null)
            {
                continue;
            }

            Material sourceMaterial;
            if (!sourceIndex.MaterialsByName.TryGetValue(NormalizeMaterialName(targetMaterial.name), out sourceMaterial))
            {
                continue;
            }

            if (sourceMaterial == null || sourceMaterial == targetMaterial)
            {
                continue;
            }

            targetMaterials[i] = sourceMaterial;
            changed = true;
            replaced++;
        }

        if (!changed)
        {
            return 0;
        }

        if (shouldRecordUndo)
        {
            Undo.RecordObject(targetRenderer, "Replace B737 Material Slots");
        }

        targetRenderer.sharedMaterials = targetMaterials;
        EditorUtility.SetDirty(targetRenderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(targetRenderer);
        return replaced;
    }

    private static bool AreSameMaterials(Material[] left, Material[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int CountNonNullMaterials(Material[] materials)
    {
        if (materials == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Material material in materials)
        {
            if (material != null)
            {
                count++;
            }
        }

        return count;
    }

    private static string BuildNameAndMeshKey(Renderer renderer)
    {
        return NormalizeName(renderer.name) + "|" + NormalizeName(GetMeshName(renderer));
    }

    private static string GetMeshName(Renderer renderer)
    {
        SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
        if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
        {
            return skinnedMeshRenderer.sharedMesh.name;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : string.Empty;
    }

    private static string GetRelativePath(Transform root, Transform child)
    {
        if (root == null || child == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        Transform current = child;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return string.Join("/", path.Split('/').Select(NormalizeName).ToArray());
    }

    private static string NormalizeMaterialName(string name)
    {
        string normalized = NormalizeName(name);
        if (normalized.StartsWith("mat_"))
        {
            normalized = normalized.Substring(4);
        }

        if (normalized.StartsWith("material_"))
        {
            normalized = normalized.Substring(9);
        }

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        string normalized = name.Trim().ToLowerInvariant();
        normalized = normalized.Replace("(instance)", string.Empty);
        normalized = normalized.Replace(" instance", string.Empty);

        int cloneIndex = normalized.IndexOf("(clone)", StringComparison.Ordinal);
        if (cloneIndex >= 0)
        {
            normalized = normalized.Substring(0, cloneIndex);
        }

        normalized = Regex.Replace(normalized, @"\.\d{3}$", string.Empty);

        return normalized.Trim();
    }

    private static void Add(Dictionary<string, List<Renderer>> map, string key, Renderer renderer)
    {
        if (string.IsNullOrEmpty(key) || renderer == null)
        {
            return;
        }

        List<Renderer> renderers;
        if (!map.TryGetValue(key, out renderers))
        {
            renderers = new List<Renderer>();
            map.Add(key, renderers);
        }

        renderers.Add(renderer);
    }

    private static Renderer GetUnique(Dictionary<string, List<Renderer>> map, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        List<Renderer> renderers;
        if (!map.TryGetValue(key, out renderers) || renderers == null || renderers.Count != 1)
        {
            return null;
        }

        return renderers[0];
    }
}
#endif

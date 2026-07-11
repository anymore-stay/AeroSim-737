using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class B737FmsDisplayEditorUtility
{
    private const string RenderTexturePath = "Assets/Aircraft/B737/Instruments/FMS.renderTexture";
    private const string MaterialPath = "Assets/Aircraft/B737/Instruments/FMS.mat";
    private const string LeftScreenName = "FMS_screens__ImpMesh.000_x345_69206";
    private const string RightScreenName = "FMS_screens__ImpMesh.001_x345_12570";

    [MenuItem("Tools/B737/Instruments/刷新 FMS 屏幕玻璃材质")]
    public static void RefreshFmsScreenMaterial()
    {
        RenderTexture renderTexture = EnsureRenderTexture();
        EnsureMaterial(renderTexture);
        AssetDatabase.SaveAssets();
        Debug.Log("[B737 FMS] 已刷新 FMS 屏幕的柔化与微反光材质。");
    }

    [MenuItem("Tools/B737/Instruments/Install FMS Display On Selected B737")]
    public static void InstallFmsDisplayOnSelectedB737()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("[B737 FMS] Select the B737 root GameObject first.");
            return;
        }

        RenderTexture renderTexture = EnsureRenderTexture();
        Material material = EnsureMaterial(renderTexture);
        B737FmsDisplayRig rig = selected.GetComponent<B737FmsDisplayRig>();
        if (rig == null)
        {
            rig = Undo.AddComponent<B737FmsDisplayRig>(selected);
        }

        MeshRenderer leftScreen = FindRenderer(selected.transform, LeftScreenName);
        MeshRenderer rightScreen = FindRenderer(selected.transform, RightScreenName);
        if (leftScreen == null || rightScreen == null)
        {
            Debug.LogError(
                $"[B737 FMS] Could not find FMS screen renderers. left={NameOf(leftScreen)}, right={NameOf(rightScreen)}",
                selected);
            return;
        }

        Undo.RecordObject(rig, "Install B737 FMS Display");
        Undo.RecordObject(leftScreen, "Assign B737 FMS Material");
        Undo.RecordObject(rightScreen, "Assign B737 FMS Material");

        rig.SetAssets(renderTexture, material);
        rig.SetScreenRenderers(leftScreen, rightScreen);
        rig.EnsureRig();

        EditorUtility.SetDirty(rig);
        EditorUtility.SetDirty(leftScreen);
        EditorUtility.SetDirty(rightScreen);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(selected.scene);

        Debug.Log(
            $"[B737 FMS] Installed FMS display on {selected.name}. " +
            $"Screens: {leftScreen.name}, {rightScreen.name}. Assets: {RenderTexturePath}, {MaterialPath}",
            selected);
    }

    [MenuItem("Tools/B737/Instruments/Install FMS Display On Selected B737", true)]
    private static bool CanInstallFmsDisplayOnSelectedB737()
    {
        return Selection.activeGameObject != null;
    }

    private static RenderTexture EnsureRenderTexture()
    {
        RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (renderTexture != null)
        {
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            EditorUtility.SetDirty(renderTexture);
            return renderTexture;
        }

        EnsureDirectory(Path.GetDirectoryName(RenderTexturePath));
        renderTexture = new RenderTexture(768, 1024, 24, RenderTextureFormat.ARGB32)
        {
            name = "FMS",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false
        };

        renderTexture.filterMode = FilterMode.Bilinear;
        renderTexture.wrapMode = TextureWrapMode.Clamp;

        AssetDatabase.CreateAsset(renderTexture, RenderTexturePath);
        AssetDatabase.SaveAssets();
        return renderTexture;
    }

    private static Material EnsureMaterial(RenderTexture renderTexture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            EnsureDirectory(Path.GetDirectoryName(MaterialPath));
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = "FMS"
            };

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
        }

        renderTexture.filterMode = FilterMode.Bilinear;
        renderTexture.wrapMode = TextureWrapMode.Clamp;
        AssignTexture(material, renderTexture);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AssignTexture(Material material, RenderTexture renderTexture)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", renderTexture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", renderTexture);
        }

        if (material.HasProperty("_EmissionMap"))
        {
            material.SetTexture("_EmissionMap", renderTexture);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f, 1f));
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_EnvironmentReflections"))
        {
            material.SetFloat("_EnvironmentReflections", 1f);
        }

        if (material.HasProperty("_SpecularHighlights"))
        {
            material.SetFloat("_SpecularHighlights", 1f);
        }

        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    private static MeshRenderer FindRenderer(Transform root, string targetName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == targetName)
            {
                return children[i].GetComponent<MeshRenderer>();
            }
        }

        return null;
    }

    private static void EnsureDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory))
        {
            return;
        }

        string parent = Path.GetDirectoryName(directory)?.Replace('\\', '/');
        string leaf = Path.GetFileName(directory);
        EnsureDirectory(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static string NameOf(Object target)
    {
        return target != null ? target.name : "<none>";
    }
}

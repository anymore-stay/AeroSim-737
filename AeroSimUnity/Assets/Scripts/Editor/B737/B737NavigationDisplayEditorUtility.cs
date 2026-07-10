using UnityEditor;
using UnityEngine;

public static class B737NavigationDisplayEditorUtility
{
    private const string RenderTexturePath = "Assets/Aircraft/B737/Textures/ND.renderTexture";
    private const string MaterialPath = "Assets/Aircraft/B737/Materials/ND.mat";

    [MenuItem("AeroSim/B737/Create Navigation Display Rig")]
    public static void CreateNavigationDisplayRig()
    {
        CreateNavigationDisplayRig(false);
    }

    [MenuItem("AeroSim/B737/Attach Navigation Display To Selected Renderer")]
    public static void AttachNavigationDisplayToSelectedRenderer()
    {
        CreateNavigationDisplayRig(true);
    }

    [MenuItem("AeroSim/B737/Attach Navigation Display To Selected Renderer", true)]
    public static bool ValidateAttachNavigationDisplayToSelectedRenderer()
    {
        return Selection.activeTransform != null &&
               Selection.activeTransform.GetComponent<MeshRenderer>() != null;
    }

    private static void CreateNavigationDisplayRig(bool attachToSelectedRenderer)
    {
        GameObject rigGo = new GameObject("B737_ND_Rig");
        Undo.RegisterCreatedObjectUndo(rigGo, "Create B737 ND Rig");

        Transform selected = Selection.activeTransform;
        if (selected != null)
        {
            rigGo.transform.SetParent(selected, false);
        }

        B737NavigationDisplayRig rig = rigGo.AddComponent<B737NavigationDisplayRig>();
        RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        rig.SetAssets(renderTexture, material);

        MeshRenderer selectedRenderer = selected != null ? selected.GetComponent<MeshRenderer>() : null;
        if (attachToSelectedRenderer && selectedRenderer != null)
        {
            rig.SetDisplayPlaneRenderer(selectedRenderer);
        }

        rig.EnsureRig();
        Selection.activeGameObject = rigGo;
        EditorGUIUtility.PingObject(rigGo);
        EditorSceneManagerHelper.MarkActiveSceneDirty();
    }

    private static class EditorSceneManagerHelper
    {
        public static void MarkActiveSceneDirty()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}

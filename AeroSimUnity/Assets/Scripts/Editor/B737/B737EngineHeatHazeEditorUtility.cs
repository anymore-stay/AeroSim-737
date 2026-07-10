using UnityEditor;
using UnityEngine;

public static class B737EngineHeatHazeEditorUtility
{
    private const string MaterialPath = "Assets/Aircraft/B737/Materials/B737HeatHaze.mat";

    [MenuItem("AeroSim/B737/Add Engine Heat Haze To Selected Aircraft")]
    public static void AddHeatHazeToSelectedAircraft()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "B737 Heat Haze",
                "请先在 Hierarchy 里选中 B737 飞机根物体，再执行该菜单。",
                "OK");
            return;
        }

        B737EngineHeatHaze heatHaze = selected.GetComponent<B737EngineHeatHaze>();
        if (heatHaze == null)
        {
            heatHaze = Undo.AddComponent<B737EngineHeatHaze>(selected);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        heatHaze.SetHeatHazeMaterial(material);
        heatHaze.RebuildEmitters();

        EditorUtility.SetDirty(selected);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = selected;
        EditorGUIUtility.PingObject(selected);
    }
}

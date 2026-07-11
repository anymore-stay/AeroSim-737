using UnityEditor;
using UnityEngine;

/// <summary>
/// 为 B737 Prefab 配置驾驶舱操纵杆显隐组件和引用。
/// </summary>
public static class B737CockpitControlColumnVisibilityEditorUtility
{
    private const string B737PrefabPath = "Assets/Aircraft/B737/Prefabs/B737.prefab";
    private const string TargetObjectName = "ImpEmpty.001_x24e_47969";

    [MenuItem("AeroSim/B737/配置驾驶舱操纵杆显隐")]
    public static void Configure()
    {
        B737FmsDisplayRig.SuppressEditorRebuild = true;
        GameObject prefabRoot = null;
        try
        {
            prefabRoot = PrefabUtility.LoadPrefabContents(B737PrefabPath);
            Transform target = FindByName(prefabRoot.transform, TargetObjectName);
            CockpitCameraController cockpitCamera = FindCockpitCamera(prefabRoot);
            if (target == null || cockpitCamera == null)
            {
                throw new MissingReferenceException("未找到操纵杆目标或 CockpitCameraController。");
            }

            B737CockpitControlColumnVisibility controller =
                prefabRoot.GetComponent<B737CockpitControlColumnVisibility>();
            if (controller == null)
            {
                controller = prefabRoot.AddComponent<B737CockpitControlColumnVisibility>();
            }

            controller.SetBindings(target.gameObject, cockpitCamera);
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, B737PrefabPath);
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            B737FmsDisplayRig.SuppressEditorRebuild = false;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[B737] 已配置驾驶舱操纵杆显隐功能，快捷键为数字键 1。");
    }

    private static CockpitCameraController FindCockpitCamera(GameObject root)
    {
        CockpitCameraController[] controllers =
            root.GetComponentsInChildren<CockpitCameraController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i].cameraMode == CockpitCameraController.CameraMode.Cockpit)
            {
                return controllers[i];
            }
        }

        return null;
    }

    private static Transform FindByName(Transform root, string targetName)
    {
        if (root == null || root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindByName(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(PFDLayerGenerator))]
public class PFDLayerGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate PFD_Final From Preview", GUILayout.Height(30)))
        {
            GenerateFinal((PFDLayerGenerator)target);
        }
    }

    private static void GenerateFinal(PFDLayerGenerator generator)
    {
        if (generator.PreviewGuide == null)
        {
            EditorUtility.DisplayDialog("PFD Final Generator", "Please assign PFD_PreviewGuide first.", "OK");
            return;
        }

        var preview = generator.PreviewGuide;
        var parent = preview.transform.parent;
        var finalName = string.IsNullOrWhiteSpace(generator.FinalLayerName)
            ? "PFD_Final"
            : generator.FinalLayerName;

        var existing = parent.Find(finalName);
        if (existing != null)
        {
            if (!generator.OverwriteExisting)
            {
                EditorUtility.DisplayDialog("PFD Final Generator", $"{finalName} already exists.", "OK");
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var final = Object.Instantiate(preview, parent);
        Undo.RegisterCreatedObjectUndo(final, "Generate PFD Final");
        final.name = finalName;

        RenameGuideObjects(final.transform);
        SwapPreviewSpritesToUsed(final);

        final.SetActive(generator.ShowFinalAfterGenerate);
        if (generator.HidePreviewAfterGenerate)
        {
            Undo.RecordObject(preview, "Hide PFD Preview Guide");
            preview.SetActive(false);
        }

        Selection.activeGameObject = final;
        EditorUtility.SetDirty(final);
        EditorSceneManagerBridge.MarkSceneDirty(final);
    }

    private static void RenameGuideObjects(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("Guide_"))
            {
                child.name = "Final_" + child.name.Substring("Guide_".Length);
            }
        }
    }

    private static void SwapPreviewSpritesToUsed(GameObject root)
    {
        var images = root.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image.sprite == null)
            {
                continue;
            }

            var previewPath = AssetDatabase.GetAssetPath(image.sprite);
            if (string.IsNullOrEmpty(previewPath) || !previewPath.Contains("/PreviewRGB/"))
            {
                continue;
            }

            var usedPath = previewPath.Replace("/PreviewRGB/", "/Used/");
            var usedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(usedPath);
            if (usedSprite == null)
            {
                Debug.LogWarning($"PFD final generator could not find matching Used sprite: {usedPath}", image);
                continue;
            }

            Undo.RecordObject(image, "Swap PFD Final Sprite");
            image.sprite = usedSprite;
            EditorUtility.SetDirty(image);
        }
    }

    private static class EditorSceneManagerBridge
    {
        public static void MarkSceneDirty(GameObject sceneObject)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sceneObject.scene);
        }
    }
}
#endif

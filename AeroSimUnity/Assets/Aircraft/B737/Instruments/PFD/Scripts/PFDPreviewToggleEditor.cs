#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PFDPreviewToggle))]
public class PFDPreviewToggleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var toggle = (PFDPreviewToggle)target;
        var label = toggle.ShowPreview ? "Show Final Layers" : "Show Preview Layers";

        EditorGUILayout.Space();
        if (GUILayout.Button(label, GUILayout.Height(28)))
        {
            Undo.RecordObject(toggle, "Toggle PFD Preview");
            toggle.TogglePreview();
            EditorUtility.SetDirty(toggle);
        }
    }
}
#endif

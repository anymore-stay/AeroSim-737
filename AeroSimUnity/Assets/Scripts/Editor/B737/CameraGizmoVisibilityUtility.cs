using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hides Camera icons and camera-frustum gizmo lines in the Editor/Game view.
/// This does not disable Camera components, so cockpit and instrument rendering keep working.
/// </summary>
[InitializeOnLoad]
public static class CameraGizmoVisibilityUtility
{
    private const int Hidden = 0;
    private const int CameraClassId = 20;

    static CameraGizmoVisibilityUtility()
    {
        EditorApplication.delayCall += HideCameraGizmos;
    }

    [MenuItem("AeroSim/Camera/Hide Camera Gizmos")]
    public static void HideCameraGizmos()
    {
        SetAnnotationVisibility(typeof(Camera), Hidden);
        DisableGameViewGizmos();
        SceneView.RepaintAll();
    }

    private static void SetAnnotationVisibility(Type componentType, int enabled)
    {
        Type annotationUtility = Type.GetType("UnityEditor.AnnotationUtility,UnityEditor");
        if (annotationUtility == null)
            return;

        bool changed = SetAnnotationVisibilityByType(annotationUtility, componentType, enabled);
        changed |= SetAnnotationVisibilityByClassId(annotationUtility, CameraClassId, string.Empty, enabled);

        if (!changed)
            Debug.LogWarning("Could not find Unity Camera gizmo visibility API. Turn off Gizmos in the Game view manually.");
    }

    private static bool SetAnnotationVisibilityByType(
        Type annotationUtility,
        Type componentType,
        int enabled)
    {
        bool changed = false;

        MethodInfo setIconEnabled = annotationUtility.GetMethod(
            "SetIconEnabled",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(Type), typeof(int) },
            null);
        if (setIconEnabled != null)
        {
            setIconEnabled.Invoke(null, new object[] { componentType, enabled });
            changed = true;
        }

        MethodInfo setGizmoEnabled = annotationUtility.GetMethod(
            "SetGizmoEnabled",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(Type), typeof(int) },
            null);
        if (setGizmoEnabled != null)
        {
            setGizmoEnabled.Invoke(null, new object[] { componentType, enabled });
            changed = true;
        }

        return changed;
    }

    private static bool SetAnnotationVisibilityByClassId(
        Type annotationUtility,
        int classId,
        string scriptClass,
        int enabled)
    {
        bool changed = false;

        MethodInfo setIconEnabled = annotationUtility.GetMethod(
            "SetIconEnabled",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(int), typeof(string), typeof(int) },
            null);
        if (setIconEnabled != null)
        {
            setIconEnabled.Invoke(null, new object[] { classId, scriptClass, enabled });
            changed = true;
        }

        MethodInfo setGizmoEnabled = annotationUtility.GetMethod(
            "SetGizmoEnabled",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(int), typeof(string), typeof(int) },
            null);
        if (setGizmoEnabled != null)
        {
            setGizmoEnabled.Invoke(null, new object[] { classId, scriptClass, enabled });
            changed = true;
        }

        return changed;
    }

    private static void DisableGameViewGizmos()
    {
        Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null)
            return;

        EditorWindow[] gameViews = Resources
            .FindObjectsOfTypeAll(gameViewType)
            .OfType<EditorWindow>()
            .ToArray();
        for (int index = 0; index < gameViews.Length; index++)
            SetGameViewDrawGizmos(gameViewType, gameViews[index], false);
    }

    private static void SetGameViewDrawGizmos(Type gameViewType, EditorWindow gameView, bool draw)
    {
        PropertyInfo drawGizmosProperty = gameViewType.GetProperty(
            "drawGizmos",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (drawGizmosProperty != null && drawGizmosProperty.CanWrite)
        {
            drawGizmosProperty.SetValue(gameView, draw, null);
            gameView.Repaint();
            return;
        }

        FieldInfo drawGizmosField = gameViewType.GetField(
            "m_Gizmos",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (drawGizmosField != null)
        {
            drawGizmosField.SetValue(gameView, draw);
            gameView.Repaint();
        }
    }
}

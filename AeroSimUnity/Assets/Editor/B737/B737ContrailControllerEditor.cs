using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(B737ContrailController))]
public class B737ContrailControllerEditor : Editor
{
    private SerializedProperty bridge;
    private SerializedProperty aircraft;
    private SerializedProperty realisticSmokeTrails;
    private SerializedProperty minimumAltitudeFt;
    private SerializedProperty minimumSpeedKts;
    private SerializedProperty altitudeHysteresisFt;
    private SerializedProperty speedHysteresisKts;
    private SerializedProperty useAltitudeAboveSeaLevel;
    private SerializedProperty particleSpacingMeters;
    private SerializedProperty smokeSortingOrder;
    private SerializedProperty minimumRecenteringDistanceMeters;
    private SerializedProperty transformJumpRecenteringDistanceMeters;
    private SerializedProperty logStateChanges;

    private void OnEnable()
    {
        bridge = serializedObject.FindProperty("bridge");
        aircraft = serializedObject.FindProperty("aircraft");
        realisticSmokeTrails = serializedObject.FindProperty("realisticSmokeTrails");
        minimumAltitudeFt = serializedObject.FindProperty("minimumAltitudeFt");
        minimumSpeedKts = serializedObject.FindProperty("minimumSpeedKts");
        altitudeHysteresisFt = serializedObject.FindProperty("altitudeHysteresisFt");
        speedHysteresisKts = serializedObject.FindProperty("speedHysteresisKts");
        useAltitudeAboveSeaLevel = serializedObject.FindProperty("useAltitudeAboveSeaLevel");
        particleSpacingMeters = serializedObject.FindProperty("particleSpacingMeters");
        smokeSortingOrder = serializedObject.FindProperty("smokeSortingOrder");
        minimumRecenteringDistanceMeters = serializedObject.FindProperty("minimumRecenteringDistanceMeters");
        transformJumpRecenteringDistanceMeters = serializedObject.FindProperty("transformJumpRecenteringDistanceMeters");
        logStateChanges = serializedObject.FindProperty("logStateChanges");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSection("引用设置");
        DrawProperty(bridge, "飞行数据 Bridge");
        DrawProperty(aircraft, "飞机根节点");
        DrawProperty(realisticSmokeTrails, "Realistic Smoke 航迹云", true);

        DrawSection("航迹云生效条件");
        DrawProperty(minimumAltitudeFt, "最低生效高度 (ft)");
        DrawProperty(minimumSpeedKts, "最低生效空速 (kt)");
        DrawProperty(altitudeHysteresisFt, "高度关闭回差 (ft)");
        DrawProperty(speedHysteresisKts, "空速关闭回差 (kt)");
        DrawProperty(useAltitudeAboveSeaLevel, "使用海拔高度");
        DrawProperty(particleSpacingMeters, "烟雾生成间距 (m)");
        DrawProperty(smokeSortingOrder, "烟雾渲染排序");

        DrawSection("Cesium 回拉补偿");
        DrawProperty(minimumRecenteringDistanceMeters, "最小回拉补偿距离 (m)");
        DrawProperty(transformJumpRecenteringDistanceMeters, "Transform 跳变阈值 (m)");
        DrawProperty(logStateChanges, "输出调试日志");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawSection(string title)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void DrawProperty(SerializedProperty property, string label, bool includeChildren = false)
    {
        if (property == null) return;
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), includeChildren);
    }
}

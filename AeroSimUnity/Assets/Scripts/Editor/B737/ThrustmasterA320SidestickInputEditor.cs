using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ThrustmasterA320SidestickInput))]
public sealed class ThrustmasterA320SidestickInputEditor : Editor
{
    private double nextRepaintTime;

    private void OnEnable()
    {
        EditorApplication.update += RefreshWhilePlaying;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RefreshWhilePlaying;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var input = (ThrustmasterA320SidestickInput)target;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("实时设备状态", EditorStyles.boldLabel);
        MessageType messageType = input.IsConnected ? MessageType.Info : MessageType.Warning;
        EditorGUILayout.HelpBox(input.ConnectionMessage, messageType);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("重新扫描设备"))
            {
                input.RescanDevices();
                Repaint();
            }

            using (new EditorGUI.DisabledScope(!input.IsConnected))
                GUILayout.Label(input.IsConnected ? "正在接收数据" : "无数据", EditorStyles.miniLabel);
        }

        if (input.DetectedDeviceNames.Count > 0)
        {
            EditorGUILayout.LabelField("已检测控制器", EditorStyles.miniBoldLabel);
            foreach (string deviceName in input.DetectedDeviceNames)
                EditorGUILayout.LabelField(deviceName);
        }

        if (!input.IsConnected)
            return;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("设备名称", input.ConnectedDeviceName);
            EditorGUILayout.IntField("设备编号", (int)input.ConnectedDeviceId);
            EditorGUILayout.TextField("厂商 / 产品 ID", $"{input.ManufacturerId:X4} / {input.ProductId:X4}");
            EditorGUILayout.IntField("轴数量", input.AxisCount);
            EditorGUILayout.IntField("按钮数量", input.ButtonCount);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("全部原始轴数据", EditorStyles.boldLabel);
        DrawAxis("X 轴", input.RawX, input.NormalizedX);
        DrawAxis("Y 轴", input.RawY, input.NormalizedY);
        DrawAxis("Z 轴", input.RawZ, input.NormalizedZ);
        DrawAxis("R 轴", input.RawR, input.NormalizedR);
        DrawAxis("U 轴", input.RawU, input.NormalizedU);
        DrawAxis("V 轴", input.RawV, input.NormalizedV);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("发送给飞机的数据", EditorStyles.boldLabel);
        DrawOutput("横滚", input.Roll, -1f, 1f);
        DrawOutput("俯仰", input.Pitch, -1f, 1f);
        DrawOutput("方向舵", input.Yaw, -1f, 1f);
        DrawOutput("油门", input.Throttle, 0f, 1f);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("POV 帽", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(input.PovDegrees < 0 ? "居中" : $"{input.PovDegrees}°");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"按钮状态（原始位掩码 0x{input.RawButtons:X8}）", EditorStyles.boldLabel);
        DrawButtons(input);
    }

    private static void DrawAxis(string label, uint raw, float normalized)
    {
        Rect row = EditorGUILayout.GetControlRect();
        Rect labelRect = new Rect(row.x, row.y, 45f, row.height);
        Rect sliderRect = new Rect(row.x + 48f, row.y, row.width - 160f, row.height);
        Rect valueRect = new Rect(row.xMax - 108f, row.y, 108f, row.height);
        EditorGUI.LabelField(labelRect, label);
        EditorGUI.ProgressBar(sliderRect, (normalized + 1f) * 0.5f, string.Empty);
        EditorGUI.LabelField(valueRect, $"{raw}  ({normalized:+0.000;-0.000;0.000})");
    }

    private static void DrawOutput(string label, float value, float min, float max)
    {
        Rect row = EditorGUILayout.GetControlRect();
        Rect labelRect = new Rect(row.x, row.y, 55f, row.height);
        Rect sliderRect = new Rect(row.x + 58f, row.y, row.width - 125f, row.height);
        Rect valueRect = new Rect(row.xMax - 62f, row.y, 62f, row.height);
        float progress = Mathf.InverseLerp(min, max, value);
        EditorGUI.LabelField(labelRect, label);
        EditorGUI.ProgressBar(sliderRect, progress, string.Empty);
        EditorGUI.LabelField(valueRect, value.ToString("+0.000;-0.000;0.000"));
    }

    private static void DrawButtons(ThrustmasterA320SidestickInput input)
    {
        int count = Mathf.Max(1, input.ButtonCount);
        const int columns = 8;
        for (int first = 0; first < count; first += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int index = first; index < Mathf.Min(first + columns, count); index++)
                {
                    Color previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = input.GetButton(index)
                        ? new Color(0.35f, 0.85f, 0.4f)
                        : Color.white;
                    GUILayout.Toggle(input.GetButton(index), (index + 1).ToString(), "Button");
                    GUI.backgroundColor = previousColor;
                }
            }
        }
    }

    private void RefreshWhilePlaying()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup < nextRepaintTime)
            return;
        nextRepaintTime = EditorApplication.timeSinceStartup + 0.05d;
        Repaint();
    }
}

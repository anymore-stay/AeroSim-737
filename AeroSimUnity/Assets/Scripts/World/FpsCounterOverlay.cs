using UnityEngine;

public class FpsCounterOverlay : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("是否显示右上角 FPS。")]
    [SerializeField] private bool showFps = true;

    [Tooltip("刷新间隔，单位秒。数值越小跳动越频繁。")]
    [SerializeField, Min(0.05f)] private float updateInterval = 0.25f;

    [Tooltip("字体大小。")]
    [SerializeField, Min(8)] private int fontSize = 24;

    [Tooltip("距离屏幕右边的像素。")]
    [SerializeField, Min(0)] private int rightPadding = 18;

    [Tooltip("距离屏幕上边的像素。")]
    [SerializeField, Min(0)] private int topPadding = 12;

    [Tooltip("显示文字颜色。")]
    [SerializeField] private Color textColor = Color.green;

    private float accumulatedTime;
    private int accumulatedFrames;
    private float currentFps;
    private GUIStyle style;

    private void Update()
    {
        accumulatedTime += Time.unscaledDeltaTime;
        accumulatedFrames++;

        if (accumulatedTime >= updateInterval)
        {
            currentFps = accumulatedFrames / accumulatedTime;
            accumulatedFrames = 0;
            accumulatedTime = 0f;
        }
    }

    private void OnGUI()
    {
        if (!showFps)
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontStyle = FontStyle.Bold
            };
        }

        style.fontSize = fontSize;
        style.normal.textColor = textColor;

        string label = string.Format("FPS: {0:0}", currentFps);
        Rect rect = new Rect(0f, topPadding, Screen.width - rightPadding, fontSize + 8f);
        GUI.Label(rect, label, style);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AeroSimStartMenuController : MonoBehaviour
{
    private const string SelectedPresetKey = "AeroSim.StartMenu.SelectedPreset";

    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private bool createInterfaceOnAwake = true;

    private readonly List<Button> presetButtons = new List<Button>();
    private GraphicsPreset[] presets;
    private int selectedPresetIndex;
    private Font interfaceFont;

    [Serializable]
    public struct GraphicsPreset
    {
        public string Label;
        public int Width;
        public int Height;
        public string QualityName;

        public GraphicsPreset(string label, int width, int height, string qualityName)
        {
            Label = label;
            Width = width;
            Height = height;
            QualityName = qualityName;
        }
    }

    public static GraphicsPreset[] CreateDefaultPresets()
    {
        return new[]
        {
            new GraphicsPreset("流畅 1080p", 1920, 1080, "Performant"),
            new GraphicsPreset("均衡 2K", 2560, 1440, "Balanced"),
            new GraphicsPreset("最高 4K", 3840, 2160, "High Fidelity")
        };
    }

    public static int FindQualityIndex(string qualityName)
    {
        return FindQualityIndex(qualityName, QualitySettings.names);
    }

    public static int FindQualityIndex(string qualityName, string[] qualityNames)
    {
        if (qualityNames == null || qualityNames.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (string.Equals(qualityNames[i], qualityName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return qualityNames.Length - 1;
    }

    private void Awake()
    {
        presets = CreateDefaultPresets();
        selectedPresetIndex = Mathf.Clamp(PlayerPrefs.GetInt(SelectedPresetKey, presets.Length - 1), 0, presets.Length - 1);

        EnsureEventSystem();

        if (createInterfaceOnAwake)
        {
            CreateInterface();
        }

        SelectPreset(selectedPresetIndex);
    }

    public void SelectPreset(int presetIndex)
    {
        selectedPresetIndex = Mathf.Clamp(presetIndex, 0, presets.Length - 1);
        PlayerPrefs.SetInt(SelectedPresetKey, selectedPresetIndex);
        PlayerPrefs.Save();

        ApplyPreset(presets[selectedPresetIndex]);
        RefreshPresetButtons();
    }

    public void StartSimulation()
    {
        ApplyPreset(presets[selectedPresetIndex]);

        if (Application.CanStreamedLevelBeLoaded(mainSceneName))
        {
            SceneManager.LoadScene(mainSceneName);
            return;
        }

        SceneManager.LoadScene(1);
    }

    public void ExitSimulation()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void ApplyPreset(GraphicsPreset preset)
    {
        QualitySettings.SetQualityLevel(FindQualityIndex(preset.QualityName), true);
        Screen.SetResolution(preset.Width, preset.Height, FullScreenMode.ExclusiveFullScreen);
    }

    private void CreateInterface()
    {
        interfaceFont = GetInterfaceFont();

        GameObject canvasObject = new GameObject("开始界面 Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        CreatePanel("背景", canvasRect, new Color(0.035f, 0.043f, 0.055f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform panel = CreatePanel(
            "开始菜单",
            canvasRect,
            new Color(0.075f, 0.09f, 0.115f, 0.95f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-360f, -245f),
            new Vector2(360f, 245f));

        CreateText("标题", panel, "AeroSim-737", 50, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 130f), new Vector2(640f, 70f));
        CreateText("副标题", panel, "Boeing 737-800 飞行模拟", 22, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0f, 82f), new Vector2(640f, 36f));
        CreateText("画质标签", panel, "画质与分辨率", 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 28f), new Vector2(640f, 34f));

        for (int i = 0; i < presets.Length; i++)
        {
            int presetIndex = i;
            Button button = CreateButton(panel, presets[i].Label, new Vector2(0f, -34f - i * 64f), new Vector2(460f, 52f));
            button.onClick.AddListener(() => SelectPreset(presetIndex));
            presetButtons.Add(button);
        }

        Button startButton = CreateButton(panel, "开始飞行", new Vector2(-118f, -202f), new Vector2(210f, 54f));
        startButton.onClick.AddListener(StartSimulation);

        Button exitButton = CreateButton(panel, "退出", new Vector2(118f, -202f), new Vector2(210f, 54f));
        exitButton.onClick.AddListener(ExitSimulation);
    }

    private void RefreshPresetButtons()
    {
        for (int i = 0; i < presetButtons.Count; i++)
        {
            Image image = presetButtons[i].GetComponent<Image>();
            Text label = presetButtons[i].GetComponentInChildren<Text>();
            bool selected = i == selectedPresetIndex;

            image.color = selected ? new Color(0.16f, 0.48f, 0.82f, 1f) : new Color(0.16f, 0.18f, 0.22f, 1f);
            label.color = Color.white;
        }
    }

    private Button CreateButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform buttonRect = CreatePanel(label, parent, new Color(0.16f, 0.18f, 0.22f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        buttonRect.name = label;
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.82f, 1f, 1f);
        button.colors = colors;

        CreateText("文本", buttonRect, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, size);
        return button;
    }

    private RectTransform CreatePanel(string objectName, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;

        return rectTransform;
    }

    private Text CreateText(string objectName, RectTransform parent, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text textComponent = textObject.GetComponent<Text>();
        textComponent.text = text;
        textComponent.font = interfaceFont;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.resizeTextForBestFit = true;
        textComponent.resizeTextMinSize = Mathf.Max(12, fontSize - 8);
        textComponent.resizeTextMaxSize = fontSize;

        return textComponent;
    }

    private static Font GetInterfaceFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}

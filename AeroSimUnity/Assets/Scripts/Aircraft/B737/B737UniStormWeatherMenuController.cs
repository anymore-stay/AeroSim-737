using System.Collections;
using System.Collections.Generic;
using UniStorm;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 调整 UniStorm 运行时天气菜单的位置和中文显示。
/// </summary>
public class B737UniStormWeatherMenuController : MonoBehaviour
{
    private const float DefaultRightPadding = 18f;
    private const float DefaultTopPadding = 18f;
    private const float DefaultVerticalSpacing = 10f;

    private static readonly Dictionary<string, string> LocalizedWeatherNames =
        new Dictionary<string, string>
        {
            { "Clear", "晴朗" },
            { "Mostly Clear", "大部晴朗" },
            { "Mostly Cloudy", "大部多云" },
            { "Partly Cloudy", "局部多云" },
            { "Cloudy", "多云" },
            { "Blowing Leaves", "落叶飞扬" },
            { "Blowing Pine Needles", "松针飞扬" },
            { "Blowing Snow", "吹雪" },
            { "Blowing Pollen", "花粉飞扬" },
            { "Foggy", "有雾" },
            { "Overcast", "阴天" },
            { "Hail", "冰雹" },
            { "Heavy Rain", "大雨" },
            { "Rain", "降雨" },
            { "Light Rain", "小雨" },
            { "Drizzle", "毛毛雨" },
            { "Heavy Snow", "大雪" },
            { "Snow", "降雪" },
            { "Light Snow", "小雪" },
            { "Thunderstorm", "雷暴" },
            { "Thunder Snow", "雷暴雪" },
            { "Lightning Bugs", "萤火虫" },
            { "Dust Storm", "沙尘暴" },
            { "Fire Rain", "火雨" },
            { "Fire Storm", "火风暴" },
            { "Red Aroras", "红色极光" },
            { "Blue Aroras", "蓝色极光" },
            { "Mostly Cloudy with Rain", "多云有雨" },
            { "Hazy", "薄雾" }
        };

    [SerializeField] private UniStormSystem uniStormSystem;
    [Header("右上角菜单布局")]
    [SerializeField] private float rightPadding = DefaultRightPadding;
    [SerializeField] private float topPadding = DefaultTopPadding;
    [SerializeField] private float verticalSpacing = DefaultVerticalSpacing;
    [SerializeField] private Vector2 weatherControlSize = new Vector2(250f, 46f);
    [SerializeField] private Vector2 timeSliderSize = new Vector2(250f, 26f);
    [SerializeField] private Vector2 timeLabelSize = new Vector2(250f, 24f);
    [SerializeField] private int timeLabelFontSize = 22;

    private Slider timeSlider;
    private Text timeLabel;

    private void Awake()
    {
        if (uniStormSystem == null)
        {
            uniStormSystem = GetComponent<UniStormSystem>();
        }
    }

    private void Start()
    {
        StartCoroutine(ConfigureMenuWhenReady());
    }

    private void LateUpdate()
    {
        if (timeSlider != null)
        {
            ConfigureTimeSlider(timeSlider);
        }

        if (timeLabel != null)
        {
            ConfigureTimeLabel(timeLabel);
            timeLabel.text = GetCurrentTimeText();
            timeLabel.gameObject.SetActive(timeSlider != null && timeSlider.gameObject.activeSelf);
        }
    }

    /// <summary>
    /// 返回天气名称的中文显示文本；未列出的自定义天气保留原名。
    /// </summary>
    public static string GetLocalizedWeatherName(string weatherName)
    {
        string localizedName;
        return LocalizedWeatherNames.TryGetValue(weatherName, out localizedName)
            ? localizedName
            : weatherName;
    }

    /// <summary>
    /// 返回 UniStorm 当前调节时间的中文显示文本。
    /// </summary>
    public static string GetTimeText(int hour, int minute)
    {
        return string.Format("当前调节时间：{0:00}:{1:00}", Mathf.Clamp(hour, 0, 23), Mathf.Clamp(minute, 0, 59));
    }

    private IEnumerator ConfigureMenuWhenReady()
    {
        yield return new WaitUntil(() => uniStormSystem != null && uniStormSystem.UniStormInitialized);
        yield return new WaitUntil(() => GameObject.Find("Weather Dropdown") != null);

        Dropdown weatherDropdown = GameObject.Find("Weather Dropdown").GetComponent<Dropdown>();
        GameObject changeButton = GameObject.Find("Change Weather Button");
        GameObject timeSliderObject = GameObject.Find("Time Slider");
        if (weatherDropdown == null || changeButton == null)
        {
            yield break;
        }

        if (timeSliderObject != null)
        {
            timeSlider = timeSliderObject.GetComponent<Slider>();
            ConfigureTimeSlider(timeSlider);
            timeLabel = CreateOrFindTimeLabel(timeSliderObject.transform.parent);
            ConfigureTimeLabel(timeLabel);
        }

        float weatherTopOffset = timeLabelSize.y + verticalSpacing + timeSliderSize.y + verticalSpacing;
        float buttonTopOffset = weatherTopOffset + weatherControlSize.y + verticalSpacing;
        ConfigureTopRightLayout(weatherDropdown.GetComponent<RectTransform>(), weatherTopOffset, weatherControlSize);
        ConfigureTopRightLayout(changeButton.GetComponent<RectTransform>(), buttonTopOffset, weatherControlSize);
        LocalizeDropdown(weatherDropdown);
        LocalizeButton(changeButton);
    }

    private void ConfigureTopRightLayout(RectTransform rectTransform, float topOffset, Vector2 size)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = new Vector2(-rightPadding, -(topPadding + topOffset));
        rectTransform.sizeDelta = size;
    }

    private void ConfigureTimeSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        ConfigureTopRightLayout(slider.GetComponent<RectTransform>(), timeLabelSize.y + verticalSpacing, timeSliderSize);
    }

    private Text CreateOrFindTimeLabel(Transform parent)
    {
        const string timeLabelName = "B737 Time Label";
        Transform existingLabel = parent.Find(timeLabelName);
        if (existingLabel != null)
        {
            Text existingText = existingLabel.GetComponent<Text>();
            if (existingText != null)
            {
                return existingText;
            }

            Destroy(existingLabel.gameObject);
        }

        GameObject labelObject = new GameObject(timeLabelName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        labelObject.AddComponent<Shadow>();
        Text label = labelObject.GetComponent<Text>();
        return label;
    }

    private void ConfigureTimeLabel(Text label)
    {
        if (label == null)
        {
            return;
        }

        ConfigureTopRightLayout(label.rectTransform, 0f, timeLabelSize);
        label.transform.SetAsLastSibling();
        label.text = GetCurrentTimeText();
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = timeLabelFontSize;
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = label.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        if (label.font == null)
        {
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    private string GetCurrentTimeText()
    {
        if (uniStormSystem == null)
        {
            return GetTimeText(0, 0);
        }

        return GetTimeText(uniStormSystem.Hour, uniStormSystem.Minute);
    }

    private void LocalizeDropdown(Dropdown weatherDropdown)
    {
        weatherDropdown.options.Clear();
        for (int index = 0; index < uniStormSystem.AllWeatherTypes.Count; index++)
        {
            WeatherType weatherType = uniStormSystem.AllWeatherTypes[index];
            if (weatherType != null)
            {
                weatherDropdown.options.Add(new Dropdown.OptionData(
                    GetLocalizedWeatherName(weatherType.WeatherTypeName)));
            }
        }

        weatherDropdown.value = Mathf.Max(0, uniStormSystem.AllWeatherTypes.IndexOf(uniStormSystem.CurrentWeatherType));
        weatherDropdown.RefreshShownValue();
    }

    private static void LocalizeButton(GameObject changeButton)
    {
        Text label = changeButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "切换天气";
        }
    }
}

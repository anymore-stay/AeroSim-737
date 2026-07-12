using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Binds one EICAS numeric value to its text readout, pointer, and swept fill.
/// </summary>
[ExecuteAlways]
public class EicasGauge : MonoBehaviour
{
    private int lastDisplayedValue = int.MinValue;
    private string lastNumberFormat;
    [Header("UI")]
    [Tooltip("要旋转的指针 RectTransform。通常是白色指针图片或细线。")]
    [SerializeField]
    private RectTransform needle;

    [Tooltip("灰色实心扇形组件。数值变化时会同步改变填充面积。")]
    [SerializeField]
    private EicasRadialFill fill;

    [Tooltip("显示当前数值的 TextMeshPro 文本。可以为空，为空时只更新指针和扇形。")]
    [SerializeField]
    private TMP_Text valueText;

    [Header("Needle Layout")]
    [Tooltip("开启后，在这个组件里控制指针的位置、长度和粗细；关闭后可以直接手动调指针 RectTransform。")]
    [SerializeField]
    private bool controlNeedleTransform = false;

    [Tooltip("指针起点在 Canvas 里的位置。灰色扇形中心也会跟随这个位置。")]
    [SerializeField]
    private Vector2 needleAnchoredPosition = Vector2.zero;

    [Tooltip("指针长度。开启同步后，灰色实心扇形半径会跟随这个长度。")]
    [SerializeField, Min(0f)]
    private float needleLength = 80f;

    [Tooltip("指针粗细。")]
    [SerializeField, Min(0.5f)]
    private float needleThickness = 2f;

    [Header("Scale")]
    [Tooltip("仪表数据最小值。当前值等于这个值时，指针位于 Start Angle，灰色扇形填充为 0。")]
    [SerializeField]
    private float minValue = 0f;

    [Tooltip("仪表数据最大值。当前值等于这个值时，指针位于 End Angle，灰色扇形填充为 100%。")]
    [SerializeField]
    private float maxValue = 100f;

    [Tooltip("最小值对应的起始角度。0 度指向右侧，负数通常表示顺时针方向。")]
    [SerializeField]
    private float startAngle = 210f;

    [Tooltip("最大值对应的结束角度。例如 Start=0、End=-180 表示顺时针扫半圈。")]
    [SerializeField]
    private float endAngle = -30f;

    [Tooltip("指针额外角度偏移。用于指针图片本身朝向不是向右时的微调。")]
    [SerializeField]
    private float needleAngleOffset = 0f;

    [Header("Fill Sync")]
    [Tooltip("开启后，灰色扇形半径会自动等于指针长度加 Fill Radius Offset。")]
    [SerializeField]
    private bool syncFillRadiusToNeedleLength = true;

    [Tooltip("开启后，灰色扇形中心会自动跟随指针起点位置。")]
    [SerializeField]
    private bool syncFillCenterToNeedlePivot = true;

    [Tooltip("灰色扇形半径微调值。正数让扇形比指针长，负数让扇形比指针短。")]
    [SerializeField]
    private float fillRadiusOffset = 0f;

    [Header("Display")]
    [Tooltip("数字显示格式。例如 0 显示整数，0.0 显示一位小数，0.00 显示两位小数。")]
    [SerializeField]
    private string numberFormat = "0.0";

    [Tooltip("编辑器预览值。还没接真实数据时，可以用它测试指针和扇形位置。")]
    [SerializeField]
    private float previewValue = 50f;

    public float Value
    {
        get => previewValue;
        set
        {
            previewValue = value;
            ApplyValue(value);
        }
    }

    public void SetValue(float value)
    {
        Value = value;
    }

    private void Start()
    {
        ApplyValue(previewValue);
    }

    private void OnEnable()
    {
        ApplyValue(previewValue);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            ApplyValue(previewValue);
        }
    }

    private void ApplyValue(float value)
    {
        float normalized = Mathf.Approximately(minValue, maxValue)
            ? 0f
            : Mathf.InverseLerp(minValue, maxValue, value);

        float angle = Mathf.Lerp(startAngle, endAngle, normalized) + needleAngleOffset;

        if (needle != null)
        {
            if (controlNeedleTransform)
            {
                Vector2 size = new Vector2(Mathf.Max(0f, needleLength), Mathf.Max(0.5f, needleThickness));
                if (needle.anchoredPosition != needleAnchoredPosition)
                {
                    needle.anchoredPosition = needleAnchoredPosition;
                }
                if (needle.sizeDelta != size)
                {
                    needle.sizeDelta = size;
                }
            }

            if (Mathf.Abs(Mathf.DeltaAngle(needle.localEulerAngles.z, angle)) > 0.01f)
            {
                needle.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        if (fill != null)
        {
            if (syncFillCenterToNeedlePivot && needle != null)
            {
                if (fill.rectTransform.anchoredPosition != needle.anchoredPosition)
                {
                    fill.rectTransform.anchoredPosition = needle.anchoredPosition;
                }
            }

            if (syncFillRadiusToNeedleLength && needle != null)
            {
                float radius = Mathf.Max(0f, needle.rect.width + fillRadiusOffset);
                fill.OuterRadius = radius;
                Vector2 fillSize = new Vector2(radius * 2f + 8f, radius * 2f + 8f);
                if (fill.rectTransform.sizeDelta != fillSize)
                {
                    fill.rectTransform.sizeDelta = fillSize;
                }
            }

            fill.StartAngle = startAngle;
            fill.EndAngle = endAngle;
            fill.SetAmount(normalized);
        }

        if (valueText != null)
        {
            int scale = GetDisplayScale(numberFormat);
            int displayedValue = Mathf.RoundToInt(value * scale);
            if (displayedValue != lastDisplayedValue || lastNumberFormat != numberFormat)
            {
                lastDisplayedValue = displayedValue;
                lastNumberFormat = numberFormat;
                valueText.text = (displayedValue / (float)scale).ToString(
                    numberFormat,
                    CultureInfo.InvariantCulture);
            }
        }
    }

    private static int GetDisplayScale(string format)
    {
        int decimalPoint = string.IsNullOrEmpty(format) ? -1 : format.IndexOf('.');
        if (decimalPoint < 0)
        {
            return 1;
        }

        int scale = 1;
        for (int i = decimalPoint + 1; i < format.Length && (format[i] == '0' || format[i] == '#'); i++)
        {
            scale *= 10;
        }
        return scale;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyValue(previewValue);
    }
#endif
}

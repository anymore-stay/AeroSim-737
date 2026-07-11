using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class PFDVerticalSpeedIndicatorController : MonoBehaviour
{
    [Header("白线中心与刻度竖线")]
    [SerializeField] private Vector2 pointerOrigin = new Vector2(188f, 68f);
    [SerializeField] private float scaleLineX = 132f;

    [Header("现有刻度的纵向位置")]
    [SerializeField] private float zeroY = 68f;
    [SerializeField] private float positiveOneY = 118f;
    [SerializeField] private float positiveTwoY = 156f;
    [SerializeField] private float positiveSixY = 182f;
    [SerializeField] private float negativeOneY = 18f;
    [SerializeField] private float negativeTwoY = -20f;
    [SerializeField] private float negativeSixY = -46f;

    [Header("白线外观")]
    [SerializeField, Min(0.1f)] private float pointerThickness = 2f;

    [Header("数值显示")]
    [SerializeField, Min(0f)] private float minimumDisplayFpm = 500f;
    [SerializeField, Min(1f)] private float displayIncrementFpm = 50f;
    [SerializeField, Min(1f)] private float maximumDisplayFpm = 9999f;
    [SerializeField] private float descendingValueY = -79f;
    [SerializeField] private RectTransform guideVerticalSpeedPointer;
    [SerializeField] private RectTransform finalVerticalSpeedPointer;
    [SerializeField] private Text guideVerticalSpeedValue;
    [SerializeField] private Text finalVerticalSpeedValue;

    private Text cachedGuideVerticalSpeedValue;
    private Text cachedFinalVerticalSpeedValue;
    private Vector2 guideAscendingValuePosition;
    private Vector2 finalAscendingValuePosition;

    private void OnEnable()
    {
        // 尚未接入模拟器或真实数据时，也先显示零垂直速度。
        SetVerticalSpeedFpm(0f);
    }

    /// <summary>
    /// 设置垂直速度，单位为英尺每分钟。后续可传入 JsbsimBridge.VerticalSpeedFps * 60f。
    /// </summary>
    public void SetVerticalSpeedFpm(float verticalSpeedFpm)
    {
        EnsureBindings();

        // 指针只能在仪表的六千英尺每分钟刻度范围内移动。
        float pointerSpeed = PFDVerticalSpeedIndicatorMath.ClampVerticalSpeedFpm(verticalSpeedFpm);
        float scaleY = PFDVerticalSpeedIndicatorMath.CalculateScaleY(
            pointerSpeed,
            zeroY,
            positiveOneY,
            positiveTwoY,
            positiveSixY,
            negativeOneY,
            negativeTwoY,
            negativeSixY);
        Vector2 scaleEndpoint = new Vector2(scaleLineX, scaleY);

        ApplyPointer(guideVerticalSpeedPointer, pointerOrigin, scaleEndpoint);
        ApplyPointer(finalVerticalSpeedPointer, pointerOrigin, scaleEndpoint);

        // 数值使用原始速度，因而能够继续显示超过六千的垂直速度。
        ApplyValue(guideVerticalSpeedValue, guideAscendingValuePosition, verticalSpeedFpm);
        ApplyValue(finalVerticalSpeedValue, finalAscendingValuePosition, verticalSpeedFpm);
    }

    private void EnsureBindings()
    {
        if (guideVerticalSpeedPointer == null
            || finalVerticalSpeedPointer == null
            || guideVerticalSpeedValue == null
            || finalVerticalSpeedValue == null)
        {
            RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rectTransform in rectTransforms)
            {
                if (guideVerticalSpeedPointer == null && rectTransform.name == "Guide_VerticalSpeedPointer")
                {
                    guideVerticalSpeedPointer = rectTransform;
                }
                else if (finalVerticalSpeedPointer == null && rectTransform.name == "Final_VerticalSpeedPointer")
                {
                    finalVerticalSpeedPointer = rectTransform;
                }
            }

            Text[] texts = GetComponentsInChildren<Text>(true);
            foreach (Text text in texts)
            {
                if (guideVerticalSpeedValue == null && text.name == "Guide_VerticalSpeedValue")
                {
                    guideVerticalSpeedValue = text;
                }
                else if (finalVerticalSpeedValue == null && text.name == "Final_VerticalSpeedValue")
                {
                    finalVerticalSpeedValue = text;
                }
            }
        }

        if (guideVerticalSpeedValue != cachedGuideVerticalSpeedValue)
        {
            cachedGuideVerticalSpeedValue = guideVerticalSpeedValue;
            guideAscendingValuePosition = GetAnchoredPosition(guideVerticalSpeedValue);
        }

        if (finalVerticalSpeedValue != cachedFinalVerticalSpeedValue)
        {
            cachedFinalVerticalSpeedValue = finalVerticalSpeedValue;
            finalAscendingValuePosition = GetAnchoredPosition(finalVerticalSpeedValue);
        }
    }

    private void ApplyPointer(RectTransform pointer, Vector2 origin, Vector2 scaleEndpoint)
    {
        if (pointer == null)
        {
            return;
        }

        pointer.anchoredPosition = PFDVerticalSpeedIndicatorMath.CalculateLineCenter(origin, scaleEndpoint);
        pointer.sizeDelta = new Vector2(
            PFDVerticalSpeedIndicatorMath.CalculateLineLength(origin, scaleEndpoint),
            pointerThickness);

        Vector3 localEulerAngles = pointer.localEulerAngles;
        localEulerAngles.z = PFDVerticalSpeedIndicatorMath.CalculateLineRotationZ(origin, scaleEndpoint);
        pointer.localEulerAngles = localEulerAngles;
    }

    private void ApplyValue(Text value, Vector2 ascendingPosition, float verticalSpeedFpm)
    {
        if (value == null)
        {
            return;
        }

        float absoluteSpeed = Mathf.Abs(verticalSpeedFpm);
        bool shouldDisplay = absoluteSpeed >= minimumDisplayFpm;
        value.enabled = shouldDisplay;
        value.alignment = TextAnchor.MiddleRight;

        if (!shouldDisplay)
        {
            value.rectTransform.anchoredPosition = ascendingPosition;
            return;
        }

        float roundedSpeed = Mathf.Round(absoluteSpeed / displayIncrementFpm) * displayIncrementFpm;
        int cappedSpeed = Mathf.Min(Mathf.RoundToInt(roundedSpeed), Mathf.RoundToInt(maximumDisplayFpm));
        value.text = cappedSpeed.ToString(CultureInfo.InvariantCulture);
        value.rectTransform.anchoredPosition = verticalSpeedFpm < 0f
            ? new Vector2(ascendingPosition.x, descendingValueY)
            : ascendingPosition;
    }

    private static Vector2 GetAnchoredPosition(Text value)
    {
        return value == null ? Vector2.zero : value.rectTransform.anchoredPosition;
    }
}

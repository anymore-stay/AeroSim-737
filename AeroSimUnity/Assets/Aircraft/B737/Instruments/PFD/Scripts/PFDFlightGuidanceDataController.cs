using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理 PFD 引导数据区的四项文字输出。
/// 后续接入外部数据时，可直接调用本类的四个公开设置方法。
/// </summary>
public class PFDFlightGuidanceDataController : MonoBehaviour
{
    [Header("默认引导数据")]
    [SerializeField] private int targetSpeedKnots = 40;
    [SerializeField] private int selectedAltitudeFeet = 0;
    [SerializeField] private float selectedMagneticHeadingDegrees = 0f;
    [SerializeField] private float barometricPressureInHg = 29.91f;

    private Text targetSpeedText;
    private Text altitudeLeadingDigitText;
    private Text altitudeRemainingDigitsText;
    private Text headingText;
    private Text barometricPressureText;
    private Text barometricUnitText;

    private void Awake()
    {
        RefreshDisplay();
    }

    private void OnEnable()
    {
        RefreshDisplay();
    }

    private void OnValidate()
    {
        RefreshDisplay();
    }

    /// <summary>
    /// 设置速度目标值，单位为节。
    /// </summary>
    public void SetTargetSpeedKnots(int speedKnots)
    {
        targetSpeedKnots = speedKnots;
        RefreshDisplay();
    }

    /// <summary>
    /// 设置 MCP 预选高度，单位为英尺。
    /// </summary>
    public void SetSelectedAltitudeFeet(int altitudeFeet)
    {
        selectedAltitudeFeet = Mathf.Max(0, altitudeFeet);
        RefreshDisplay();
    }

    /// <summary>
    /// 设置预选磁航向，单位为度。
    /// </summary>
    public void SetSelectedMagneticHeading(float headingDegrees)
    {
        selectedMagneticHeadingDegrees = headingDegrees;
        RefreshDisplay();
    }

    /// <summary>
    /// 设置高度表气压，单位为英寸汞柱。
    /// </summary>
    public void SetBarometricPressureInHg(float pressureInHg)
    {
        barometricPressureInHg = pressureInHg;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        EnsureBindings();

        if (targetSpeedText != null)
        {
            targetSpeedText.text = targetSpeedKnots.ToString(CultureInfo.InvariantCulture);
        }

        string altitudeDigits = selectedAltitudeFeet.ToString("D4", CultureInfo.InvariantCulture);
        if (altitudeLeadingDigitText != null)
        {
            altitudeLeadingDigitText.text = altitudeDigits.Substring(0, 1);
        }

        if (altitudeRemainingDigitsText != null)
        {
            altitudeRemainingDigitsText.text = altitudeDigits.Substring(1);
        }

        int roundedHeading = Mathf.RoundToInt(selectedMagneticHeadingDegrees);
        int normalizedHeading = ((roundedHeading % 360) + 360) % 360;
        if (headingText != null)
        {
            headingText.text = normalizedHeading.ToString("D3", CultureInfo.InvariantCulture);
        }

        if (barometricPressureText != null)
        {
            barometricPressureText.text = barometricPressureInHg.ToString("F2", CultureInfo.InvariantCulture);
        }

        if (barometricUnitText != null)
        {
            barometricUnitText.text = "IN.";
        }
    }

    private void EnsureBindings()
    {
        if (targetSpeedText != null
            && altitudeLeadingDigitText != null
            && altitudeRemainingDigitsText != null
            && headingText != null
            && barometricPressureText != null
            && barometricUnitText != null)
        {
            return;
        }

        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            switch (text.name)
            {
                case "Guide_TargetSpeedValue":
                    targetSpeedText = text;
                    break;
                case "Guide_SelectedAltitudeLeadingDigit":
                    altitudeLeadingDigitText = text;
                    break;
                case "Guide_SelectedAltitudeRemainingDigits":
                    altitudeRemainingDigitsText = text;
                    break;
                case "Guide_SelectedHeadingValue":
                    headingText = text;
                    break;
                case "Guide_BarometricPressureValue":
                    barometricPressureText = text;
                    break;
                case "Guide_BarometricPressureUnit":
                    barometricUnitText = text;
                    break;
            }
        }
    }
}

using UnityEngine;

public static class PFDRollingNumberMath
{
    public const float RowHeight = 24f;

    /// <summary>
    /// 计算普通十进制数字轮的位置，返回范围为 0 到 10。
    /// </summary>
    public static float CalculateDecimalWheelValue(float value, float place, float rolloverWindow)
    {
        float safeValue = Mathf.Max(0f, value);
        float safePlace = Mathf.Max(1f, place);

        if (safePlace <= 1f)
        {
            return Mathf.Repeat(safeValue, 10f);
        }

        float digit = Mathf.Floor(safeValue / safePlace) % 10f;
        float remainder = Mathf.Repeat(safeValue, safePlace);
        float window = Mathf.Clamp(rolloverWindow, 0.0001f, safePlace);
        float progress = Mathf.InverseLerp(safePlace - window, safePlace, remainder);
        return digit + progress;
    }

    /// <summary>
    /// 将海拔最后两位换算成 00、20、40、60、80 五档滚轮位置。
    /// </summary>
    public static float CalculateAltitudeTwoDigitWheelValue(float altitudeFt)
    {
        return Mathf.Repeat(Mathf.Max(0f, altitudeFt), 100f) / 20f;
    }
}

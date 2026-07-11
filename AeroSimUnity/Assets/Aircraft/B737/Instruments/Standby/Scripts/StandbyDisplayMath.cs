using UnityEngine;

/// <summary>
/// Standby 仪表的纯数学换算，避免显示脚本中混入难以验证的坐标计算。
/// </summary>
public static class StandbyDisplayMath
{
    private const float DigitRowPitchPixels = 21f;
    private const float DigitZeroCenterFromBottomPixels = 31.5f;
    private const float AltitudePairRowPitchPixels = 17f;
    private const float AltitudePairZeroCenterFromBottomPixels = 25.5f;

    public static float CalculateTapeOffset(
        float value,
        float referenceValue,
        float pixelsPerUnit,
        bool invertDirection)
    {
        float direction = invertDirection ? -1f : 1f;
        return (value - referenceValue) * Mathf.Max(0f, pixelsPerUnit) * direction;
    }

    public static Vector2 CalculateHorizonPosition(
        Vector2 basePosition,
        float pitchDegrees,
        float rollDegrees,
        float pixelsPerDegree,
        bool invertPitch,
        bool invertRoll)
    {
        float pitchDirection = invertPitch ? -1f : 1f;
        float rollDirection = invertRoll ? -1f : 1f;
        float offsetY = -pitchDegrees * Mathf.Max(0f, pixelsPerDegree) * pitchDirection;
        float rollRadians = rollDegrees * rollDirection * Mathf.Deg2Rad;

        Vector2 rotatedOffset = new Vector2(
            -offsetY * Mathf.Sin(rollRadians),
            offsetY * Mathf.Cos(rollRadians));
        return basePosition + rotatedOffset;
    }

    public static float CalculateHorizonRotation(
        float baseRotationZ,
        float rollDegrees,
        bool invertRoll)
    {
        return baseRotationZ + rollDegrees * (invertRoll ? -1f : 1f);
    }

    /// <summary>
    /// 计算普通十进制数字滚轮的位置，返回范围允许到 10 以完成 9 到 0 的连续过渡。
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

    public static float CalculateAltitudePairWheelValue(float altitudeFeet)
    {
        return Mathf.Repeat(Mathf.Max(0f, altitudeFeet), 100f) / 20f;
    }

    /// <summary>
    /// 根据转换后的三列数字图集计算单列数字轮的 UV 可见区域。
    /// </summary>
    public static Rect CalculateDigitStripUv(
        float wheelValue,
        int columnIndex,
        int columnCount,
        float viewportHeightPixels,
        float textureHeightPixels)
    {
        int safeColumnCount = Mathf.Max(1, columnCount);
        int safeColumnIndex = Mathf.Clamp(columnIndex, 0, safeColumnCount - 1);
        float safeTextureHeight = Mathf.Max(1f, textureHeightPixels);
        float uvHeight = Mathf.Clamp01(viewportHeightPixels / safeTextureHeight);
        float centerY = (DigitZeroCenterFromBottomPixels
            + Mathf.Clamp(wheelValue, 0f, 10f) * DigitRowPitchPixels) / safeTextureHeight;
        float y = Mathf.Clamp(centerY - uvHeight * 0.5f, 0f, 1f - uvHeight);
        float width = 1f / safeColumnCount;

        return new Rect(safeColumnIndex * width, y, width, uvHeight);
    }

    /// <summary>
    /// 高度末两位图集按 00、20、40、60、80 五档连续滚动。
    /// </summary>
    public static Rect CalculateAltitudePairUv(
        float wheelValue,
        float viewportHeightPixels,
        float textureHeightPixels)
    {
        float safeTextureHeight = Mathf.Max(1f, textureHeightPixels);
        float uvHeight = Mathf.Clamp01(viewportHeightPixels / safeTextureHeight);
        float centerY = (AltitudePairZeroCenterFromBottomPixels
            + Mathf.Clamp(wheelValue, 0f, 5f) * AltitudePairRowPitchPixels) / safeTextureHeight;
        float y = Mathf.Clamp(centerY - uvHeight * 0.5f, 0f, 1f - uvHeight);
        return new Rect(0f, y, 1f, uvHeight);
    }
}

using UnityEngine;

public static class PFDAltitudeTapeMath
{
    /// <summary>
    /// 将输入高度限制在高度带贴图支持的范围内。
    /// </summary>
    public static float ClampAltitude(float altitudeFt, float minimumAltitudeFt, float maximumAltitudeFt)
    {
        float minimum = Mathf.Min(minimumAltitudeFt, maximumAltitudeFt);
        float maximum = Mathf.Max(minimumAltitudeFt, maximumAltitudeFt);
        return Mathf.Clamp(altitudeFt, minimum, maximum);
    }

    /// <summary>
    /// 将高度换算成高度带内容层的纵向坐标。
    /// </summary>
    public static float CalculateContentY(
        float altitudeFt,
        float minimumAltitudeFt,
        float maximumAltitudeFt,
        float pixelsPerFoot,
        float referenceAltitudeFt,
        float referenceContentY,
        bool invertDirection)
    {
        float clampedAltitude = ClampAltitude(altitudeFt, minimumAltitudeFt, maximumAltitudeFt);
        float direction = invertDirection ? -1f : 1f;

        return referenceContentY
            + (clampedAltitude - referenceAltitudeFt) * pixelsPerFoot * direction;
    }
}

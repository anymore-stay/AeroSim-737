using UnityEngine;

public static class PFDAirspeedTapeMath
{
    /// <summary>
    /// 将输入空速限制在空速带贴图支持的范围内。
    /// </summary>
    public static float ClampAirspeed(float airspeedKts, float minimumAirspeedKts, float maximumAirspeedKts)
    {
        float minimum = Mathf.Min(minimumAirspeedKts, maximumAirspeedKts);
        float maximum = Mathf.Max(minimumAirspeedKts, maximumAirspeedKts);
        return Mathf.Clamp(airspeedKts, minimum, maximum);
    }

    /// <summary>
    /// 将空速换算为相对于 Prefab 校准位置的纵向偏移。
    /// </summary>
    public static float CalculateContentOffsetY(
        float airspeedKts,
        float minimumAirspeedKts,
        float maximumAirspeedKts,
        float pixelsPerKnot,
        float referenceAirspeedKts,
        bool invertDirection)
    {
        float clampedAirspeed = ClampAirspeed(
            airspeedKts,
            minimumAirspeedKts,
            maximumAirspeedKts);
        float direction = invertDirection ? -1f : 1f;

        return (clampedAirspeed - referenceAirspeedKts) * pixelsPerKnot * direction;
    }
}

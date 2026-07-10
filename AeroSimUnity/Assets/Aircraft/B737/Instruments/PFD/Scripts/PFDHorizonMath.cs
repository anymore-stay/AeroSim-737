using UnityEngine;

public static class PFDHorizonMath
{
    public static Vector2 CalculateAnchoredPosition(
        Vector2 basePosition,
        float pitchDeg,
        float rollDeg,
        float pixelsPerDegree,
        bool invertPitch,
        bool invertRoll)
    {
        float pitchDirection = invertPitch ? -1f : 1f;
        float rollDirection = invertRoll ? -1f : 1f;
        float offsetY = -pitchDeg * pixelsPerDegree * pitchDirection;
        float rollRadians = rollDeg * rollDirection * Mathf.Deg2Rad;

        Vector2 rotatedOffset = new Vector2(
            -offsetY * Mathf.Sin(rollRadians),
            offsetY * Mathf.Cos(rollRadians));

        return basePosition + rotatedOffset;
    }

    public static float CalculateRotationZ(float baseRotationZ, float rollDeg, bool invertRoll)
    {
        float rollDirection = invertRoll ? -1f : 1f;
        return baseRotationZ + rollDeg * rollDirection;
    }

    public static Vector2 RotatePointAroundCenter(
        Vector2 basePosition,
        Vector2 center,
        float rollDeg,
        bool invertRoll)
    {
        float rollDirection = invertRoll ? -1f : 1f;
        float rollRadians = rollDeg * rollDirection * Mathf.Deg2Rad;
        Vector2 offset = basePosition - center;

        Vector2 rotatedOffset = new Vector2(
            offset.x * Mathf.Cos(rollRadians) - offset.y * Mathf.Sin(rollRadians),
            offset.x * Mathf.Sin(rollRadians) + offset.y * Mathf.Cos(rollRadians));

        return center + rotatedOffset;
    }
}

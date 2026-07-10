using UnityEngine;

public static class PFDVerticalSpeedIndicatorMath
{
    /// <summary>
    /// 按照 0、1、2、6 千英尺每分钟的实际刻度位置，连续计算指针终点的纵向坐标。
    /// </summary>
    public static float CalculateScaleY(
        float verticalSpeedFpm,
        float zeroY,
        float positiveOneY,
        float positiveTwoY,
        float positiveSixY,
        float negativeOneY,
        float negativeTwoY,
        float negativeSixY)
    {
        if (verticalSpeedFpm >= 0f)
        {
            float speed = Mathf.Clamp(verticalSpeedFpm, 0f, 6000f);
            return CalculatePositiveScaleY(speed, zeroY, positiveOneY, positiveTwoY, positiveSixY);
        }

        float descendingSpeed = Mathf.Clamp(-verticalSpeedFpm, 0f, 6000f);
        return CalculateNegativeScaleY(
            descendingSpeed,
            zeroY,
            negativeOneY,
            negativeTwoY,
            negativeSixY);
    }

    /// <summary>
    /// 将垂直速度限制到仪表的上下最大刻度范围。
    /// </summary>
    public static float ClampVerticalSpeedFpm(float verticalSpeedFpm)
    {
        return Mathf.Clamp(verticalSpeedFpm, -6000f, 6000f);
    }

    /// <summary>
    /// 计算白线的中心位置，使白线的一端始终落在刻度竖线。
    /// </summary>
    public static Vector2 CalculateLineCenter(Vector2 origin, Vector2 scaleEndpoint)
    {
        return (origin + scaleEndpoint) * 0.5f;
    }

    /// <summary>
    /// 计算白线长度。
    /// </summary>
    public static float CalculateLineLength(Vector2 origin, Vector2 scaleEndpoint)
    {
        return Vector2.Distance(origin, scaleEndpoint);
    }

    /// <summary>
    /// 计算白线从中心向刻度终点旋转的角度。
    /// </summary>
    public static float CalculateLineRotationZ(Vector2 origin, Vector2 scaleEndpoint)
    {
        Vector2 direction = scaleEndpoint - origin;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private static float CalculatePositiveScaleY(
        float speed,
        float zeroY,
        float oneY,
        float twoY,
        float sixY)
    {
        if (speed <= 1000f)
        {
            return Mathf.Lerp(zeroY, oneY, speed / 1000f);
        }

        if (speed <= 2000f)
        {
            return Mathf.Lerp(oneY, twoY, (speed - 1000f) / 1000f);
        }

        return Mathf.Lerp(twoY, sixY, (speed - 2000f) / 4000f);
    }

    private static float CalculateNegativeScaleY(
        float speed,
        float zeroY,
        float oneY,
        float twoY,
        float sixY)
    {
        if (speed <= 1000f)
        {
            return Mathf.Lerp(zeroY, oneY, speed / 1000f);
        }

        if (speed <= 2000f)
        {
            return Mathf.Lerp(oneY, twoY, (speed - 1000f) / 1000f);
        }

        return Mathf.Lerp(twoY, sixY, (speed - 2000f) / 4000f);
    }
}

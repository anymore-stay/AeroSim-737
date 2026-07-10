using UnityEngine;

public static class PFDAngleOfAttackGaugeMath
{
    /// <summary>
    /// 将迎角限制在仪表允许范围内，再映射到指针可旋转的扇形角度。
    /// </summary>
    public static float CalculatePointerRotationZ(
        float angleOfAttackDeg,
        float minimumAoaDeg,
        float maximumAoaDeg,
        float minimumPointerRotationZ,
        float maximumPointerRotationZ)
    {
        float minimum = Mathf.Min(minimumAoaDeg, maximumAoaDeg);
        float maximum = Mathf.Max(minimumAoaDeg, maximumAoaDeg);

        if (Mathf.Approximately(minimum, maximum))
        {
            return minimumPointerRotationZ;
        }

        float clampedAoa = Mathf.Clamp(angleOfAttackDeg, minimum, maximum);
        float normalizedAoa = Mathf.InverseLerp(minimum, maximum, clampedAoa);
        return Mathf.Lerp(minimumPointerRotationZ, maximumPointerRotationZ, normalizedAoa);
    }

    /// <summary>
    /// 返回用于显示的迎角，确保数值与指针使用相同的有效范围。
    /// </summary>
    public static float ClampAngleOfAttack(
        float angleOfAttackDeg,
        float minimumAoaDeg,
        float maximumAoaDeg)
    {
        return Mathf.Clamp(
            angleOfAttackDeg,
            Mathf.Min(minimumAoaDeg, maximumAoaDeg),
            Mathf.Max(minimumAoaDeg, maximumAoaDeg));
    }
}

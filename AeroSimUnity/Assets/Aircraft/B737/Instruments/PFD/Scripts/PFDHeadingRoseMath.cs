using UnityEngine;

public static class PFDHeadingRoseMath
{
    /// <summary>
    /// 根据磁航向计算转盘角度。飞机航向增加时，刻度盘向相反方向旋转。
    /// </summary>
    public static float CalculateRotationZ(float baseRotationZ, float magneticHeadingDeg)
    {
        return baseRotationZ - Mathf.Repeat(magneticHeadingDeg, 360f);
    }
}

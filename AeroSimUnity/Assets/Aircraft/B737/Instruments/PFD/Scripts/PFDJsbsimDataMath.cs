using UnityEngine;

/// <summary>
/// 集中处理 JSBSim 数据接入 PFD 时需要的单位和航向换算。
/// </summary>
public static class PFDJsbsimDataMath
{
    /// <summary>
    /// 把真航向换算为磁航向。磁差东偏为正。
    /// </summary>
    public static float CalculateMagneticHeading(float trueHeadingDeg, float magneticVariationDeg)
    {
        return Mathf.Repeat(trueHeadingDeg - magneticVariationDeg, 360f);
    }

    /// <summary>
    /// 把英尺每秒换算为英尺每分钟。
    /// </summary>
    public static float ConvertVerticalSpeedToFpm(float verticalSpeedFps)
    {
        return verticalSpeedFps * 60f;
    }
}

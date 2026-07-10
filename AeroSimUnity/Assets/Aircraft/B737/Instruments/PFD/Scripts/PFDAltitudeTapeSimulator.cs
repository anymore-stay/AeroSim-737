using UnityEngine;

public class PFDAltitudeTapeSimulator : MonoBehaviour
{
    public enum SimulationMode
    {
        Manual,
        Automatic
    }

    [SerializeField] private PFDAltitudeTapeController controller;
    [SerializeField] private SimulationMode mode = SimulationMode.Manual;
    [SerializeField, Range(-1000f, 50000f)] private float simulatedAltitudeFt;
    [SerializeField] private float automaticMinimumAltitudeFt;
    [SerializeField] private float automaticMaximumAltitudeFt = 10000f;
    [SerializeField, Min(0.1f)] private float automaticRoundTripSeconds = 12f;

    private float automaticStartTime;

    /// <summary>
    /// 计算自动模式在指定时间点的模拟高度，一个周期完成一次往返。
    /// </summary>
    public static float EvaluateAutomaticAltitude(
        float time,
        float minimumAltitudeFt,
        float maximumAltitudeFt,
        float roundTripSeconds)
    {
        float minimum = Mathf.Min(minimumAltitudeFt, maximumAltitudeFt);
        float maximum = Mathf.Max(minimumAltitudeFt, maximumAltitudeFt);
        float range = maximum - minimum;

        if (range <= 0f || roundTripSeconds <= 0f)
        {
            return minimum;
        }

        float distance = time * range * 2f / roundTripSeconds;
        return minimum + Mathf.PingPong(distance, range);
    }

    private void OnEnable()
    {
        // 每次开始预览时都从自动区间的最低高度重新起步。
        mode = SimulationMode.Automatic;
        automaticStartTime = Time.time;
        EnsureController();

        if (controller != null)
        {
            controller.SetAltitude(automaticMinimumAltitudeFt);
        }
    }

    private void Update()
    {
        EnsureController();

        if (controller == null)
        {
            return;
        }

        float altitudeFt = mode == SimulationMode.Manual
            ? simulatedAltitudeFt
            : EvaluateAutomaticAltitude(
                Time.time - automaticStartTime,
                automaticMinimumAltitudeFt,
                automaticMaximumAltitudeFt,
                automaticRoundTripSeconds);

        controller.SetAltitude(altitudeFt);
    }

    private void EnsureController()
    {
        if (controller == null)
        {
            controller = GetComponent<PFDAltitudeTapeController>();
        }
    }
}

using UnityEngine;

public class PFDAirspeedTapeSimulator : MonoBehaviour
{
    public enum SimulationMode
    {
        Manual,
        Automatic
    }

    [SerializeField] private PFDAirspeedTapeController controller;
    [SerializeField] private SimulationMode mode = SimulationMode.Automatic;
    [SerializeField, Range(40f, 440f)] private float simulatedAirspeedKts = 120f;
    [SerializeField] private float automaticMinimumAirspeedKts = 40f;
    [SerializeField] private float automaticMaximumAirspeedKts = 200f;
    [SerializeField, Min(0.1f)] private float automaticRoundTripSeconds = 120f;

    private float automaticStartTime;

    /// <summary>
    /// 计算自动模式在指定时间点的模拟空速，一个周期完成一次往返。
    /// </summary>
    public static float EvaluateAutomaticAirspeed(
        float time,
        float minimumAirspeedKts,
        float maximumAirspeedKts,
        float roundTripSeconds)
    {
        float minimum = Mathf.Min(minimumAirspeedKts, maximumAirspeedKts);
        float maximum = Mathf.Max(minimumAirspeedKts, maximumAirspeedKts);
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
        // 保留 Inspector 参数，只重置本次自动预览的计时起点。
        automaticStartTime = Time.time;
        EnsureController();

        if (controller != null)
        {
            float startingAirspeedKts = mode == SimulationMode.Manual
                ? simulatedAirspeedKts
                : automaticMinimumAirspeedKts;
            controller.SetAirspeed(startingAirspeedKts);
        }
    }

    private void Update()
    {
        EnsureController();

        if (controller == null)
        {
            return;
        }

        float airspeedKts = mode == SimulationMode.Manual
            ? simulatedAirspeedKts
            : EvaluateAutomaticAirspeed(
                Time.time - automaticStartTime,
                automaticMinimumAirspeedKts,
                automaticMaximumAirspeedKts,
                automaticRoundTripSeconds);

        controller.SetAirspeed(airspeedKts);
    }

    private void EnsureController()
    {
        if (controller == null)
        {
            controller = GetComponent<PFDAirspeedTapeController>();
        }
    }
}

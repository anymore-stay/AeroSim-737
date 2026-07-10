using UnityEngine;

public class PFDAngleOfAttackGaugeSimulator : MonoBehaviour
{
    public enum SimulationMode
    {
        Manual,
        Automatic
    }

    [SerializeField] private PFDAngleOfAttackGaugeController controller;
    [SerializeField] private SimulationMode mode = SimulationMode.Automatic;
    [SerializeField, Range(-10f, 30f)] private float simulatedAoaDeg;
    [SerializeField] private float automaticMinimumAoaDeg;
    [SerializeField] private float automaticMaximumAoaDeg = 15f;
    [SerializeField, Min(0.1f)] private float automaticRoundTripSeconds = 20f;

    private float automaticStartTime;

    /// <summary>
    /// 计算自动预览迎角，一个周期内由最小值平滑往返到最大值。
    /// </summary>
    public static float EvaluateAutomaticAoa(
        float elapsedSeconds,
        float minimumAoaDeg,
        float maximumAoaDeg,
        float roundTripSeconds)
    {
        float minimum = Mathf.Min(minimumAoaDeg, maximumAoaDeg);
        float maximum = Mathf.Max(minimumAoaDeg, maximumAoaDeg);
        float range = maximum - minimum;

        if (range <= 0f || roundTripSeconds <= 0f)
        {
            return minimum;
        }

        float distance = elapsedSeconds * range * 2f / roundTripSeconds;
        return minimum + Mathf.PingPong(distance, range);
    }

    private void OnEnable()
    {
        // 自动模式每次启用都从最小迎角开始，便于观察完整指针行程。
        automaticStartTime = Time.time;
        EnsureController();

        if (controller != null)
        {
            controller.SetAngleOfAttack(
                mode == SimulationMode.Manual ? simulatedAoaDeg : automaticMinimumAoaDeg);
        }
    }

    private void Update()
    {
        EnsureController();

        if (controller == null)
        {
            return;
        }

        float aoaDeg = mode == SimulationMode.Manual
            ? simulatedAoaDeg
            : EvaluateAutomaticAoa(
                Time.time - automaticStartTime,
                automaticMinimumAoaDeg,
                automaticMaximumAoaDeg,
                automaticRoundTripSeconds);

        controller.SetAngleOfAttack(aoaDeg);
    }

    private void EnsureController()
    {
        if (controller == null)
        {
            controller = GetComponent<PFDAngleOfAttackGaugeController>();
        }
    }
}

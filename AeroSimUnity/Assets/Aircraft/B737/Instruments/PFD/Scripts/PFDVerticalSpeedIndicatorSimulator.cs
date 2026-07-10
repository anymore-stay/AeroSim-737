using UnityEngine;

public class PFDVerticalSpeedIndicatorSimulator : MonoBehaviour
{
    public enum SimulationMode
    {
        Manual,
        Automatic
    }

    [SerializeField] private PFDVerticalSpeedIndicatorController controller;
    [SerializeField] private SimulationMode mode = SimulationMode.Automatic;
    // 手动模拟值不设人工上限，便于验证超过仪表刻度的显示效果。
    [SerializeField] private float simulatedVerticalSpeedFpm;
    // 自动模拟振幅不设人工上限；默认越过六千刻度，数值显示仍由控制器封顶。
    [SerializeField, Min(0f)] private float automaticMaximumFpm = 12000f;
    [SerializeField, Min(0.1f)] private float automaticCycleSeconds = 24f;

    private float automaticStartTime;

    /// <summary>
    /// 计算自动预览垂直速度。一个周期依次经过零、最大爬升、零、最大下降。
    /// </summary>
    public static float EvaluateAutomaticVerticalSpeed(
        float elapsedSeconds,
        float maximumFpm,
        float cycleSeconds)
    {
        if (maximumFpm <= 0f || cycleSeconds <= 0f)
        {
            return 0f;
        }

        return Mathf.Sin(elapsedSeconds * Mathf.PI * 2f / cycleSeconds) * maximumFpm;
    }

    private void OnEnable()
    {
        // 自动模式从零垂直速度开始，便于直接观察中心基准位置。
        automaticStartTime = Time.time;
        EnsureController();

        if (controller != null)
        {
            controller.SetVerticalSpeedFpm(
                mode == SimulationMode.Manual ? simulatedVerticalSpeedFpm : 0f);
        }
    }

    private void Update()
    {
        EnsureController();

        if (controller == null)
        {
            return;
        }

        float verticalSpeedFpm = mode == SimulationMode.Manual
            ? simulatedVerticalSpeedFpm
            : EvaluateAutomaticVerticalSpeed(
                Time.time - automaticStartTime,
                automaticMaximumFpm,
                automaticCycleSeconds);

        controller.SetVerticalSpeedFpm(verticalSpeedFpm);
    }

    private void EnsureController()
    {
        if (controller == null)
        {
            controller = GetComponent<PFDVerticalSpeedIndicatorController>();
        }
    }
}

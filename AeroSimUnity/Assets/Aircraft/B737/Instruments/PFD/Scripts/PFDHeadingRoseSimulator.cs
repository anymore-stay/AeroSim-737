using UnityEngine;

public class PFDHeadingRoseSimulator : MonoBehaviour
{
    public enum SimulationMode
    {
        Manual,
        Automatic
    }

    [SerializeField] private PFDHeadingRoseController controller;
    [SerializeField] private SimulationMode mode = SimulationMode.Automatic;
    [SerializeField, Range(0f, 360f)] private float simulatedHeadingDeg;
    [SerializeField, Min(0.1f)] private float automaticTurnSeconds = 120f;

    private float automaticStartTime;

    /// <summary>
    /// 计算自动预览航向。到达 360 度后从 0 度继续循环。
    /// </summary>
    public static float EvaluateAutomaticHeading(float elapsedSeconds, float turnSeconds)
    {
        if (turnSeconds <= 0f)
        {
            return 0f;
        }

        return Mathf.Repeat(elapsedSeconds * 360f / turnSeconds, 360f);
    }

    private void OnEnable()
    {
        // 每次启用都从 0 度开始，Inspector 中的模式和速度保持不变。
        automaticStartTime = Time.time;
        EnsureController();

        if (controller != null)
        {
            controller.SetMagneticHeading(
                mode == SimulationMode.Manual ? simulatedHeadingDeg : 0f);
        }
    }

    private void Update()
    {
        EnsureController();

        if (controller == null)
        {
            return;
        }

        float headingDeg = mode == SimulationMode.Manual
            ? simulatedHeadingDeg
            : EvaluateAutomaticHeading(Time.time - automaticStartTime, automaticTurnSeconds);

        controller.SetMagneticHeading(headingDeg);
    }

    private void EnsureController()
    {
        if (controller == null)
        {
            controller = GetComponent<PFDHeadingRoseController>();
        }
    }
}

using UnityEngine;

/// <summary>
/// Standby_demo 专用模拟数据源，迁移到主场景后可直接禁用或移除。
/// </summary>
public class StandbyDemoDataSource : MonoBehaviour
{
    [SerializeField] private StandbyDisplayController displayController;
    [SerializeField] private float speedMinimumKnots;
    [SerializeField] private float speedMaximumKnots = 240f;
    [SerializeField] private float speedCycleSeconds = 30f;
    [SerializeField] private float altitudeMinimumFeet;
    [SerializeField] private float altitudeMaximumFeet = 12000f;
    [SerializeField] private float altitudeCycleSeconds = 42f;
    [SerializeField] private float maximumPitchDegrees = 18f;
    [SerializeField] private float maximumRollDegrees = 35f;
    [SerializeField] private float attitudeCycleSeconds = 24f;
    [SerializeField] private float headingCycleSeconds = 60f;

    private float elapsedSeconds;

    private void Awake()
    {
        if (displayController == null)
        {
            displayController = GetComponent<StandbyDisplayController>();
        }

        ApplyInitialValues();
    }

    private void Update()
    {
        if (displayController == null)
        {
            return;
        }

        elapsedSeconds += Time.deltaTime;
        float speedBlend = SmoothPingPong(elapsedSeconds, speedCycleSeconds);
        float altitudeBlend = SmoothPingPong(elapsedSeconds, altitudeCycleSeconds);
        float attitudePhase = elapsedSeconds / Mathf.Max(1f, attitudeCycleSeconds) * Mathf.PI * 2f;

        displayController.SetAirspeedKnots(Mathf.Lerp(speedMinimumKnots, speedMaximumKnots, speedBlend));
        displayController.SetAltitudeFeet(Mathf.Lerp(altitudeMinimumFeet, altitudeMaximumFeet, altitudeBlend));
        displayController.SetAttitudeDegrees(
            Mathf.Sin(attitudePhase) * maximumPitchDegrees,
            Mathf.Sin(attitudePhase * 0.73f) * maximumRollDegrees);
        displayController.SetMagneticHeadingDegrees(
            Mathf.Repeat(elapsedSeconds / Mathf.Max(1f, headingCycleSeconds) * 360f, 360f));
    }

    private void ApplyInitialValues()
    {
        if (displayController == null)
        {
            return;
        }

        displayController.SetAirspeedKnots(speedMinimumKnots);
        displayController.SetAltitudeFeet(altitudeMinimumFeet);
        displayController.SetAttitudeDegrees(0f, 0f);
        displayController.SetMagneticHeadingDegrees(0f);
    }

    private static float SmoothPingPong(float elapsed, float cycleSeconds)
    {
        float phase = elapsed / Mathf.Max(1f, cycleSeconds) * Mathf.PI * 2f;
        return 0.5f - 0.5f * Mathf.Cos(phase);
    }
}

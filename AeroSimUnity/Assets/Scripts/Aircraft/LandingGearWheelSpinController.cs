using UnityEngine;

/// <summary>
/// Spins a visible landing gear wheel while the gear is extended.
/// Steering/retraction and wheel rolling are intentionally split across different transforms:
/// steering or retraction uses the strut/root, rolling uses the wheel's own empty parent.
/// </summary>
public class LandingGearWheelSpinController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform wheelRoot;
    [SerializeField] private Transform spinPivot;
    [SerializeField] private LandingGearHingeRetractionController gearRetraction;

    [Header("Spin")]
    [SerializeField] private bool enableVisualSpin = true;
    [SerializeField] private Vector3 localSpinAxis = Vector3.right;
    [SerializeField] private float wheelRadiusMeters = 0.34f;
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private bool invertSpin;
    [SerializeField] private bool requireGearFullyExtended = true;
    [SerializeField] private bool requireNearGround = true;
    [SerializeField] private float maxGroundAglFt = 15f;
    [SerializeField] private float minimumSpinSpeedKts = 1f;
    [SerializeField] private float maximumVisualSpinSpeedKts = 25f;
    [SerializeField] private float maximumVisualDegreesPerSecond = 720f;
    [SerializeField] private float visualSpeedResponse = 6f;

    [Header("Manual Test")]
    [SerializeField] private bool useManualSpeedWhenNoJsbsim;
    [SerializeField] private float manualSpeedKts = 25f;

    [Header("Debug")]
    [SerializeField] private float spinAngle;
    [SerializeField] private float visualSpeedMetersPerSecond;

    private Quaternion neutralPivotLocalRotation;

    private void Awake()
    {
        if (wheelRoot == null)
        {
            wheelRoot = transform;
        }

        if (spinPivot == null)
        {
            enableVisualSpin = false;
            return;
        }

        neutralPivotLocalRotation = spinPivot.localRotation;
    }

    private void LateUpdate()
    {
        float targetSpeedMetersPerSecond = enableVisualSpin && CanSpin() ? GetWheelSpeedMetersPerSecond() : 0f;
        float response = Mathf.Max(0.01f, visualSpeedResponse);
        visualSpeedMetersPerSecond = Mathf.MoveTowards(
            visualSpeedMetersPerSecond,
            targetSpeedMetersPerSecond,
            response * Time.deltaTime);

        if (visualSpeedMetersPerSecond <= 0.001f)
        {
            ApplySpin();
            return;
        }

        float safeRadius = Mathf.Max(0.01f, wheelRadiusMeters);
        float degreesPerSecond = visualSpeedMetersPerSecond / safeRadius * Mathf.Rad2Deg * speedMultiplier;
        if (maximumVisualDegreesPerSecond > 0f)
        {
            degreesPerSecond = Mathf.Min(degreesPerSecond, maximumVisualDegreesPerSecond);
        }

        if (invertSpin)
        {
            degreesPerSecond = -degreesPerSecond;
        }

        spinAngle = Mathf.Repeat(spinAngle + degreesPerSecond * Time.deltaTime, 360f);
        ApplySpin();
    }

    private bool CanSpin()
    {
        bool gearReady = !requireGearFullyExtended || gearRetraction == null || gearRetraction.IsFullyExtended;
        return gearReady && IsNearGround();
    }

    private bool IsNearGround()
    {
        if (!requireNearGround)
        {
            return true;
        }

        JsbsimBridge bridge = JsbsimBridge.Instance;
        if (bridge == null || !bridge.HasState)
        {
            return true;
        }

        return bridge.AglFt <= maxGroundAglFt;
    }

    private float GetWheelSpeedMetersPerSecond()
    {
        JsbsimBridge bridge = JsbsimBridge.Instance;
        if (bridge != null && bridge.HasState)
        {
            float speedKts = bridge.TrueSpeedKts > 0.1f ? bridge.TrueSpeedKts : bridge.SpeedKts;
            if (speedKts < minimumSpinSpeedKts)
            {
                return 0f;
            }

            if (maximumVisualSpinSpeedKts > 0f)
            {
                speedKts = Mathf.Min(speedKts, maximumVisualSpinSpeedKts);
            }

            return speedKts * 0.514444f;
        }

        if (!useManualSpeedWhenNoJsbsim || manualSpeedKts < minimumSpinSpeedKts)
        {
            return 0f;
        }

        float effectiveManualSpeedKts = manualSpeedKts;
        if (maximumVisualSpinSpeedKts > 0f)
        {
            effectiveManualSpeedKts = Mathf.Min(effectiveManualSpeedKts, maximumVisualSpinSpeedKts);
        }

        return effectiveManualSpeedKts * 0.514444f;
    }

    private void ApplySpin()
    {
        if (spinPivot == null)
        {
            return;
        }

        Vector3 axis = localSpinAxis.sqrMagnitude > 0.001f ? localSpinAxis.normalized : Vector3.right;
        spinPivot.localRotation = neutralPivotLocalRotation * Quaternion.AngleAxis(spinAngle, axis);
    }
}

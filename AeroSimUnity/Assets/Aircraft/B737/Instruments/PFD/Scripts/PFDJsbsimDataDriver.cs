using UnityEngine;

/// <summary>
/// 把 JsbsimBridge 的实时飞行状态统一分发给一套 PFD 控制器。
/// </summary>
public class PFDJsbsimDataDriver : MonoBehaviour
{
    [Header("数据来源")]
    [SerializeField] private JsbsimBridge bridge;
    [Tooltip("本项目当前输出真航向。磁差东偏为正，磁航向等于真航向减磁差。")]
    [SerializeField] private float magneticVariationDeg;

    [Header("PFD 控制器")]
    [SerializeField] private PFDAirspeedTapeController airspeedController;
    [SerializeField] private PFDAltitudeTapeController altitudeController;
    [SerializeField] private PFDHorizonController horizonController;
    [SerializeField] private PFDHeadingRoseController headingController;
    [SerializeField] private PFDAngleOfAttackGaugeController angleOfAttackController;
    [SerializeField] private PFDVerticalSpeedIndicatorController verticalSpeedController;

    [Header("断流诊断")]
    [SerializeField, Min(0.1f)] private float staleWarningSeconds = 1f;
    [SerializeField] private bool logStaleWarning = true;

    private JsbsimBridge subscribedBridge;
    private float lastStateUpdateTime;
    private bool hasReceivedState;
    private bool staleWarningLogged;

    private void Awake()
    {
        EnsureControllerBindings();
        DisableSimulationComponents();
    }

    private void OnEnable()
    {
        EnsureControllerBindings();
        DisableSimulationComponents();
        ApplyZeroState();
        TryBindBridge();
    }

    private void Update()
    {
        if (subscribedBridge == null)
        {
            TryBindBridge();
        }

        if (logStaleWarning
            && hasReceivedState
            && !staleWarningLogged
            && Time.unscaledTime - lastStateUpdateTime > staleWarningSeconds)
        {
            staleWarningLogged = true;
            Debug.LogWarning("[PFD] JSBSim 状态超过指定时间未更新，PFD 将冻结最后一次有效值。", this);
        }
    }

    private void OnDisable()
    {
        UnbindBridge();
    }

    private void TryBindBridge()
    {
        JsbsimBridge targetBridge = bridge != null ? bridge : JsbsimBridge.Instance;
        if (targetBridge == null || targetBridge == subscribedBridge)
        {
            return;
        }

        UnbindBridge();
        subscribedBridge = targetBridge;
        subscribedBridge.OnStateUpdated += HandleStateUpdated;

        if (subscribedBridge.HasState)
        {
            ApplyBridgeState(subscribedBridge);
        }
    }

    private void UnbindBridge()
    {
        if (subscribedBridge != null)
        {
            subscribedBridge.OnStateUpdated -= HandleStateUpdated;
            subscribedBridge = null;
        }
    }

    private void HandleStateUpdated()
    {
        if (subscribedBridge != null)
        {
            ApplyBridgeState(subscribedBridge);
        }
    }

    private void ApplyBridgeState(JsbsimBridge state)
    {
        EnsureControllerBindings();

        if (airspeedController != null)
        {
            airspeedController.SetAirspeed(state.SpeedKts);
        }

        if (altitudeController != null)
        {
            altitudeController.SetAltitude(state.AltitudeFt);
        }

        if (horizonController != null)
        {
            horizonController.SetAttitude(state.PitchDeg, state.RollDeg);
        }

        if (headingController != null)
        {
            headingController.SetMagneticHeading(
                PFDJsbsimDataMath.CalculateMagneticHeading(
                    state.HeadingDeg,
                    magneticVariationDeg));
        }

        if (angleOfAttackController != null)
        {
            angleOfAttackController.SetAngleOfAttack(state.AngleOfAttackDeg);
        }

        if (verticalSpeedController != null)
        {
            verticalSpeedController.SetVerticalSpeedFpm(
                PFDJsbsimDataMath.ConvertVerticalSpeedToFpm(state.VerticalSpeedFps));
        }

        hasReceivedState = true;
        staleWarningLogged = false;
        lastStateUpdateTime = Time.unscaledTime;
    }

    private void ApplyZeroState()
    {
        if (airspeedController != null)
        {
            airspeedController.SetAirspeed(0f);
        }

        if (altitudeController != null)
        {
            altitudeController.SetAltitude(0f);
        }

        if (horizonController != null)
        {
            horizonController.SetAttitude(0f, 0f);
        }

        if (headingController != null)
        {
            headingController.SetMagneticHeading(
                PFDJsbsimDataMath.CalculateMagneticHeading(0f, magneticVariationDeg));
        }

        if (angleOfAttackController != null)
        {
            angleOfAttackController.SetAngleOfAttack(0f);
        }

        if (verticalSpeedController != null)
        {
            verticalSpeedController.SetVerticalSpeedFpm(0f);
        }
    }

    private void EnsureControllerBindings()
    {
        if (airspeedController == null)
        {
            airspeedController = GetComponent<PFDAirspeedTapeController>();
        }

        if (altitudeController == null)
        {
            altitudeController = GetComponent<PFDAltitudeTapeController>();
        }

        if (horizonController == null)
        {
            horizonController = GetComponent<PFDHorizonController>();
        }

        if (headingController == null)
        {
            headingController = GetComponent<PFDHeadingRoseController>();
        }

        if (angleOfAttackController == null)
        {
            angleOfAttackController = GetComponent<PFDAngleOfAttackGaugeController>();
        }

        if (verticalSpeedController == null)
        {
            verticalSpeedController = GetComponent<PFDVerticalSpeedIndicatorController>();
        }
    }

    private void DisableSimulationComponents()
    {
        DisableSimulator<PFDAirspeedTapeSimulator>();
        DisableSimulator<PFDAltitudeTapeSimulator>();
        DisableSimulator<PFDAttitudeSimulator>();
        DisableSimulator<PFDHeadingRoseSimulator>();
        DisableSimulator<PFDAngleOfAttackGaugeSimulator>();
        DisableSimulator<PFDVerticalSpeedIndicatorSimulator>();
    }

    private void DisableSimulator<T>() where T : Behaviour
    {
        T simulator = GetComponent<T>();
        if (simulator != null)
        {
            simulator.enabled = false;
        }
    }
}

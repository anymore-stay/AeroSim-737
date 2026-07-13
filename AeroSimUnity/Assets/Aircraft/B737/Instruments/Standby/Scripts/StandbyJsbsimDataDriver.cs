using UnityEngine;

/// <summary>
/// 把 JSBSim 实时飞行状态分发给备用仪表。
/// </summary>
public class StandbyJsbsimDataDriver : MonoBehaviour
{
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private StandbyDisplayController displayController;
    [Tooltip("本项目当前输出真航向。磁差东偏为正，磁航向等于真航向减磁差。")]
    [SerializeField] private float magneticVariationDeg;
    [SerializeField, Min(0.01f)] private float displayRefreshInterval = 1f / 30f;

    private JsbsimBridge subscribedBridge;
    private bool stateUpdatePending;
    private float nextDisplayRefreshTime;

    private void Awake()
    {
        EnsureBindings();
        DisableDemoData();
    }

    private void OnEnable()
    {
        EnsureBindings();
        DisableDemoData();
        ApplyZeroState();
        TryBindBridge();
    }

    private void Update()
    {
        if (subscribedBridge == null)
        {
            TryBindBridge();
        }

        if (stateUpdatePending
            && subscribedBridge != null
            && Time.unscaledTime >= nextDisplayRefreshTime)
        {
            stateUpdatePending = false;
            nextDisplayRefreshTime = Time.unscaledTime + displayRefreshInterval;
            ApplyBridgeState(subscribedBridge);
        }
    }

    private void OnDisable()
    {
        UnbindBridge();
    }

    private void TryBindBridge()
    {
        JsbsimBridge target = bridge != null ? bridge : JsbsimBridge.Instance;
        if (target == null || target == subscribedBridge)
        {
            return;
        }

        UnbindBridge();
        subscribedBridge = target;
        subscribedBridge.OnStateUpdated += HandleStateUpdated;
        if (subscribedBridge.HasState)
        {
            ApplyBridgeState(subscribedBridge);
        }
    }

    private void UnbindBridge()
    {
        if (subscribedBridge == null)
        {
            return;
        }

        subscribedBridge.OnStateUpdated -= HandleStateUpdated;
        subscribedBridge = null;
    }

    private void HandleStateUpdated()
    {
        stateUpdatePending = true;
    }

    private void ApplyBridgeState(JsbsimBridge state)
    {
        EnsureBindings();
        if (displayController == null)
        {
            return;
        }

        displayController.SetAirspeedKnots(state.SpeedKts);
        displayController.SetAltitudeFeet(state.AltitudeFt);
        displayController.SetAttitudeDegrees(state.PitchDeg, state.RollDeg);
        displayController.SetMagneticHeadingDegrees(
            PFDJsbsimDataMath.CalculateMagneticHeading(state.HeadingDeg, magneticVariationDeg));
    }

    private void ApplyZeroState()
    {
        if (displayController == null)
        {
            return;
        }

        displayController.SetAirspeedKnots(0f);
        displayController.SetAltitudeFeet(0f);
        displayController.SetAttitudeDegrees(0f, 0f);
        displayController.SetMagneticHeadingDegrees(
            PFDJsbsimDataMath.CalculateMagneticHeading(0f, magneticVariationDeg));
    }

    private void EnsureBindings()
    {
        if (displayController == null)
        {
            displayController = GetComponent<StandbyDisplayController>();
        }
    }

    private void DisableDemoData()
    {
        StandbyDemoDataSource demoData = GetComponent<StandbyDemoDataSource>();
        if (demoData != null)
        {
            demoData.enabled = false;
        }
    }
}

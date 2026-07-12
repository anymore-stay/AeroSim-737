using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class B737StrobeLights : MonoBehaviour
{
    public enum StrobeMode
    {
        Off,
        Armed,
        On
    }

    public const float DefaultPulseSeconds = 0.05f;
    public const float DefaultGapSeconds = 0.05f;
    public const float DefaultRestSeconds = 1f;

    [Header("References")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private bool autoFindBridge = true;

    [Header("Controls")]
    [SerializeField] private StrobeMode mode = StrobeMode.Armed;
    [SerializeField] private bool enableKeyboardControl = true;
    [SerializeField] private KeyCode cycleModeKey = KeyCode.K;

    [Header("Movable Light Points")]
    [SerializeField] private Transform leftWingStrobePoint;
    [SerializeField] private Transform rightWingStrobePoint;
    [SerializeField] private Transform tailStrobePoint;

    [Header("Default Local Positions")]
    [SerializeField] private Vector3 leftWingDefaultLocalPosition = new Vector3(-8.25f, 0.08f, -1.3f);
    [SerializeField] private Vector3 rightWingDefaultLocalPosition = new Vector3(8.25f, 0.08f, -1.3f);
    [SerializeField] private Vector3 tailDefaultLocalPosition = new Vector3(0f, 1.05f, 20.9f);

    [Header("Flight Logic")]
    [SerializeField] private bool forceWhenMovingOrAirborne = true;
    [SerializeField] private bool previewArmedWithoutJsbsim = true;
    [SerializeField] private bool armedModeConstantOn = true;
    [SerializeField, Min(0f)] private float forceAboveAglFt = 10f;
    [SerializeField, Min(0f)] private float forceAboveSpeedKts = 5f;
    [SerializeField, Min(0f)] private float landingHoldSeconds = 12f;

    [Header("Flash Pattern")]
    [SerializeField, Min(0.001f)] private float pulseSeconds = DefaultPulseSeconds;
    [SerializeField, Min(0f)] private float gapSeconds = DefaultGapSeconds;
    [SerializeField, Min(0.001f)] private float restSeconds = DefaultRestSeconds;

    [Header("Initial Light Defaults")]
    [SerializeField] private Color strobeColor = Color.white;
    [SerializeField, Min(0f)] private float peakIntensity = 220f;
    [SerializeField, Min(0.1f)] private float range = 42f;

    [Header("Lens Visuals")]
    [SerializeField, Min(0.01f)] private float lensScale = 0.08f;
    [SerializeField, Min(0.01f)] private float flashGlowScale = 0.85f;
    [SerializeField, Range(0f, 1f)] private float flashGlowAlpha = 0.75f;
    [SerializeField, Min(0f)] private float emissionIntensity = 18f;

    private float elapsedSeconds;
    private float landingHoldTimer;
    private bool wasAirborne;

    public StrobeMode Mode => mode;

    public static bool EvaluatePulse(float elapsedSeconds, float pulseSeconds, float gapSeconds, float restSeconds)
    {
        float safePulse = Mathf.Max(0.001f, pulseSeconds);
        float safeGap = Mathf.Max(0f, gapSeconds);
        float safeRest = Mathf.Max(0.001f, restSeconds);
        float cycle = safePulse + safeGap + safePulse + safeRest;
        float time = Mathf.Repeat(Mathf.Max(0f, elapsedSeconds), cycle);

        if (time < safePulse)
        {
            return true;
        }

        float secondPulseStart = safePulse + safeGap;
        return time >= secondPulseStart && time < secondPulseStart + safePulse;
    }

    public static StrobeMode NextMode(StrobeMode current)
    {
        if (current == StrobeMode.Off)
        {
            return StrobeMode.Armed;
        }

        if (current == StrobeMode.Armed)
        {
            return StrobeMode.On;
        }

        return StrobeMode.Off;
    }

    public static bool ShouldForceFromFlightState(
        bool hasState,
        float aglFt,
        float speedKts,
        float airborneThresholdAglFt,
        float taxiThresholdSpeedKts)
    {
        if (!hasState)
        {
            return false;
        }

        return aglFt >= Mathf.Max(0f, airborneThresholdAglFt) ||
               speedKts >= Mathf.Max(0f, taxiThresholdSpeedKts);
    }

    public void SetMode(StrobeMode nextMode)
    {
        mode = nextMode;
    }

    [ContextMenu("Create Missing Strobe Light Points")]
    public void CreateMissingLightPoints()
    {
        EnsureRig();
        ApplyCurrentLightState(0f);
    }

    private void Awake()
    {
        ResolveBridge();
        EnsureRig();
        ApplyCurrentLightState(0f);
    }

    private void OnEnable()
    {
        ResolveBridge();
        EnsureRig();
        ApplyCurrentLightState(0f);
    }

    private void Update()
    {
        ResolveBridge();
        EnsureRig();

        if (Application.isPlaying)
        {
            if (enableKeyboardControl && Input.GetKeyDown(cycleModeKey))
            {
                mode = NextMode(mode);
            }

            elapsedSeconds += Time.deltaTime;
        }

        ApplyCurrentLightState(Time.deltaTime);
    }

    private void ApplyCurrentLightState(float dt)
    {
        bool active = ShouldStrobeBeActive(dt);
        bool steady = mode == StrobeMode.Armed && armedModeConstantOn && active;
        bool visible = steady || (active && EvaluatePulse(elapsedSeconds, pulseSeconds, gapSeconds, restSeconds));
        ApplyLights(visible);
    }

    private void OnValidate()
    {
        pulseSeconds = Mathf.Max(0.001f, pulseSeconds);
        gapSeconds = Mathf.Max(0f, gapSeconds);
        restSeconds = Mathf.Max(0.001f, restSeconds);
        peakIntensity = Mathf.Max(0f, peakIntensity);
        range = Mathf.Max(0.1f, range);
    }

    private void ResolveBridge()
    {
        if (bridge != null || !autoFindBridge)
        {
            return;
        }

        bridge = GetComponent<JsbsimBridge>();
        if (bridge == null)
        {
            bridge = GetComponentInParent<JsbsimBridge>();
        }
        if (bridge == null)
        {
            bridge = JsbsimBridge.Instance;
        }
        if (bridge == null)
        {
            bridge = FindObjectOfType<JsbsimBridge>();
        }
    }

    private void EnsureRig()
    {
        if (!B737ExteriorLightUtility.CanModifySceneObject(this))
        {
            return;
        }

        Transform root = B737ExteriorLightUtility.FindOrCreateChild(transform, "B737_StrobeLights", Vector3.zero);
        leftWingStrobePoint = B737ExteriorLightUtility.FindOrCreatePoint(root, leftWingStrobePoint, "STROBE_LeftWing_Movable", leftWingDefaultLocalPosition);
        rightWingStrobePoint = B737ExteriorLightUtility.FindOrCreatePoint(root, rightWingStrobePoint, "STROBE_RightWing_Movable", rightWingDefaultLocalPosition);
        tailStrobePoint = B737ExteriorLightUtility.FindOrCreatePoint(root, tailStrobePoint, "STROBE_Tail_Movable", tailDefaultLocalPosition);
    }

    private bool ShouldStrobeBeActive(float dt)
    {
        bool hasState = bridge != null && bridge.HasState;
        float speedKts = hasState ? Mathf.Max(bridge.SpeedKts, bridge.TrueSpeedKts) : 0f;
        bool forced = ShouldForceFromFlightState(
            hasState,
            hasState ? bridge.AglFt : 0f,
            speedKts,
            forceAboveAglFt,
            forceAboveSpeedKts);

        bool airborne = hasState && bridge.AglFt >= Mathf.Max(0f, forceAboveAglFt);
        if (airborne || wasAirborne)
        {
            landingHoldTimer = landingHoldSeconds;
        }
        else
        {
            landingHoldTimer = Mathf.Max(0f, landingHoldTimer - Mathf.Max(0f, dt));
        }

        wasAirborne = airborne;
        bool forcedOrDelayed = forceWhenMovingOrAirborne && (forced || landingHoldTimer > 0f);

        if (mode == StrobeMode.On)
        {
            return true;
        }

        if (mode == StrobeMode.Off)
        {
            return forcedOrDelayed;
        }

        if (armedModeConstantOn)
        {
            return true;
        }

        if (hasState)
        {
            return forcedOrDelayed;
        }

        return previewArmedWithoutJsbsim;
    }

    private void ApplyLights(bool pulse)
    {
        ApplyStrobePoint(leftWingStrobePoint, pulse);
        ApplyStrobePoint(rightWingStrobePoint, pulse);
        ApplyStrobePoint(tailStrobePoint, pulse);
    }

    private void ApplyStrobePoint(Transform point, bool pulse)
    {
        if (point == null)
        {
            return;
        }

        Light light = B737ExteriorLightUtility.EnsureLight(point, LightType.Point, strobeColor, peakIntensity, range, 0f);
        B737ExteriorLightUtility.SetLightEnabled(light, pulse);
        B737ExteriorLightUtility.EnsureLensVisual(point, strobeColor, lensScale, flashGlowScale, flashGlowAlpha, emissionIntensity, pulse);
    }

    private void OnDrawGizmosSelected()
    {
        B737ExteriorLightUtility.DrawPointGizmo(leftWingStrobePoint, leftWingDefaultLocalPosition, transform, strobeColor, 0.24f);
        B737ExteriorLightUtility.DrawPointGizmo(rightWingStrobePoint, rightWingDefaultLocalPosition, transform, strobeColor, 0.24f);
        B737ExteriorLightUtility.DrawPointGizmo(tailStrobePoint, tailDefaultLocalPosition, transform, strobeColor, 0.24f);
    }
}

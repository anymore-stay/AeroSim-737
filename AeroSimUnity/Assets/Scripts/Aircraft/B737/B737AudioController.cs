using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class B737AudioController : MonoBehaviour
{
    private const float FeetToMeters = 0.3048f;
    private const float RunwayFullSpeedFeetPerSecond = 180f;
    private const double GearMotionHoldSeconds = 3.6d;
    private const double FlapMotionHoldSeconds = 1.4d;

    private const float EngineLoopGain = 0.12f;
    private const float EngineStarterGain = 0.55f;
    private const float GearGain = 1f;
    private const float FlapGain = 0f;
    private const float RunwayRollGain = 0.2f;
    private const float TouchdownGain = 0.7f;
    private const float WarningGain = 0.38f;
    private const float RadioCalloutGain = 0.55f;
    private static readonly HideFlags RuntimeAudioObjectHideFlags =
        HideFlags.HideInHierarchy |
        HideFlags.DontSaveInEditor |
        HideFlags.DontSaveInBuild;

    private static readonly string[] WowKeys =
    {
        "gear_unit_wow", "gear_unit_1_wow", "gear_unit_2_wow"
    };

    private static readonly string[] WheelSpeedKeys =
    {
        "gear_unit_wheel_speed_fps",
        "gear_unit_1_wheel_speed_fps",
        "gear_unit_2_wheel_speed_fps"
    };

    private static readonly string[] CompressionVelocityKeys =
    {
        "gear_unit_compression_velocity_fps",
        "gear_unit_1_compression_velocity_fps",
        "gear_unit_2_compression_velocity_fps"
    };

    [Header("Aircraft Components")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private FlightInput flightInput;
    [SerializeField] private B737FlapController flapController;
    [SerializeField] private B737MechanicalController mechanicalController;
    [SerializeField] private B737EngineSpinner engineSpinner;

    [Header("Connection Freshness")]
    [SerializeField, Min(0f)] private float stateFreshnessTimeoutSeconds = 1f;
    [SerializeField] private bool allowOfflineFlightInputAudio = false;

    [Header("Engine Audio Arming")]
    [Tooltip("Keep engine audio muted after JSBSim starts until the player gives an engine/throttle input.")]
    [SerializeField] private bool requireUserEngineAudioArm = true;
    [Tooltip("Pressing this key arms engine audio. The default matches FlightInput throttle-up.")]
    [SerializeField] private KeyCode engineAudioArmKey = KeyCode.LeftShift;
    [SerializeField, Range(0f, 1f)] private float engineAudioArmThrottleThreshold = 0.02f;

    [Header("Interior Mix")]
    [Tooltip("Extra multiplier applied to engine loop volume when CockpitCamera is the active listener.")]
    [SerializeField, Range(0f, 1f)] private float cockpitEngineLoopVolumeMultiplier = 0.18f;
    [Tooltip("Extra multiplier applied to engine loop volume when CabinCamera is the active listener.")]
    [SerializeField, Range(0f, 1f)] private float cabinEngineLoopVolumeMultiplier = 0.3f;
    [Tooltip("Extra multiplier applied to engine loop volume for outside / third-person cameras.")]
    [SerializeField, Range(0f, 1f)] private float outsideEngineLoopVolumeMultiplier = 1f;

    [Header("Runtime Debugging")]
    [SerializeField] private bool logAudioEvents = true;
    [SerializeField] private bool hideRuntimeAudioObjects = true;

    [Header("Engine Clip Overrides")]
    [SerializeField] private AudioClip engineLoopClip;
    [SerializeField] private AudioClip engineStarterClip;

    [Header("System Clip Overrides")]
    [SerializeField] private AudioClip gearClip;
    [SerializeField] private AudioClip flapClip;
    [SerializeField] private AudioClip runwayRollClip;
    [SerializeField] private AudioClip touchdownNormalClip;
    [SerializeField] private AudioClip stallClip;
    [SerializeField] private AudioClip overspeedClip;

    [Header("Radio Altitude Clip Overrides")]
    [SerializeField] private AudioClip callout1000Clip;
    [SerializeField] private AudioClip callout500Clip;
    [SerializeField] private AudioClip callout400Clip;
    [SerializeField] private AudioClip callout300Clip;
    [SerializeField] private AudioClip callout200Clip;
    [SerializeField] private AudioClip callout100Clip;
    [SerializeField] private AudioClip callout50Clip;
    [SerializeField] private AudioClip callout40Clip;
    [SerializeField] private AudioClip callout30Clip;
    [SerializeField] private AudioClip callout20Clip;
    [SerializeField] private AudioClip callout10Clip;

    private AudioSource leftEngineLoopSource;
    private AudioSource leftEngineStarterSource;
    private AudioSource rightEngineLoopSource;
    private AudioSource rightEngineStarterSource;
    private AudioSource gearSource;
    private AudioSource flapSource;
    private AudioSource runwayRollSource;
    private AudioSource touchdownSource;
    private AudioSource stallSource;
    private AudioSource overspeedSource;
    private AudioSource radioAltitudeSource;

    private OverspeedLatch overspeedLatch = new OverspeedLatch();
    private MovementDetector gearMovement = new MovementDetector(0.001d, GearMotionHoldSeconds);
    private MovementDetector flapMovement = new MovementDetector(0.001d, FlapMotionHoldSeconds);
    private MovementDetector gearCommandMovement = new MovementDetector(0.001d, GearMotionHoldSeconds);
    private MovementDetector flapCommandMovement = new MovementDetector(0.001d, FlapMotionHoldSeconds);
    private CalloutTracker calloutTracker = new CalloutTracker();
    private TouchdownDetector touchdownDetector = new TouchdownDetector();
    private EngineStartDetector engineStartDetector = new EngineStartDetector();
    private readonly Queue<int> pendingCallouts = new Queue<int>();
    private readonly bool[] wowValues = new bool[3];
    private readonly bool[] hasWowValues = new bool[3];

    private bool isInitialized;
    private bool connectedAudioActive;
    private JsbsimBridge subscribedBridge;
    private bool hasReceivedBridgeState;
    private float lastBridgeStateRealtime = float.NegativeInfinity;
    private bool offlineEngineOverride;
    private bool engineAudioArmed;
    private float offlineLeftN1;
    private float offlineRightN1;
    private float currentLeftN1;
    private float currentRightN1;
    private float currentLeftN2;
    private float currentRightN2;
    private bool hasLeftN2;
    private bool hasRightN2;
    private float lastKias;
    private float lastMach;
    private float smoothedRunwayVolume;
    private float smoothedRunwayPitch = 0.75f;
    private bool leftEngineLoopWasActive;
    private bool rightEngineLoopWasActive;
    private bool gearWasActive;
    private bool hasLastGearCommand;
    private float lastGearCommand;
    private float gearSoundHoldUntilRealtime;
    private bool flapWasActive;
    private bool runwayRollWasActive;
    private bool stallWasActive;
    private bool overspeedWasActive;

    private bool previewGearSet;
    private bool previewGearActive;
    private bool previewFlapsSet;
    private bool previewFlapsActive;
    private bool previewRunwaySet;
    private float previewRunwaySpeed;
    private bool previewStallSet;
    private bool previewStallActive;
    private bool previewOverspeedSet;
    private bool previewOverspeedActive;

    public bool IsInitialized => isInitialized;

    public bool HasAllRequiredClips =>
        engineLoopClip != null &&
        engineStarterClip != null &&
        gearClip != null &&
        runwayRollClip != null &&
        touchdownNormalClip != null &&
        stallClip != null &&
        overspeedClip != null &&
        callout1000Clip != null &&
        callout500Clip != null &&
        callout400Clip != null &&
        callout300Clip != null &&
        callout200Clip != null &&
        callout100Clip != null &&
        callout50Clip != null &&
        callout40Clip != null &&
        callout30Clip != null &&
        callout20Clip != null &&
        callout10Clip != null;

    private void Awake()
    {
        InitializeAudio();
    }

    private void OnEnable()
    {
        ResolveAircraftComponents();
        SubscribeToBridge();
    }

    private void Update ()
    {
        if (!isInitialized)
        {
            InitializeAudio();
        }

        float deltaSeconds = SafeDeltaTime();
        UpdateEngineAudioArming();
        bool useConnectedAudio = IsBridgeStateFresh();
        if (useConnectedAudio != connectedAudioActive)
        {
            if (useConnectedAudio)
            {
                BeginConnectedAudioSession();
            }
            else
            {
                EndConnectedAudioSession();
            }

            connectedAudioActive = useConnectedAudio;
        }

        if (useConnectedAudio)
        {
            UpdateConnectedAudio(deltaSeconds);
        }
        else
        {
            UpdateOfflineAudio(deltaSeconds);
        }

        PlayNextQueuedCallout();
    }

    private void OnDisable()
    {
        connectedAudioActive = false;
        UnsubscribeFromBridge();
        pendingCallouts.Clear();
        SilenceAllSources();
    }

    private void OnDestroy()
    {
        connectedAudioActive = false;
        UnsubscribeFromBridge();
        pendingCallouts.Clear();
        SilenceAllSources();
    }

    public void InitializeAudio()
    {
        ResolveAircraftComponents();
        if (isActiveAndEnabled)
        {
            SubscribeToBridge();
        }

        LoadClips();

        Transform leftAnchor = FindEngineAnchor(
            engineSpinner != null ? engineSpinner.LeftEngine : null);
        Transform rightAnchor = FindEngineAnchor(
            engineSpinner != null ? engineSpinner.RightEngine : null);

        leftEngineLoopSource = EnsureSource(
            "Left Engine Loop", leftAnchor, engineLoopClip, true, 1f);
        leftEngineStarterSource = EnsureSource(
            "Left Engine Starter", leftAnchor, engineStarterClip, false, 1f);
        rightEngineLoopSource = EnsureSource(
            "Right Engine Loop", rightAnchor, engineLoopClip, true, 1f);
        rightEngineStarterSource = EnsureSource(
            "Right Engine Starter", rightAnchor, engineStarterClip, false, 1f);
        gearSource = EnsureSource("Gear", transform, gearClip, false, 1f);
        flapSource = null;
        runwayRollSource = EnsureSource(
            "Runway Roll", transform, runwayRollClip, true, 1f);
        touchdownSource = EnsureSource(
            "Touchdown", transform, touchdownNormalClip, false, 1f);
        stallSource = EnsureSource("Stall", transform, stallClip, true, 0f);
        overspeedSource = EnsureSource(
            "Overspeed", transform, overspeedClip, true, 0f);
        radioAltitudeSource = EnsureSource(
            "Radio Altitude", transform, callout1000Clip, false, 0f);

        SetSourceLevel(leftEngineLoopSource, 0f, (float)EngineSoundModel.MinimumPitch);
        SetSourceLevel(rightEngineLoopSource, 0f, (float)EngineSoundModel.MinimumPitch);
        SetSourceLevel(gearSource, 0f, 1f);
        SetSourceLevel(runwayRollSource, 0f, smoothedRunwayPitch);
        SetSourceLevel(stallSource, 0f, 1f);
        SetSourceLevel(overspeedSource, 0f, 1f);
        SetSourceLevel(leftEngineStarterSource, EngineStarterGain, 1f);
        SetSourceLevel(rightEngineStarterSource, EngineStarterGain, 1f);
        SetSourceLevel(touchdownSource, TouchdownGain, 1f);
        SetSourceLevel(radioAltitudeSource, RadioCalloutGain, 1f);

        isInitialized = true;
    }

    public void SetOfflineEngineN1(float left, float right)
    {
        offlineEngineOverride = true;
        ArmEngineAudio();
        offlineLeftN1 = SanitizeN1(left);
        offlineRightN1 = SanitizeN1(right);
        ApplyEngineLevels(offlineLeftN1, offlineRightN1);
    }

    public void ClearOfflineEngineOverride()
    {
        offlineEngineOverride = false;
        if (!IsBridgeStateFresh())
        {
            ApplyOfflineEngineLevels();
        }
    }

    public void ArmEngineAudio()
    {
        if (engineAudioArmed)
        {
            return;
        }

        engineAudioArmed = true;
        LogAudioEvent("engine audio armed by user input");
    }

    public void PreviewEngineStart(int engineIndex)
    {
        InitializeIfNeeded();
        if (engineIndex < 0 || engineIndex > 1)
        {
            return;
        }

        PlayOneShot(engineIndex == 0
            ? leftEngineStarterSource
            : rightEngineStarterSource,
            EngineStarterGain);
    }

    public void PreviewGear(bool active)
    {
        InitializeIfNeeded();
        previewGearSet = true;
        previewGearActive = active;
        SetMovementOneShotState(gearSource, ref gearWasActive, active, GearGain, "gear preview");
    }

    public void PreviewFlaps(bool active)
    {
        InitializeIfNeeded();
        previewFlapsSet = true;
        previewFlapsActive = active;
        SetMovementOneShotState(flapSource, ref flapWasActive, active, FlapGain, "flap preview");
    }

    public void PreviewRunwayRoll(float normalizedSpeed)
    {
        InitializeIfNeeded();
        previewRunwaySet = true;
        previewRunwaySpeed = SanitizeNormalized(normalizedSpeed);
        ApplyRunwayLevel(previewRunwaySpeed, false, 0f);
    }

    public void PreviewTouchdown(int severity1To5)
    {
        InitializeIfNeeded();
        int severity = Mathf.Clamp(severity1To5, 1, 5);
        touchdownSource.clip = GetTouchdownClip(severity);
        PlayOneShot(touchdownSource, TouchdownGain);
    }

    public void PreviewStall(bool active)
    {
        InitializeIfNeeded();
        previewStallSet = true;
        previewStallActive = active;
        SetLoopState(stallSource, active, active ? WarningGain : 0f, 1f);
    }

    public void PreviewOverspeed(bool active)
    {
        InitializeIfNeeded();
        previewOverspeedSet = true;
        previewOverspeedActive = active;
        SetLoopState(overspeedSource, active, active ? WarningGain : 0f, 1f);
    }

    public void PreviewCallout(int feet)
    {
        InitializeIfNeeded();
        AudioClip clip = GetCalloutClip(feet);
        if (clip == null)
        {
            return;
        }

        radioAltitudeSource.clip = clip;
        PlayOneShot(radioAltitudeSource, RadioCalloutGain);
    }

    public void StopAllPreviewAudio()
    {
        previewGearSet = false;
        previewFlapsSet = false;
        previewRunwaySet = false;
        previewStallSet = false;
        previewOverspeedSet = false;
        offlineEngineOverride = false;
        pendingCallouts.Clear();

        if (!isInitialized)
        {
            return;
        }

        SilenceAllSources();
    }

    private void SilenceAllSources()
    {
        AudioSource[] sources =
        {
            leftEngineLoopSource,
            leftEngineStarterSource,
            rightEngineLoopSource,
            rightEngineStarterSource,
            gearSource,
            flapSource,
            runwayRollSource,
            touchdownSource,
            stallSource,
            overspeedSource,
            radioAltitudeSource
        };

        for (int index = 0; index < sources.Length; index++)
        {
            SetSourceLevel(sources[index], 0f, sources[index] != null ? sources[index].pitch : 1f);
            StopSource(sources[index]);
        }
    }

    private void ResolveAircraftComponents()
    {
        if (bridge == null)
        {
            bridge = GetComponent<JsbsimBridge>();
        }

        if (flightInput == null)
        {
            flightInput = GetComponent<FlightInput>();
        }

        if (flapController == null)
        {
            flapController = GetComponent<B737FlapController>();
        }

        if (mechanicalController == null)
        {
            mechanicalController = GetComponent<B737MechanicalController>();
        }

        if (engineSpinner == null)
        {
            engineSpinner = GetComponent<B737EngineSpinner>();
        }
    }

    private void SubscribeToBridge()
    {
        if (subscribedBridge == bridge)
        {
            return;
        }

        UnsubscribeFromBridge();
        hasReceivedBridgeState = false;
        lastBridgeStateRealtime = float.NegativeInfinity;
        if (bridge == null)
        {
            return;
        }

        bridge.OnStateUpdated += HandleBridgeStateUpdated;
        subscribedBridge = bridge;
    }

    private void UnsubscribeFromBridge()
    {
        if (subscribedBridge != null)
        {
            subscribedBridge.OnStateUpdated -= HandleBridgeStateUpdated;
        }

        subscribedBridge = null;
    }

    private void HandleBridgeStateUpdated()
    {
        hasReceivedBridgeState = true;
        lastBridgeStateRealtime = Time.realtimeSinceStartup;
    }

    private bool IsBridgeStateFresh()
    {
        if (bridge == null || !bridge.HasState || !hasReceivedBridgeState)
        {
            return false;
        }

        float timeout = IsFinite(stateFreshnessTimeoutSeconds)
            ? Mathf.Max(0f, stateFreshnessTimeoutSeconds)
            : 1f;
        float age = Time.realtimeSinceStartup - lastBridgeStateRealtime;
        return IsFinite(age) && age >= 0f && age <= timeout;
    }

    private void BeginConnectedAudioSession()
    {
        overspeedLatch = new OverspeedLatch();
        gearMovement = new MovementDetector(0.001d, GearMotionHoldSeconds);
        flapMovement = new MovementDetector(0.001d, FlapMotionHoldSeconds);
        gearCommandMovement = new MovementDetector(0.001d, GearMotionHoldSeconds);
        flapCommandMovement = new MovementDetector(0.001d, FlapMotionHoldSeconds);
        calloutTracker = new CalloutTracker();
        touchdownDetector = new TouchdownDetector();
        engineStartDetector = new EngineStartDetector();

        currentLeftN1 = 0f;
        currentRightN1 = 0f;
        currentLeftN2 = 0f;
        currentRightN2 = 0f;
        hasLeftN2 = false;
        hasRightN2 = false;
        lastKias = 0f;
        lastMach = 0f;
        smoothedRunwayVolume = 0f;
        smoothedRunwayPitch = 0.75f;
        engineAudioArmed = !requireUserEngineAudioArm;
        hasLastGearCommand = false;
        lastGearCommand = 0f;
        gearSoundHoldUntilRealtime = 0f;
        ResetLoopLogState();
        pendingCallouts.Clear();
        SilenceRadioAltitude();
        LogAudioEvent("connected audio session started");
    }

    private void EndConnectedAudioSession()
    {
        pendingCallouts.Clear();
        SilenceRadioAltitude();
        LogAudioEvent("connected audio session ended; waiting for fresh JSBSim state");
    }

    private void SilenceRadioAltitude()
    {
        if (radioAltitudeSource == null)
        {
            return;
        }

        SetSourceLevel(radioAltitudeSource, 0f, radioAltitudeSource.pitch);
        StopSource(radioAltitudeSource);
    }

    private void LoadClips()
    {
        LoadClip(ref engineLoopClip, "Audio/B737/Engine/engine_loop");
        LoadClip(ref engineStarterClip, "Audio/B737/Engine/starter");
        LoadClip(ref gearClip, "Audio/B737/Systems/gear");
        LoadClip(ref runwayRollClip, "Audio/B737/Ground/runway_roll");
        LoadClip(ref touchdownNormalClip, "Audio/B737/Ground/touchdown_normal");
        LoadClip(ref stallClip, "Audio/B737/Alerts/stall");
        LoadClip(ref overspeedClip, "Audio/B737/Alerts/overspeed");
        LoadClip(ref callout1000Clip, "Audio/B737/Callouts/1000");
        LoadClip(ref callout500Clip, "Audio/B737/Callouts/500");
        LoadClip(ref callout400Clip, "Audio/B737/Callouts/400");
        LoadClip(ref callout300Clip, "Audio/B737/Callouts/300");
        LoadClip(ref callout200Clip, "Audio/B737/Callouts/200");
        LoadClip(ref callout100Clip, "Audio/B737/Callouts/100");
        LoadClip(ref callout50Clip, "Audio/B737/Callouts/50");
        LoadClip(ref callout40Clip, "Audio/B737/Callouts/40");
        LoadClip(ref callout30Clip, "Audio/B737/Callouts/30");
        LoadClip(ref callout20Clip, "Audio/B737/Callouts/20");
        LoadClip(ref callout10Clip, "Audio/B737/Callouts/10");
    }

    private void UpdateConnectedAudio(float deltaSeconds)
    {
        UpdateConnectedEngines();
        UpdateConnectedMechanicalSounds(deltaSeconds);
        UpdateConnectedWarnings();
        UpdateConnectedGroundSounds(deltaSeconds);
        UpdateConnectedCallouts();
    }

    private void UpdateConnectedEngines()
    {
        float value;
        if (TryBridgeValue("propulsion_engine_n1", out value))
        {
            currentLeftN1 = value;
        }

        if (TryBridgeValue("propulsion_engine_1_n1", out value))
        {
            currentRightN1 = value;
        }

        if (TryBridgeValue("propulsion_engine_n2", out value))
        {
            currentLeftN2 = value;
            hasLeftN2 = true;
        }

        if (TryBridgeValue("propulsion_engine_1_n2", out value))
        {
            currentRightN2 = value;
            hasRightN2 = true;
        }

        if (!engineAudioArmed)
        {
            ApplyEngineLevels(0f, 0f);
            engineStartDetector.Update(0, false, 0f);
            engineStartDetector.Update(1, false, 0f);
            return;
        }

        ApplyEngineLevels(currentLeftN1, currentRightN1);

        bool starterCommanded =
            TryBridgeValue("propulsion_starter_cmd", out value) && value > 0.5f;
        float activeEngineValue;
        bool hasActiveEngine = TryBridgeValue(
            "propulsion_active_engine",
            out activeEngineValue);
        int activeEngine = hasActiveEngine
            ? Mathf.RoundToInt(activeEngineValue)
            : int.MinValue;
        bool hasStarterRoute =
            hasActiveEngine && activeEngine >= -1 && activeEngine <= 1;

        bool leftStarted = engineStartDetector.Update(
            0,
            starterCommanded && hasStarterRoute &&
            (activeEngine == -1 || activeEngine == 0),
            hasLeftN2 ? currentLeftN2 : 0f);
        bool rightStarted = engineStartDetector.Update(
            1,
            starterCommanded && hasStarterRoute &&
            (activeEngine == -1 || activeEngine == 1),
            hasRightN2 ? currentRightN2 : 0f);

        if (starterCommanded && hasStarterRoute)
        {
            if (activeEngine == 0)
            {
                rightStarted = false;
            }
            else if (activeEngine == 1)
            {
                leftStarted = false;
            }
        }

        if (leftStarted)
        {
            LogAudioEvent("left engine starter");
            PlayOneShot(leftEngineStarterSource, EngineStarterGain);
        }

        if (rightStarted)
        {
            LogAudioEvent("right engine starter");
            PlayOneShot(rightEngineStarterSource, EngineStarterGain);
        }
    }

    private void UpdateConnectedMechanicalSounds(float deltaSeconds)
    {
        float gearX;
        float gearY;
        float gearZ;
        bool hasGearPosition = TryReadGearPosition(out gearX, out gearY, out gearZ);
        bool gearIsMoving = hasGearPosition &&
            gearMovement.Update(gearX, gearY, gearZ, deltaSeconds);

        float gearCommand;
        if (TryReadGearCommand(out gearCommand))
        {
            if (DetectGearCommandChange(gearCommand))
            {
                PlayGearCommandSound(gearCommand);
                gearWasActive = true;
                gearIsMoving = true;
            }

            gearIsMoving |= gearCommandMovement.Update(gearCommand, 0d, 0d, deltaSeconds);
        }

        gearIsMoving |= IsGearCommandSoundPlaying();

        if (!hasGearPosition)
        {
            gearIsMoving |= LandingGearToggleGate.HasActiveMotion;
        }

        SetMovementOneShotState(
            gearSource,
            ref gearWasActive,
            gearIsMoving,
            GearGain,
            "gear motion");

        float flapPosition;
        bool hasFlapPosition = TryBridgeValue("fcs_flap_pos_norm", out flapPosition);
        if (!hasFlapPosition && flapController != null && IsFinite(flapController.FlapInput))
        {
            flapPosition = flapController.FlapInput;
            hasFlapPosition = true;
        }

        bool flapIsMoving = hasFlapPosition &&
            flapMovement.Update(flapPosition, 0d, 0d, deltaSeconds);

        float flapCommand;
        if (TryReadFlapCommand(out flapCommand))
        {
            flapIsMoving |= flapCommandMovement.Update(flapCommand, 0d, 0d, deltaSeconds);
        }

        SetMovementOneShotState(
            flapSource,
            ref flapWasActive,
            flapIsMoving,
            FlapGain,
            "flap motion");
    }

    private void UpdateConnectedWarnings()
    {
        float value;
        bool hasStall = TryBridgeValue("systems_stall_warn_norm", out value);
        if (!hasStall)
        {
            hasStall = TryBridgeValue("aero_stall_hyst_norm", out value);
        }

        bool stallActive = hasStall && value > 0.5f;
        SetLoopStateWithLog(
            stallSource,
            ref stallWasActive,
            stallActive,
            stallActive ? WarningGain : 0f,
            1f,
            "stall warning");

        if (TryBridgeValue("velocities_mach", out value))
        {
            lastMach = value;
        }

        if (TryBridgeValue("velocities_vc_kts", out value))
        {
            lastKias = value;
        }
        else if (bridge != null && IsFinite(bridge.SpeedKts))
        {
            lastKias = bridge.SpeedKts;
        }

        bool overspeedActive = overspeedLatch.Update(lastKias, lastMach);
        SetLoopStateWithLog(
            overspeedSource,
            ref overspeedWasActive,
            overspeedActive,
            overspeedActive ? WarningGain : 0f,
            1f,
            "overspeed warning kias=" + lastKias.ToString("0") +
            " mach=" + lastMach.ToString("0.00"));
    }

    private void UpdateConnectedGroundSounds(float deltaSeconds)
    {
        for (int group = 0; group < WowKeys.Length; group++)
        {
            wowValues[group] = false;
            hasWowValues[group] = false;
        }

        int individualWowCount = 0;
        bool anyWeightOnWheels = false;
        float value;
        for (int group = 0; group < WowKeys.Length; group++)
        {
            if (!TryBridgeValue(WowKeys[group], out value))
            {
                continue;
            }

            hasWowValues[group] = true;
            wowValues[group] = value > 0.5f;
            individualWowCount++;
            anyWeightOnWheels |= wowValues[group];
        }

        bool aggregateWow = false;
        bool hasAggregateWow = TryBridgeValue("gear_wow", out value);
        if (hasAggregateWow)
        {
            aggregateWow = value > 0.5f;
            anyWeightOnWheels |= aggregateWow;
        }

        float maximumWheelSpeed = ReadMaximumWheelSpeed();
        float horizontalSpeedMetersPerSecond = maximumWheelSpeed * FeetToMeters;
        float normalizedRunwaySpeed = anyWeightOnWheels
            ? Mathf.Clamp01(maximumWheelSpeed / RunwayFullSpeedFeetPerSecond)
            : 0f;
        ApplyRunwayLevel(normalizedRunwaySpeed, true, deltaSeconds);

        float descentRateMetersPerSecond =
            bridge != null && IsFinite(bridge.VerticalSpeedFps)
                ? bridge.VerticalSpeedFps * FeetToMeters
                : 0f;
        int highestBoom = 0;

        if (individualWowCount > 0)
        {
            for (int group = 0; group < hasWowValues.Length; group++)
            {
                if (!hasWowValues[group])
                {
                    continue;
                }

                float compression = ReadCompressionVelocity(group) * FeetToMeters;
                highestBoom = Mathf.Max(
                    highestBoom,
                    touchdownDetector.Update(
                        group,
                        wowValues[group],
                        compression,
                        descentRateMetersPerSecond,
                        horizontalSpeedMetersPerSecond));
            }
        }

        if (hasAggregateWow && individualWowCount < WowKeys.Length)
        {
            float compression = ReadMaximumCompressionVelocity() * FeetToMeters;
            highestBoom = Mathf.Max(
                highestBoom,
                touchdownDetector.Update(
                    3,
                    aggregateWow,
                    compression,
                    descentRateMetersPerSecond,
                    horizontalSpeedMetersPerSecond));
        }

        if (highestBoom > 0)
        {
            PreviewTouchdown(highestBoom);
        }
    }

    private void UpdateConnectedCallouts()
    {
        if (bridge == null || !IsFinite(bridge.AglFt))
        {
            return;
        }

        int[] crossed = calloutTracker.Update(bridge.AglFt);
        for (int index = 0; index < crossed.Length; index++)
        {
            pendingCallouts.Enqueue(crossed[index]);
        }
    }

    private void UpdateOfflineAudio(float deltaSeconds)
    {
        ApplyOfflineEngineLevels();

        bool gearActive = previewGearSet
            ? previewGearActive
            : LandingGearToggleGate.HasActiveMotion;

        float gearCommand;
        if (!previewGearSet && TryReadGearCommand(out gearCommand))
        {
            if (DetectGearCommandChange(gearCommand))
            {
                PlayGearCommandSound(gearCommand);
                gearWasActive = true;
                gearActive = true;
            }

            gearActive |= gearCommandMovement.Update(gearCommand, 0d, 0d, deltaSeconds);
        }

        gearActive |= IsGearCommandSoundPlaying();

        SetMovementOneShotState(
            gearSource,
            ref gearWasActive,
            gearActive,
            GearGain,
            "gear preview/local motion");

        bool flapsActive;
        if (previewFlapsSet)
        {
            flapsActive = previewFlapsActive;
        }
        else if (flapController != null && IsFinite(flapController.FlapInput))
        {
            flapsActive = flapMovement.Update(
                flapController.FlapInput,
                0d,
                0d,
                deltaSeconds);
        }
        else
        {
            flapsActive = false;
        }

        float flapCommand;
        if (!previewFlapsSet && TryReadFlapCommand(out flapCommand))
        {
            flapsActive |= flapCommandMovement.Update(flapCommand, 0d, 0d, deltaSeconds);
        }

        SetMovementOneShotState(
            flapSource,
            ref flapWasActive,
            flapsActive,
            FlapGain,
            "flap preview/local motion");

        float runwaySpeed = previewRunwaySet ? previewRunwaySpeed : 0f;
        ApplyRunwayLevel(runwaySpeed, false, deltaSeconds);

        bool stallActive = previewStallSet && previewStallActive;
        bool overspeedActive = previewOverspeedSet && previewOverspeedActive;
        SetLoopStateWithLog(
            stallSource,
            ref stallWasActive,
            stallActive,
            stallActive ? WarningGain : 0f,
            1f,
            "stall preview");
        SetLoopStateWithLog(
            overspeedSource,
            ref overspeedWasActive,
            overspeedActive,
            overspeedActive ? WarningGain : 0f,
            1f,
            "overspeed preview");
    }

    private void ApplyOfflineEngineLevels()
    {
        if (!engineAudioArmed)
        {
            ApplyEngineLevels(0f, 0f);
            return;
        }

        if (offlineEngineOverride)
        {
            ApplyEngineLevels(offlineLeftN1, offlineRightN1);
            return;
        }

        float throttleN1 =
            allowOfflineFlightInputAudio && flightInput != null && IsFinite(flightInput.Throttle)
                ? Mathf.Clamp01(flightInput.Throttle) * 100f
                : 0f;
        ApplyEngineLevels(throttleN1, throttleN1);
    }

    private void UpdateEngineAudioArming()
    {
        if (engineAudioArmed || !requireUserEngineAudioArm)
        {
            engineAudioArmed = true;
            return;
        }

        if (Application.isPlaying && Input.GetKeyDown(engineAudioArmKey))
        {
            ArmEngineAudio();
            return;
        }

        if (flightInput != null &&
            IsFinite(flightInput.Throttle) &&
            flightInput.Throttle > engineAudioArmThrottleThreshold)
        {
            ArmEngineAudio();
        }
    }

    private bool TryReadGearPosition(out float x, out float y, out float z)
    {
        float value;
        if (TryBridgeValue("gear_gear_pos_norm", out value))
        {
            x = value;
            y = value;
            z = value;
            return true;
        }

        bool hasX = TryBridgeValue("gear_unit_pos_norm", out x);
        bool hasY = TryBridgeValue("gear_unit_1_pos_norm", out y);
        bool hasZ = TryBridgeValue("gear_unit_2_pos_norm", out z);
        if (hasX || hasY || hasZ)
        {
            float fallback = hasX ? x : hasY ? y : z;
            if (!hasX)
            {
                x = fallback;
            }

            if (!hasY)
            {
                y = fallback;
            }

            if (!hasZ)
            {
                z = fallback;
            }

            return true;
        }

        if (TryBridgeValue("gear_gear_cmd_norm", out value))
        {
            x = value;
            y = value;
            z = value;
            return true;
        }

        if (flightInput != null)
        {
            value = flightInput.GearDown ? 1f : 0f;
            x = value;
            y = value;
            z = value;
            return true;
        }

        if (mechanicalController != null)
        {
            value = mechanicalController.GearExtended ? 1f : 0f;
            x = value;
            y = value;
            z = value;
            return true;
        }

        x = 0f;
        y = 0f;
        z = 0f;
        return false;
    }

    private bool TryReadGearCommand(out float value)
    {
        if (flightInput != null)
        {
            value = flightInput.GearDown ? 1f : 0f;
            return true;
        }

        if (mechanicalController != null)
        {
            value = mechanicalController.GearExtended ? 1f : 0f;
            return true;
        }

        if (TryBridgeValue("gear_gear_cmd_norm", out value))
        {
            return true;
        }

        value = 0f;
        return false;
    }

    private bool DetectGearCommandChange(float command)
    {
        float normalizedCommand = command >= 0.5f ? 1f : 0f;
        if (!hasLastGearCommand)
        {
            hasLastGearCommand = true;
            lastGearCommand = normalizedCommand;
            return false;
        }

        if (Mathf.Approximately(lastGearCommand, normalizedCommand))
        {
            return false;
        }

        lastGearCommand = normalizedCommand;
        return true;
    }

    private void PlayGearCommandSound(float command)
    {
        if (gearSource == null)
        {
            return;
        }

        SetSourceLevel(gearSource, GearGain, 1f);
        LogAudioEvent("gear command changed to " +
            (command >= 0.5f ? "down" : "up"));

        if (!Application.isPlaying || gearSource.clip == null)
        {
            return;
        }

        gearSoundHoldUntilRealtime =
            Time.realtimeSinceStartup + Mathf.Max(0.1f, gearSource.clip.length);
        gearSource.PlayOneShot(gearSource.clip, 1f);
    }

    private bool IsGearCommandSoundPlaying()
    {
        return Application.isPlaying &&
            Time.realtimeSinceStartup < gearSoundHoldUntilRealtime;
    }

    private bool TryReadFlapCommand(out float value)
    {
        if (flightInput != null && IsFinite(flightInput.Flaps))
        {
            value = flightInput.Flaps;
            return true;
        }

        if (flapController != null && IsFinite(flapController.FlapInput))
        {
            value = flapController.FlapInput;
            return true;
        }

        if (TryBridgeValue("fcs_flap_cmd_norm", out value) ||
            TryBridgeValue("fcs_flaps_control", out value))
        {
            return true;
        }

        value = 0f;
        return false;
    }

    private float ReadMaximumWheelSpeed()
    {
        float maximum = 0f;
        bool found = false;
        float value;
        for (int index = 0; index < WheelSpeedKeys.Length; index++)
        {
            if (!TryBridgeValue(WheelSpeedKeys[index], out value))
            {
                continue;
            }

            maximum = Mathf.Max(maximum, Mathf.Abs(value));
            found = true;
        }

        if (!found && TryBridgeValue("velocities_vg_fps", out value))
        {
            maximum = Mathf.Abs(value);
        }

        return IsFinite(maximum) ? maximum : 0f;
    }

    private float ReadCompressionVelocity(int group)
    {
        if (group < 0 || group >= CompressionVelocityKeys.Length)
        {
            return 0f;
        }

        float value;
        return TryBridgeValue(CompressionVelocityKeys[group], out value)
            ? value
            : 0f;
    }

    private float ReadMaximumCompressionVelocity()
    {
        float maximum = 0f;
        for (int group = 0; group < 3; group++)
        {
            maximum = Mathf.Max(maximum, Mathf.Abs(ReadCompressionVelocity(group)));
        }

        return maximum;
    }

    private void ApplyEngineLevels(float leftN1, float rightN1)
    {
        SetEngineLevelWithLog(
            leftEngineLoopSource,
            ref leftEngineLoopWasActive,
            leftN1,
            "left engine loop n1=" + leftN1.ToString("0"));
        SetEngineLevelWithLog(
            rightEngineLoopSource,
            ref rightEngineLoopWasActive,
            rightN1,
            "right engine loop n1=" + rightN1.ToString("0"));
    }

    private static void SetEngineLevel(AudioSource source, float n1)
    {
        if (source == null)
        {
            return;
        }

        float volume = (float)EngineSoundModel.EvaluateGain(n1) * EngineLoopGain;
        float pitch = (float)EngineSoundModel.EvaluatePitch(n1);
        SetSourceLevel(source, volume, pitch);
        SetPlaybackState(source, volume > 0.001f);
    }

    private void SetEngineLevelWithLog(
        AudioSource source,
        ref bool wasActive,
        float n1,
        string eventName)
    {
        float volume = (float)EngineSoundModel.EvaluateGain(n1) * GetCurrentEngineLoopGain();
        float pitch = (float)EngineSoundModel.EvaluatePitch(n1);
        bool active = volume > 0.001f;
        SetSourceLevel(source, volume, pitch);
        SetPlaybackState(source, engineAudioArmed);
        LogLoopTransition(ref wasActive, active, eventName);
    }

    private float GetCurrentEngineLoopGain()
    {
        return EngineLoopGain * GetActiveListenerEngineLoopMultiplier();
    }

    private float GetActiveListenerEngineLoopMultiplier()
    {
        AudioListener listener = FindActiveAudioListener();
        if (listener == null)
        {
            return outsideEngineLoopVolumeMultiplier;
        }

        CockpitCameraController cameraController =
            listener.GetComponent<CockpitCameraController>();
        if (cameraController != null)
        {
            switch (cameraController.cameraMode)
            {
                case CockpitCameraController.CameraMode.Cockpit:
                    return cockpitEngineLoopVolumeMultiplier;
                case CockpitCameraController.CameraMode.Cabin:
                    return cabinEngineLoopVolumeMultiplier;
                default:
                    return outsideEngineLoopVolumeMultiplier;
            }
        }

        string listenerName = listener.gameObject.name;
        if (!string.IsNullOrEmpty(listenerName))
        {
            string normalizedName = listenerName.ToLowerInvariant();
            if (normalizedName.Contains("cockpit"))
            {
                return cockpitEngineLoopVolumeMultiplier;
            }

            if (normalizedName.Contains("cabin"))
            {
                return cabinEngineLoopVolumeMultiplier;
            }
        }

        return outsideEngineLoopVolumeMultiplier;
    }

    private static AudioListener FindActiveAudioListener()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>(false);
        for (int index = 0; index < listeners.Length; index++)
        {
            AudioListener listener = listeners[index];
            if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
            {
                return listener;
            }
        }

        return null;
    }

    private void ApplyRunwayLevel(
        float normalizedSpeed,
        bool smooth,
        float deltaSeconds)
    {
        normalizedSpeed = SanitizeNormalized(normalizedSpeed);
        float targetVolume = normalizedSpeed * RunwayRollGain;
        float targetPitch = Mathf.Lerp(0.75f, 1.35f, normalizedSpeed);

        if (smooth)
        {
            smoothedRunwayVolume = Mathf.MoveTowards(
                smoothedRunwayVolume,
                targetVolume,
                2.5f * Mathf.Max(0f, deltaSeconds));
            smoothedRunwayPitch = Mathf.MoveTowards(
                smoothedRunwayPitch,
                targetPitch,
                1.5f * Mathf.Max(0f, deltaSeconds));
        }
        else
        {
            smoothedRunwayVolume = targetVolume;
            smoothedRunwayPitch = targetPitch;
        }

        SetLoopStateWithLog(
            runwayRollSource,
            ref runwayRollWasActive,
            smoothedRunwayVolume > 0.001f,
            smoothedRunwayVolume,
            smoothedRunwayPitch,
            "runway roll");
    }

    private void PlayNextQueuedCallout()
    {
        if (!Application.isPlaying || radioAltitudeSource == null ||
            radioAltitudeSource.isPlaying)
        {
            return;
        }

        while (pendingCallouts.Count > 0)
        {
            AudioClip clip = GetCalloutClip(pendingCallouts.Dequeue());
            if (clip == null)
            {
                continue;
            }

            radioAltitudeSource.clip = clip;
            LogAudioEvent("radio altitude callout " + clip.name);
            PlayOneShot(radioAltitudeSource, RadioCalloutGain);
            return;
        }
    }

    private AudioClip GetTouchdownClip(int severity)
    {
        return touchdownNormalClip;
    }

    private AudioClip GetCalloutClip(int feet)
    {
        switch (feet)
        {
            case 1000:
                return callout1000Clip;
            case 500:
                return callout500Clip;
            case 400:
                return callout400Clip;
            case 300:
                return callout300Clip;
            case 200:
                return callout200Clip;
            case 100:
                return callout100Clip;
            case 50:
                return callout50Clip;
            case 40:
                return callout40Clip;
            case 30:
                return callout30Clip;
            case 20:
                return callout20Clip;
            case 10:
                return callout10Clip;
            default:
                return null;
        }
    }

    private Transform FindEngineAnchor(B737EngineSpinner.EngineSide side)
    {
        if (side == null || side.blades == null || side.blades.Length == 0)
        {
            return transform;
        }

        Transform common = null;
        for (int index = 0; index < side.blades.Length; index++)
        {
            Transform blade = side.blades[index];
            if (blade == null)
            {
                continue;
            }

            Transform candidate = blade.parent != null ? blade.parent : blade;
            common = common == null ? candidate : FindCommonAncestor(common, candidate);
            if (common == null)
            {
                return transform;
            }
        }

        if (common == null || (common != transform && !common.IsChildOf(transform)))
        {
            return transform;
        }

        return common;
    }

    private static Transform FindCommonAncestor(Transform first, Transform second)
    {
        HashSet<Transform> ancestors = new HashSet<Transform>();
        for (Transform current = first; current != null; current = current.parent)
        {
            ancestors.Add(current);
        }

        for (Transform current = second; current != null; current = current.parent)
        {
            if (ancestors.Contains(current))
            {
                return current;
            }
        }

        return null;
    }

    private AudioSource EnsureSource(
        string sourceName,
        Transform anchor,
        AudioClip clip,
        bool loop,
        float spatialBlend)
    {
        Transform sourceAnchor = anchor != null ? anchor : transform;
        Transform holder = sourceAnchor.Find(sourceName);
        AudioSource source = holder != null ? holder.GetComponent<AudioSource>() : null;
        bool createdHolder = false;
        if (source == null)
        {
            if (holder == null)
            {
                GameObject holderObject = new GameObject(sourceName);
                holderObject.transform.SetParent(sourceAnchor, false);
                holder = holderObject.transform;
                createdHolder = true;
            }

            source = holder.gameObject.AddComponent<AudioSource>();
        }

        if (createdHolder)
        {
            HideRuntimeAudioObject(holder.gameObject);
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 3f;
        source.maxDistance = 180f;
        source.clip = clip;
        return source;
    }

    private bool TryBridgeValue(string key, out float value)
    {
        if (bridge != null && bridge.TryGetValue(key, out value) && IsFinite(value))
        {
            return true;
        }

        value = 0f;
        return false;
    }

    private static void LoadClip(ref AudioClip clip, string resourcePath)
    {
        if (clip == null)
        {
            clip = Resources.Load<AudioClip>(resourcePath);
        }
    }

    private static void SetLoopState(
        AudioSource source,
        bool active,
        float volume,
        float pitch)
    {
        SetSourceLevel(source, active ? volume : 0f, pitch);
        SetPlaybackState(source, active && volume > 0.001f);
    }

    private void SetMovementOneShotState(
        AudioSource source,
        ref bool wasActive,
        bool active,
        float gain,
        string eventName)
    {
        bool sourceStillPlaying =
            Application.isPlaying &&
            source != null &&
            source.isPlaying;

        if (active && !wasActive)
        {
            PlayOneShotWithoutRestart(source, gain);
            LogAudioEvent(eventName + " start");
        }
        else if (!active && wasActive && !sourceStillPlaying)
        {
            LogAudioEvent(eventName + " end");
        }

        wasActive = active || sourceStillPlaying;
        if (!wasActive)
        {
            SetSourceLevel(source, 0f, 1f);
        }
    }

    private void SetLoopStateWithLog(
        AudioSource source,
        ref bool wasActive,
        bool active,
        float volume,
        float pitch,
        string eventName)
    {
        SetLoopState(source, active, volume, pitch);
        LogLoopTransition(ref wasActive, active && volume > 0.001f, eventName);
    }

    private static void SetSourceLevel(AudioSource source, float volume, float pitch)
    {
        if (source == null)
        {
            return;
        }

        source.volume = IsFinite(volume) ? Mathf.Clamp01(volume) : 0f;
        source.pitch = IsFinite(pitch) ? Mathf.Clamp(pitch, -3f, 3f) : 1f;
    }

    private static void SetPlaybackState(AudioSource source, bool shouldPlay)
    {
        if (!Application.isPlaying || source == null || source.clip == null)
        {
            return;
        }

        if (shouldPlay)
        {
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
        else if (source.isPlaying)
        {
            source.Stop();
        }
    }

    private static void PlayOneShot(AudioSource source, float gain)
    {
        if (source == null)
        {
            return;
        }

        SetSourceLevel(source, gain, 1f);
        if (!Application.isPlaying || source.clip == null)
        {
            return;
        }

        if (source.isPlaying)
        {
            source.Stop();
        }

        source.Play();
    }

    private static void PlayOneShotWithoutRestart(AudioSource source, float gain)
    {
        if (source == null)
        {
            return;
        }

        SetSourceLevel(source, gain, 1f);
        if (!Application.isPlaying || source.clip == null || source.isPlaying)
        {
            return;
        }

        source.Play();
    }

    private static void StopSource(AudioSource source)
    {
        if (Application.isPlaying && source != null && source.isPlaying)
        {
            source.Stop();
        }
    }

    private void HideRuntimeAudioObject(GameObject target)
    {
        if (!Application.isPlaying || !hideRuntimeAudioObjects || target == null)
        {
            return;
        }

        target.hideFlags = RuntimeAudioObjectHideFlags;
    }

    private void LogLoopTransition(ref bool wasActive, bool active, string eventName)
    {
        if (wasActive == active)
        {
            return;
        }

        wasActive = active;
        LogAudioEvent(eventName + (active ? " on" : " off"));
    }

    private void ResetLoopLogState()
    {
        leftEngineLoopWasActive = false;
        rightEngineLoopWasActive = false;
        gearWasActive = false;
        flapWasActive = false;
        runwayRollWasActive = false;
        stallWasActive = false;
        overspeedWasActive = false;
    }

    private void LogAudioEvent(string message)
    {
        if (!logAudioEvents || string.IsNullOrEmpty(message))
        {
            return;
        }

        Debug.Log("[B737Audio] " + message);
    }

    private void InitializeIfNeeded()
    {
        if (!isInitialized)
        {
            InitializeAudio();
        }
    }

    private static float SafeDeltaTime()
    {
        return IsFinite(Time.deltaTime) ? Mathf.Max(0f, Time.deltaTime) : 0f;
    }

    private static float SanitizeN1(float value)
    {
        return IsFinite(value) ? Mathf.Clamp(value, 0f, 100f) : 0f;
    }

    private static float SanitizeNormalized(float value)
    {
        return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

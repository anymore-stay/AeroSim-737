using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(JsbsimBridge))]
[RequireComponent(typeof(FlightInput))]
public sealed class B737PatternAutopilot : MonoBehaviour
{
    public enum PatternLeg
    {
        Upwind,
        Crosswind,
        Downwind,
        Base,
        Final,
        Flare,
        Rollout,
        Complete
    }

    public enum TrafficDirection
    {
        Left,
        Right
    }

    private const float FeetToMeters = 0.3048f;
    private static readonly string[] WeightOnWheelsKeys =
    {
        "gear_wow",
        "gear_unit_wow",
        "gear_unit_1_wow",
        "gear_unit_2_wow"
    };

    [Header("References")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private FlightInput flightInput;

    [Header("Engage")]
    [SerializeField] private KeyCode toggleKey = KeyCode.O;
    [SerializeField] private bool activeOnStart;
    [SerializeField] private PatternLeg startLeg = PatternLeg.Upwind;
    [SerializeField] private KeyCode advanceLegKey = KeyCode.F5;

    [Header("Runway Reference")]
    [Tooltip("Capture the current JSBSim position and heading the first time the autopilot is engaged.")]
    [SerializeField] private bool captureRunwayOnFirstEngage = true;
    [SerializeField] private float runwayThresholdNorthM;
    [SerializeField] private float runwayThresholdEastM;
    [SerializeField] private float runwayHeadingDeg;
    [SerializeField] private float runwayElevationM;
    [SerializeField] private bool runwayReferenceCaptured;

    [Header("Pattern Geometry")]
    [SerializeField] private TrafficDirection trafficDirection = TrafficDirection.Right;
    [SerializeField] private float upwindTurnDistanceM = 4500f;
    [SerializeField] private float downwindOffsetM = 4500f;
    [SerializeField] private float baseTurnBeforeThresholdM = 7000f;
    [SerializeField] private float finalTurnLeadM = 2400f;
    [SerializeField] private float minimumLegTimeSec = 5f;
    [SerializeField] private bool useDynamicTurnLead = true;
    [SerializeField] private float turnLeadScale = 1.05f;
    [SerializeField] private float minTurnLeadM = 700f;
    [SerializeField] private float maxTurnLeadM = 3500f;

    [Header("Pattern Targets")]
    [SerializeField] private float patternAltitudeAglFt = 3000f;
    [SerializeField] private float upwindTargetAltitudeAglFt = 2100f;
    [SerializeField] private float crosswindTurnMinAglFt = 2000f;
    [SerializeField] private float downwindTurnMinAglFt = 3000f;
    [SerializeField] private float upwindSpeedKts = 180f;
    [SerializeField] private float crosswindSpeedKts = 180f;
    [SerializeField] private float crosswindMaxPitchDeg = 20f;
    [SerializeField, Range(0f, 1f)] private float crosswindMaxElevator = 0.8f;
    [SerializeField] private float crosswindPitchGain = 0.28f;
    [SerializeField] private float crosswindCapturePitchFloorDeg = 7.5f;
    [SerializeField] private float crosswindPitchReductionRateDegPerSec = 1.5f;
    [SerializeField] private float crosswindHeadingToleranceDeg = 3f;
    [SerializeField] private float downwindSpeedKts = 170f;
    [SerializeField] private float baseSpeedKts = 160f;
    [SerializeField] private float patternMaxBankDeg = 30f;
    [SerializeField] private float altitudeProtectionBandFt = 300f;
    [SerializeField] private float sinkRateProtectionFps = 8f;
    [SerializeField] private float protectedBankDeg = 30f;
    [SerializeField] private float recoveryPitchFloorDeg = 6f;
    [SerializeField] private float recoveryClimbRateFps = 20f;
    [SerializeField, Range(0f, 1f)] private float recoveryElevatorLimit = 0.65f;
    [SerializeField, Range(0f, 1f)] private float recoveryMinThrottle = 0.7f;

    [Header("Takeoff")]
    [SerializeField] private float rotationSpeedKts = 150f;
    [SerializeField] private float takeoffTargetSpeedKts = 200f;
    [SerializeField, Range(0f, 1f)] private float takeoffElevatorLimit = 0.65f;
    [SerializeField] private float takeoffPitchGain = 0.22f;
    [SerializeField, Range(0f, 1f)] private float takeoffFlaps = 0.5f;
    [SerializeField, Range(0f, 0.25f)] private float takeoffFlapReadyTolerance = 0.05f;
    [SerializeField, Range(0f, 1f)] private float crosswindFlaps = 0f;
    [SerializeField] private float climbThrottleBase = 0.9f;
    [SerializeField] private float maxClimbRateFps = 55f;
    [SerializeField] private float maxClimbPitchDeg = 14f;
    [SerializeField] private float takeoffAltitudeToVerticalSpeedGain = 0.24f;

    [Header("Final Approach")]
    [SerializeField] private float glideSlopeDeg = 3f;
    [SerializeField] private float thresholdCrossingHeightM = 15f;
    [SerializeField] private bool useAglForGlideSlope = true;
    [SerializeField] private float finalSpeedKts = 150f;
    [SerializeField] private float finalMaxBankDeg = 22f;
    [SerializeField] private float centerlineLookaheadM = 1200f;
    [SerializeField] private float maxInterceptDeg = 25f;
    [SerializeField] private float finalCenterlineLookaheadM = 800f;
    [SerializeField] private float finalMaxInterceptDeg = 30f;
    [SerializeField] private float finalPathCorrectionDegPerFt = 0.012f;
    [SerializeField] private float finalMinFlightPathDeg = -7f;
    [SerializeField] private float finalMaxFlightPathDeg = 2f;
    [SerializeField, Range(-1f, 1f)] private float finalElevatorTrim = -0.18f;
    [SerializeField] private float finalFlightPathGain = 0.1f;
    [SerializeField] private float finalPitchRateDamping = 0.8f;

    [Header("Auto Land")]
    [SerializeField] private bool autoLand = true;
    [SerializeField] private float flareStartAglFt = 70f;
    [SerializeField] private float flareMaxDistanceM = 900f;
    [SerializeField] private float flarePastThresholdAllowanceM = 1500f;
    [SerializeField] private float flarePitchDeg = 4f;
    [SerializeField] private float flareEntrySinkRateFps = 10f;
    [SerializeField] private float flareTouchdownSinkRateFps = 2f;
    [SerializeField] private float flareVerticalSpeedToPitchGain = 0.25f;
    [SerializeField] private float flareMinPitchDeg = 3f;
    [SerializeField] private float flareMaxPitchDeg = 6f;
    [SerializeField, Range(0f, 1f)] private float flareElevatorLimit = 0.55f;
    [SerializeField] private float flareIdleStartAglFt = 30f;
    [SerializeField] private float touchdownDetectionAglFt = 4.5f;
    [SerializeField] private float touchdownDetectionMaxSinkRateFps = 3f;
    [SerializeField] private float rolloutNoseHoldSeconds = 2.5f;
    [SerializeField] private float rolloutNoseHoldPitchDeg = 2f;
    [SerializeField, Range(0f, 1f)] private float touchdownSpeedbrake = 1f;
    [SerializeField, Range(0f, 1f)] private float rolloutBrake = 0.7f;
    [SerializeField] private float rolloutCompleteSpeedKts = 20f;

    [Header("Lateral Control")]
    [SerializeField] private float bankPerHeadingError = 2.5f;
    [SerializeField] private float aileronGain = 0.04f;
    [SerializeField] private float rollRateDamping = 0.7f;
    [SerializeField, Range(0f, 1f)] private float maxAileron = 0.4f;
    [SerializeField] private bool invertAileron;

    [Header("Vertical Control")]
    [SerializeField] private float altitudeToPitchGain = 0.003f;
    [SerializeField] private float altitudeToVerticalSpeedGain = 0.012f;
    [SerializeField] private float verticalSpeedToPitchGain = 0.08f;
    [SerializeField] private float levelPitchBiasDeg = 4.5f;
    [SerializeField] private float turnPitchCompensationDeg = 1f;
    [SerializeField] private float pitchGain = 0.14f;
    [SerializeField] private float pitchRateDamping = 1.1f;
    [SerializeField, Min(0f)] private float elevatorCommandSlewRate = 0.45f;
    [SerializeField, Min(0f)] private float elevatorNoseDownSlewRate = 0.15f;
    [SerializeField] private float minTargetPitchDeg = -3f;
    [SerializeField] private float maxTargetPitchDeg = 8f;
    [SerializeField, Range(0f, 1f)] private float patternMaxElevator = 0.45f;
    [SerializeField, Range(0f, 1f)] private float maxElevator = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxNoseDownElevator = 0.25f;
    [SerializeField, Range(-1f, 1f)] private float autopilotPitchTrim;
    [SerializeField] private bool invertElevator;

    [Header("Speed Control")]
    [SerializeField] private float patternThrottleBase = 0.62f;
    [SerializeField] private float approachThrottleBase = 0.45f;
    [SerializeField] private float throttleSpeedGain = 0.014f;
    [SerializeField] private float throttleSlewRate = 0.5f;
    [SerializeField] private float turnThrottleCompensation = 0.08f;
    [SerializeField] private float overspeedThrottleCutKts = 20f;
    [SerializeField, Range(0f, 1f)] private float minThrottle = 0.18f;
    [SerializeField, Range(0f, 1f)] private float maxThrottle = 1f;

    [Header("Yaw and Rollout")]
    [SerializeField] private float yawRateDamping = 0.25f;
    [SerializeField, Min(0f)] private float rudderCommandSlewRate = 0.3f;
    [SerializeField] private float rolloutHeadingGain = 0.012f;
    [SerializeField] private float rolloutSteeringHeadingGain = 0.05f;
    [SerializeField] private float rolloutSteeringCrossTrackGain = 0.002f;
    [SerializeField, Range(0f, 1f)] private float rolloutMaxSteer = 0.6f;
    [SerializeField, Range(0f, 1f)] private float maxRudder = 0.08f;
    [SerializeField] private bool invertRudder;

    [Header("Configuration")]
    [SerializeField] private bool manageConfiguration = true;
    [SerializeField, Range(0f, 1f)] private float downwindFlaps = 0.5f;
    [SerializeField, Range(0f, 1f)] private float baseFlaps = 0.75f;
    [SerializeField, Range(0f, 1f)] private float approachFlaps = 1f;
    [SerializeField, Range(0f, 1f)] private float approachSpeedbrake;

    [Header("Output and Debug")]
    [SerializeField, Min(1f)] private float sendRate = 50f;
    [SerializeField] private bool logState = true;
    [SerializeField, Min(0f)] private float logInterval = 2f;
    [SerializeField] private bool recordTelemetry = true;
    [SerializeField, Min(0.2f)] private float telemetryRateHz = 5f;
    [SerializeField] private PatternLeg currentLeg = PatternLeg.Upwind;

    public bool Active { get { return active; } }
    public PatternLeg CurrentLeg { get { return currentLeg; } }
    public string LastTelemetryPath { get; private set; }

    private bool active;
    private bool capturePending;
    private float legEnteredTime;
    private float sendTimer;
    private float logTimer;
    private float telemetryTimer;
    private float telemetryStartTime;
    private float lastTargetFlightPathDeg;
    private bool crosswindAltitudeReached;
    private float crosswindTargetPitchDeg;
    private StreamWriter telemetryWriter;

    private float commandedElevator;
    private float commandedAileron;
    private float commandedRudder;
    private float commandedSteer;
    private float commandedThrottle;
    private float commandedFlaps;
    private float commandedSpeedbrake;
    private float commandedBrake;

    private void Awake()
    {
        if (bridge == null) bridge = GetComponent<JsbsimBridge>();
        if (flightInput == null) flightInput = GetComponent<FlightInput>();
    }

    private void Start()
    {
        if (activeOnStart)
            SetActive(true);
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            SetActive(!active);

        if (!active)
            return;

        if (flightInput != null && !flightInput.ExternalControlActive)
            flightInput.SetExternalControlActive(true);

        if (bridge == null || !bridge.HasState)
            return;

        if (capturePending)
        {
            CaptureCurrentRunwayReference();
            capturePending = false;
        }

        if (!bridge.ControlConnected)
            return;

        if (advanceLegKey != KeyCode.None && Input.GetKeyDown(advanceLegKey))
            EnterLeg(GetNextLeg(currentLeg), "manual advance");

        ApplyPatternControl();
        EvaluateLegTransition();
        PushExternalStateToFlightInput();
        SendAtConfiguredRate();
        RecordTelemetryAtConfiguredRate();
        LogStateIfNeeded();
    }

    private void OnDisable()
    {
        if (active)
            SetActive(false);
    }

    public void SetActive(bool value)
    {
        if (active == value)
            return;

        active = value;
        sendTimer = 0f;
        logTimer = 0f;

        if (!active)
        {
            StopTelemetry("disengaged");
            PushExternalStateToFlightInput();
            flightInput?.SetExternalControlActive(false);
            Debug.Log("[PatternAP] Disengaged with O. Manual controls restored.");
            return;
        }

        InitializeCommandsFromCurrentState();
        flightInput?.SetExternalControlActive(true);
        capturePending = captureRunwayOnFirstEngage && !runwayReferenceCaptured;
        if (capturePending && bridge != null && bridge.HasState)
        {
            CaptureCurrentRunwayReference();
            capturePending = false;
        }

        EnterLeg(startLeg, "engaged with O");
        StartTelemetry();
        PushExternalStateToFlightInput();
    }

    [ContextMenu("Capture Current Position And Heading As Runway")]
    public void CaptureCurrentRunwayReference()
    {
        if (bridge == null || !bridge.HasState)
            return;

        GetCurrentPositionMeters(out runwayThresholdNorthM, out runwayThresholdEastM);
        runwayHeadingDeg = B737PatternAutopilotMath.NormalizeHeading(bridge.HeadingDeg);
        runwayElevationM = bridge.AltitudeFt * FeetToMeters;
        runwayReferenceCaptured = true;
        Debug.Log(string.Format(
            "[PatternAP] Runway captured N/E=({0:F1}, {1:F1}) m, heading={2:F1} deg.",
            runwayThresholdNorthM,
            runwayThresholdEastM,
            runwayHeadingDeg));
    }

    private void InitializeCommandsFromCurrentState()
    {
        commandedElevator = 0f;
        commandedAileron = 0f;
        commandedRudder = 0f;
        commandedSteer = 0f;
        commandedThrottle = bridge != null ? Mathf.Clamp01(bridge.GetValue("throttle", 0f)) : 0f;
        commandedFlaps = bridge != null ? Mathf.Clamp01(bridge.GetValue("fcs_flap_pos_norm", 0f)) : 0f;
        commandedSpeedbrake = 0f;
        commandedBrake = IsWeightOnWheels() ? 1f : 0f;
    }

    private PatternLeg GetNextLeg(PatternLeg leg)
    {
        switch (leg)
        {
            case PatternLeg.Upwind: return PatternLeg.Crosswind;
            case PatternLeg.Crosswind: return PatternLeg.Downwind;
            case PatternLeg.Downwind: return PatternLeg.Base;
            case PatternLeg.Base: return PatternLeg.Final;
            case PatternLeg.Final: return autoLand ? PatternLeg.Flare : PatternLeg.Final;
            case PatternLeg.Flare: return PatternLeg.Rollout;
            case PatternLeg.Rollout: return PatternLeg.Complete;
            default: return PatternLeg.Complete;
        }
    }

    private void EnterLeg(PatternLeg nextLeg, string reason)
    {
        currentLeg = nextLeg;
        legEnteredTime = Time.time;
        logTimer = 0f;
        if (nextLeg == PatternLeg.Crosswind)
        {
            crosswindAltitudeReached = false;
            crosswindTargetPitchDeg = crosswindMaxPitchDeg;
        }
        Debug.Log(string.Format("[PatternAP] Enter {0}: {1}.", currentLeg, reason));
    }

    private void ApplyPatternControl()
    {
        commandedSteer = 0f;
        GetPatternCoordinates(out float alongM, out float crossTrackM);
        float sideSign = trafficDirection == TrafficDirection.Left ? -1f : 1f;

        switch (currentLeg)
        {
            case PatternLeg.Upwind:
                ApplyUpwind(crossTrackM);
                break;
            case PatternLeg.Crosswind:
                ApplyCrosswind(sideSign);
                break;
            case PatternLeg.Downwind:
                ApplyDownwind(crossTrackM, sideSign);
                break;
            case PatternLeg.Base:
                ApplyBase(alongM, sideSign);
                break;
            case PatternLeg.Final:
                ApplyFinal(alongM, crossTrackM);
                break;
            case PatternLeg.Flare:
                ApplyFlare(crossTrackM);
                break;
            case PatternLeg.Rollout:
                ApplyRollout(crossTrackM);
                break;
            case PatternLeg.Complete:
                ApplyComplete();
                break;
        }
    }

    private void ApplyUpwind(float crossTrackM)
    {
        ApplyHeadingControl(GetLineTrackHeading(runwayHeadingDeg, crossTrackM), patternMaxBankDeg);
        bool onGround = IsWeightOnWheels();
        float actualFlaps = flightInput != null
            ? flightInput.FlapVisualPosition
            : bridge.GetValue("fcs_flap_pos_norm", 0f);
        bool waitingForTakeoffFlaps = onGround &&
                                     actualFlaps < takeoffFlaps - takeoffFlapReadyTolerance;
        if (waitingForTakeoffFlaps)
        {
            ApplyPitchControl(0f, takeoffElevatorLimit);
            MoveThrottleTowards(0f);
            ApplyYawDamping();
            ApplyConfiguration(takeoffFlaps, 0f, 1f);
            return;
        }

        bool waitingForRotation = onGround && bridge.SpeedKts < rotationSpeedKts;
        if (waitingForRotation)
            ApplyPitchControl(0f, takeoffElevatorLimit);
        else
            ApplyTakeoffAltitudeControl();

        ApplySpeedControl(
            onGround ? takeoffTargetSpeedKts : upwindSpeedKts,
            waitingForRotation ? 1f : climbThrottleBase,
            0.4f,
            1f);
        ApplyYawDamping();
        ApplyConfiguration(takeoffFlaps, 0f, 0f);
    }

    private void ApplyCrosswind(float sideSign)
    {
        float heading = B737PatternAutopilotMath.NormalizeHeading(runwayHeadingDeg + sideSign * 90f);
        ApplyHeadingControl(heading, patternMaxBankDeg);
        if (!crosswindAltitudeReached && bridge.AglFt >= patternAltitudeAglFt)
            crosswindAltitudeReached = true;

        crosswindTargetPitchDeg = crosswindAltitudeReached
            ? Mathf.MoveTowards(
                crosswindTargetPitchDeg,
                crosswindCapturePitchFloorDeg,
                Mathf.Max(0f, crosswindPitchReductionRateDegPerSec) * Time.deltaTime)
            : crosswindMaxPitchDeg;
        ApplyPitchControl(crosswindTargetPitchDeg, crosswindMaxElevator, crosswindPitchGain);
        ApplySpeedControl(crosswindSpeedKts, patternThrottleBase, minThrottle, maxThrottle);
        ApplyYawDamping();
        ApplyConfiguration(crosswindFlaps, 0f, 0f);
    }

    private void ApplyDownwind(float crossTrackM, float sideSign)
    {
        float targetCrossTrackM = sideSign * downwindOffsetM;
        float lateralError = -(crossTrackM - targetCrossTrackM);
        float heading = B737PatternAutopilotMath.NormalizeHeading(runwayHeadingDeg + 180f);
        ApplyHeadingControl(GetLineTrackHeading(heading, lateralError), patternMaxBankDeg);
        ApplyAltitudeControl(
            patternAltitudeAglFt,
            20f,
            15f,
            crosswindMaxElevator,
            12f);
        ApplySpeedControl(downwindSpeedKts, patternThrottleBase, minThrottle, maxThrottle);
        ApplyYawDamping();
        ApplyConfiguration(downwindFlaps, 0f, 0f);
    }

    private void ApplyBase(float alongM, float sideSign)
    {
        float heading = B737PatternAutopilotMath.NormalizeHeading(runwayHeadingDeg - sideSign * 90f);
        float lateralError = sideSign * (alongM + baseTurnBeforeThresholdM);
        ApplyHeadingControl(GetLineTrackHeading(heading, lateralError), patternMaxBankDeg);
        float distanceToThresholdM = Mathf.Max(0f, -alongM);
        float targetAglFt = Mathf.Min(patternAltitudeAglFt, GetGlideSlopeTargetAglFt(distanceToThresholdM));
        ApplyAltitudeControl(targetAglFt, 8f, 30f, patternMaxElevator);
        ApplySpeedControl(baseSpeedKts, approachThrottleBase, minThrottle, maxThrottle);
        ApplyYawDamping();
        ApplyConfiguration(baseFlaps, 0f, 0f);
    }

    private void ApplyFinal(float alongM, float crossTrackM)
    {
        ApplyHeadingControl(GetFinalTrackHeading(crossTrackM), finalMaxBankDeg);
        ApplyFinalVerticalControl(Mathf.Max(0f, -alongM));
        ApplySpeedControl(finalSpeedKts, approachThrottleBase, minThrottle, maxThrottle);
        ApplyYawDamping();
        ApplyConfiguration(approachFlaps, approachSpeedbrake, 0f);
    }

    private void ApplyFlare(float crossTrackM)
    {
        ApplyHeadingControl(GetFinalTrackHeading(crossTrackM), Mathf.Min(8f, finalMaxBankDeg));
        float targetPitchDeg = B737PatternAutopilotMath.CalculateFlareTargetPitch(
            bridge.AglFt,
            flareStartAglFt,
            touchdownDetectionAglFt,
            flareEntrySinkRateFps,
            flareTouchdownSinkRateFps,
            bridge.VerticalSpeedFps,
            flarePitchDeg,
            flareVerticalSpeedToPitchGain,
            flareMinPitchDeg,
            flareMaxPitchDeg);
        ApplyPitchControl(targetPitchDeg, flareElevatorLimit);
        if (bridge.AglFt <= flareIdleStartAglFt)
            MoveThrottleTowards(0f);
        ApplyYawDamping();
        ApplyConfiguration(approachFlaps, 0f, 0f);
    }

    private void ApplyRollout(float crossTrackM)
    {
        float desiredHeading = GetFinalTrackHeading(crossTrackM);
        ApplyHeadingControl(desiredHeading, 5f);
        if (Time.time - legEnteredTime < rolloutNoseHoldSeconds)
            ApplyPitchControl(rolloutNoseHoldPitchDeg, flareElevatorLimit);
        else
            SetElevatorCommand(0f);
        MoveThrottleTowards(0f);

        float headingErrorDeg = Mathf.DeltaAngle(bridge.HeadingDeg, desiredHeading);
        float rudder = headingErrorDeg * rolloutHeadingGain -
                       bridge.GetValue("r_rps", 0f) * yawRateDamping;
        SetRudderCommand(ApplyOptionalInversion(
            Mathf.Clamp(rudder, -maxRudder, maxRudder),
            invertRudder));
        commandedSteer = Mathf.Clamp(
            headingErrorDeg * rolloutSteeringHeadingGain -
            crossTrackM * rolloutSteeringCrossTrackGain,
            -rolloutMaxSteer,
            rolloutMaxSteer);
        ApplyConfiguration(approachFlaps, touchdownSpeedbrake, rolloutBrake);
    }

    private void ApplyComplete()
    {
        commandedAileron = Mathf.MoveTowards(commandedAileron, 0f, Time.deltaTime);
        commandedElevator = Mathf.MoveTowards(commandedElevator, 0f, Time.deltaTime);
        commandedRudder = Mathf.MoveTowards(commandedRudder, 0f, Time.deltaTime);
        commandedSteer = 0f;
        MoveThrottleTowards(0f);
        ApplyConfiguration(approachFlaps, touchdownSpeedbrake, 1f);
    }

    private void EvaluateLegTransition()
    {
        if (Time.time - legEnteredTime < minimumLegTimeSec)
            return;

        GetPatternCoordinates(out float alongM, out float crossTrackM);
        float sideSign = trafficDirection == TrafficDirection.Left ? -1f : 1f;
        float sideDistanceM = crossTrackM * sideSign;
        float turnLeadM = GetPatternTurnLeadM();

        switch (currentLeg)
        {
            case PatternLeg.Upwind:
                if (alongM >= upwindTurnDistanceM &&
                    bridge.AglFt >= crosswindTurnMinAglFt &&
                    bridge.VerticalSpeedFps >= 0f)
                {
                    EnterLeg(PatternLeg.Crosswind, "upwind distance and altitude reached");
                }
                break;
            case PatternLeg.Crosswind:
                float crosswindHeading = B737PatternAutopilotMath.NormalizeHeading(
                    runwayHeadingDeg + sideSign * 90f);
                if (sideDistanceM >= downwindOffsetM - turnLeadM &&
                    crosswindAltitudeReached &&
                    bridge.VerticalSpeedFps >= -15f &&
                    Mathf.Abs(Mathf.DeltaAngle(bridge.HeadingDeg, crosswindHeading)) <=
                    crosswindHeadingToleranceDeg)
                    EnterLeg(PatternLeg.Downwind, "downwind turn lead reached");
                break;
            case PatternLeg.Downwind:
                if (alongM <= -baseTurnBeforeThresholdM + turnLeadM)
                    EnterLeg(PatternLeg.Base, "base turn lead reached");
                break;
            case PatternLeg.Base:
                if (sideDistanceM <= Mathf.Max(finalTurnLeadM, turnLeadM))
                    EnterLeg(PatternLeg.Final, "final turn lead reached");
                break;
            case PatternLeg.Final:
                float distanceToThresholdM = -alongM;
                if (autoLand && IsLandingContactDetected())
                    EnterLeg(PatternLeg.Rollout, "weight on wheels");
                else if (autoLand && bridge.AglFt <= flareStartAglFt &&
                         distanceToThresholdM <= flareMaxDistanceM &&
                         distanceToThresholdM >= -flarePastThresholdAllowanceM)
                    EnterLeg(PatternLeg.Flare, "flare height reached");
                break;
            case PatternLeg.Flare:
                if (IsLandingContactDetected())
                    EnterLeg(PatternLeg.Rollout, "weight on wheels");
                break;
            case PatternLeg.Rollout:
                if (bridge.SpeedKts <= rolloutCompleteSpeedKts)
                    EnterLeg(PatternLeg.Complete, "rollout complete");
                break;
        }
    }

    private void ApplyHeadingControl(float desiredHeadingDeg, float bankLimitDeg)
    {
        if (IsPatternAltitudeRecoveryActive())
            bankLimitDeg = Mathf.Min(bankLimitDeg, protectedBankDeg);

        float command = B737PatternAutopilotMath.CalculateAileronCommand(
            bridge.HeadingDeg,
            desiredHeadingDeg,
            bridge.RollDeg,
            bridge.GetValue("p_rps", 0f),
            bankPerHeadingError,
            bankLimitDeg,
            aileronGain,
            rollRateDamping,
            maxAileron);
        commandedAileron = ApplyOptionalInversion(command, invertAileron);
    }

    private void ApplyAltitudeControl(
        float targetAltitudeAglFt,
        float maxClimbFps,
        float maxDescentFps,
        float elevatorLimit,
        float maximumPitchDeg = float.NaN)
    {
        float altitudeErrorFt = targetAltitudeAglFt - bridge.AglFt;
        float desiredVerticalSpeedFps = Mathf.Clamp(
            altitudeErrorFt * altitudeToVerticalSpeedGain,
            -maxDescentFps,
            maxClimbFps);
        ApplyVerticalTarget(
            altitudeErrorFt,
            desiredVerticalSpeedFps,
            minTargetPitchDeg,
            float.IsNaN(maximumPitchDeg) ? maxTargetPitchDeg : maximumPitchDeg,
            elevatorLimit);
    }

    private void ApplyTakeoffAltitudeControl()
    {
        float targetPitchDeg = bridge.AglFt < upwindTargetAltitudeAglFt
            ? maxClimbPitchDeg
            : levelPitchBiasDeg;
        ApplyPitchControl(targetPitchDeg, takeoffElevatorLimit, takeoffPitchGain);
    }

    private void ApplyVerticalTarget(
        float altitudeErrorFt,
        float desiredVerticalSpeedFps,
        float minPitchDeg,
        float maxPitchDeg,
        float elevatorLimit,
        float pitchControlGain = -1f)
    {
        bool recovery = IsPatternAltitudeRecoveryActive();
        if (recovery)
        {
            desiredVerticalSpeedFps = Mathf.Max(desiredVerticalSpeedFps, recoveryClimbRateFps);
            minPitchDeg = Mathf.Max(minPitchDeg, recoveryPitchFloorDeg);
            maxPitchDeg = Mathf.Max(maxPitchDeg, recoveryPitchFloorDeg);
            elevatorLimit = Mathf.Max(elevatorLimit, recoveryElevatorLimit);
        }

        float verticalSpeedError = desiredVerticalSpeedFps - bridge.VerticalSpeedFps;
        float targetPitchDeg = levelPitchBiasDeg +
                               altitudeErrorFt * altitudeToPitchGain +
                               verticalSpeedError * verticalSpeedToPitchGain +
                               GetTurnPitchCompensation();
        targetPitchDeg = Mathf.Clamp(targetPitchDeg, minPitchDeg, maxPitchDeg);
        if (recovery)
            targetPitchDeg = Mathf.Max(targetPitchDeg, recoveryPitchFloorDeg);
        ApplyPitchControl(targetPitchDeg, elevatorLimit, pitchControlGain);
    }

    private void ApplyPitchControl(
        float targetPitchDeg,
        float elevatorLimit,
        float pitchControlGain = -1f)
    {
        float activePitchGain = pitchControlGain > 0f ? pitchControlGain : pitchGain;
        float command = B737PatternAutopilotMath.CalculateElevatorCommand(
            targetPitchDeg,
            bridge.PitchDeg,
            bridge.GetValue("q_rps", 0f),
            activePitchGain,
            pitchRateDamping,
            elevatorLimit,
            maxNoseDownElevator);
        SetElevatorCommand(ApplyOptionalInversion(command, invertElevator));
    }

    private void ApplyFinalVerticalControl(float distanceToThresholdM)
    {
        float targetAltitudeFt;
        float currentAltitudeFt;
        if (useAglForGlideSlope)
        {
            targetAltitudeFt = GetGlideSlopeTargetAglFt(distanceToThresholdM);
            currentAltitudeFt = bridge.AglFt;
        }
        else
        {
            float targetAltitudeM = runwayElevationM + thresholdCrossingHeightM +
                                    distanceToThresholdM * Mathf.Tan(glideSlopeDeg * Mathf.Deg2Rad);
            targetAltitudeFt = targetAltitudeM / FeetToMeters;
            currentAltitudeFt = bridge.AltitudeFt;
        }

        float altitudeErrorFt = targetAltitudeFt - currentAltitudeFt;
        lastTargetFlightPathDeg = Mathf.Clamp(
            -glideSlopeDeg + altitudeErrorFt * finalPathCorrectionDegPerFt,
            finalMinFlightPathDeg,
            finalMaxFlightPathDeg);
        float flightPathErrorDeg = lastTargetFlightPathDeg - GetFlightPathAngleDeg();
        float elevator = finalElevatorTrim - flightPathErrorDeg * finalFlightPathGain +
                         bridge.GetValue("q_rps", 0f) * finalPitchRateDamping;
        elevator = Mathf.Clamp(elevator, -maxElevator, maxNoseDownElevator);
        SetElevatorCommand(ApplyOptionalInversion(elevator, invertElevator));
    }

    private void ApplySpeedControl(
        float targetSpeedKts,
        float throttleBase,
        float throttleMinimum,
        float throttleMaximum)
    {
        float targetThrottle = Mathf.Clamp(
            throttleBase + (targetSpeedKts - bridge.SpeedKts) * throttleSpeedGain,
            throttleMinimum,
            throttleMaximum);
        targetThrottle = Mathf.Clamp01(targetThrottle + GetTurnThrottleCompensation());
        if (bridge.SpeedKts >= targetSpeedKts + overspeedThrottleCutKts)
            targetThrottle = 0f;
        else if (IsPatternAltitudeRecoveryActive())
            targetThrottle = Mathf.Max(targetThrottle, recoveryMinThrottle);
        MoveThrottleTowards(targetThrottle);
    }

    private void SetElevatorCommand(float targetCommand)
    {
        float clampedTarget = Mathf.Clamp(targetCommand, -1f, 1f);
        bool movingNoseDown = invertElevator
            ? clampedTarget < commandedElevator
            : clampedTarget > commandedElevator;
        float slewRate = movingNoseDown
            ? elevatorNoseDownSlewRate
            : elevatorCommandSlewRate;
        commandedElevator = Mathf.MoveTowards(
            commandedElevator,
            clampedTarget,
            Mathf.Max(0f, slewRate) * Time.deltaTime);
    }

    private void MoveThrottleTowards(float targetThrottle)
    {
        commandedThrottle = Mathf.MoveTowards(
            commandedThrottle,
            Mathf.Clamp01(targetThrottle),
            Mathf.Max(0f, throttleSlewRate) * Time.deltaTime);
    }

    private void ApplyYawDamping()
    {
        float command = Mathf.Clamp(
            -bridge.GetValue("r_rps", 0f) * yawRateDamping,
            -maxRudder,
            maxRudder);
        SetRudderCommand(ApplyOptionalInversion(command, invertRudder));
    }

    private void SetRudderCommand(float targetCommand)
    {
        commandedRudder = Mathf.MoveTowards(
            commandedRudder,
            Mathf.Clamp(targetCommand, -maxRudder, maxRudder),
            Mathf.Max(0f, rudderCommandSlewRate) * Time.deltaTime);
    }

    private void ApplyConfiguration(float flaps, float speedbrake, float brakes)
    {
        if (!manageConfiguration)
            return;
        commandedFlaps = Mathf.Clamp01(flaps);
        commandedSpeedbrake = Mathf.Clamp01(speedbrake);
        commandedBrake = Mathf.Clamp01(brakes);
    }

    private bool IsPatternAltitudeRecoveryActive()
    {
        bool criticalLeg = currentLeg == PatternLeg.Crosswind || currentLeg == PatternLeg.Downwind;
        return criticalLeg &&
               (bridge.AglFt < patternAltitudeAglFt - altitudeProtectionBandFt ||
                bridge.VerticalSpeedFps < -sinkRateProtectionFps);
    }

    private float GetTurnPitchCompensation()
    {
        float ratio = Mathf.Clamp01(Mathf.Abs(bridge.RollDeg) / Mathf.Max(1f, patternMaxBankDeg));
        return turnPitchCompensationDeg * ratio * ratio;
    }

    private float GetTurnThrottleCompensation()
    {
        float ratio = Mathf.Clamp01(Mathf.Abs(bridge.RollDeg) / Mathf.Max(1f, patternMaxBankDeg));
        return turnThrottleCompensation * ratio;
    }

    private float GetPatternTurnLeadM()
    {
        if (!useDynamicTurnLead)
            return 0f;
        return B737PatternAutopilotMath.GetDynamicTurnLeadM(
            bridge.SpeedKts,
            patternMaxBankDeg,
            turnLeadScale,
            minTurnLeadM,
            maxTurnLeadM);
    }

    private float GetLineTrackHeading(float courseHeadingDeg, float lateralErrorM)
    {
        return B737PatternAutopilotMath.GetLineTrackHeading(
            courseHeadingDeg,
            lateralErrorM,
            centerlineLookaheadM,
            maxInterceptDeg);
    }

    private float GetFinalTrackHeading(float crossTrackM)
    {
        return B737PatternAutopilotMath.GetLineTrackHeading(
            runwayHeadingDeg,
            crossTrackM,
            finalCenterlineLookaheadM,
            finalMaxInterceptDeg);
    }

    private float GetGlideSlopeTargetAglFt(float distanceToThresholdM)
    {
        float targetHeightM = thresholdCrossingHeightM +
                              Mathf.Max(0f, distanceToThresholdM) *
                              Mathf.Tan(glideSlopeDeg * Mathf.Deg2Rad);
        return targetHeightM / FeetToMeters;
    }

    private float GetFlightPathAngleDeg()
    {
        if (bridge.TryGetValue("flight_path_gamma_deg", out float gammaDeg))
            return gammaDeg;

        float trueSpeedFps = Mathf.Max(1f, bridge.TrueSpeedKts * 1.68781f);
        return Mathf.Asin(Mathf.Clamp(bridge.VerticalSpeedFps / trueSpeedFps, -1f, 1f)) *
               Mathf.Rad2Deg;
    }

    private bool IsWeightOnWheels()
    {
        if (bridge == null || !bridge.HasState)
            return true;

        bool hasWow = false;
        bool weightOnWheels = false;
        for (int i = 0; i < WeightOnWheelsKeys.Length; i++)
        {
            if (!bridge.TryGetValue(WeightOnWheelsKeys[i], out float value))
                continue;
            hasWow = true;
            weightOnWheels |= value > 0.5f;
        }

        return hasWow ? weightOnWheels : bridge.AglFt <= 5f;
    }

    private bool IsLandingContactDetected()
    {
        if (IsWeightOnWheels())
            return true;

        return bridge.AglFt <= touchdownDetectionAglFt &&
               bridge.VerticalSpeedFps >= -Mathf.Abs(touchdownDetectionMaxSinkRateFps);
    }

    private void GetPatternCoordinates(out float alongM, out float crossTrackM)
    {
        GetCurrentPositionMeters(out float northM, out float eastM);
        Vector2 coordinates = B737PatternAutopilotMath.GetPatternCoordinates(
            northM,
            eastM,
            runwayThresholdNorthM,
            runwayThresholdEastM,
            runwayHeadingDeg);
        alongM = coordinates.x;
        crossTrackM = coordinates.y;
    }

    private void GetCurrentPositionMeters(out float northM, out float eastM)
    {
        if (bridge.TryGetValue("position_from_start_neu_n_ft", out float northFt))
            northM = northFt * FeetToMeters;
        else
            northM = bridge.GetValue("position_distance_from_start_lat_mt", 0f);

        if (bridge.TryGetValue("position_from_start_neu_e_ft", out float eastFt))
            eastM = eastFt * FeetToMeters;
        else
            eastM = bridge.GetValue("position_distance_from_start_lon_mt", 0f);
    }

    private void PushExternalStateToFlightInput()
    {
        flightInput?.SetExternalControlState(
            commandedElevator,
            commandedAileron,
            commandedRudder,
            commandedThrottle,
            commandedFlaps,
            commandedSpeedbrake,
            commandedBrake >= 0.5f);
    }

    private void SendAtConfiguredRate()
    {
        sendTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(1f, sendRate);
        if (sendTimer < interval)
            return;
        sendTimer %= interval;
        SendControls();
    }

    private void SendControls()
    {
        if (bridge == null || !bridge.ControlConnected)
            return;

        bridge.SetProperty("fcs/elevator-cmd-norm", commandedElevator);
        bridge.SetProperty("fcs/pitch-trim-cmd-norm", autopilotPitchTrim);
        bridge.SetProperty("fcs/aileron-cmd-norm", commandedAileron);
        bridge.SetProperty("fcs/rudder-cmd-norm", commandedRudder);
        bridge.SetProperty("fcs/steer-cmd-norm", commandedSteer);
        bridge.SetProperty("propulsion/engine[0]/reverser-angle-rad", 0f);
        bridge.SetProperty("propulsion/engine[1]/reverser-angle-rad", 0f);
        bridge.SetProperty("fcs/throttle-cmd-norm[0]", commandedThrottle);
        bridge.SetProperty("fcs/throttle-cmd-norm[1]", commandedThrottle);
        bridge.SetProperty("fcs/flap-cmd-norm", commandedFlaps);
        bridge.SetProperty("fcs/speedbrake-cmd-norm", commandedSpeedbrake);
        bridge.SetProperty("fcs/left-brake-cmd-norm", commandedBrake);
        bridge.SetProperty("fcs/right-brake-cmd-norm", commandedBrake);
        bridge.SetProperty("fcs/center-brake-cmd-norm", commandedBrake);
    }

    private void StartTelemetry()
    {
        StopTelemetry("restarted");
        if (!recordTelemetry)
            return;

        try
        {
            string directory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Captures",
                "Autopilot"));
            Directory.CreateDirectory(directory);
            LastTelemetryPath = Path.Combine(
                directory,
                "pattern_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
            telemetryWriter = new StreamWriter(LastTelemetryPath, false);
            telemetryWriter.AutoFlush = true;
            telemetryWriter.WriteLine(
                "time_s,leg,north_m,east_m,along_m,cross_m,agl_ft,altitude_ft,speed_kts,vertical_speed_fps," +
                "heading_deg,pitch_deg,roll_deg,aileron_cmd,elevator_cmd,rudder_cmd,throttle_cmd,flaps_cmd,gear_cmd,brake_cmd");
            telemetryTimer = 1f / Mathf.Max(0.2f, telemetryRateHz);
            telemetryStartTime = Time.time;
            Debug.Log("[PatternAP] Telemetry: " + LastTelemetryPath);
        }
        catch (Exception exception)
        {
            telemetryWriter = null;
            Debug.LogWarning("[PatternAP] Unable to start telemetry: " + exception.Message);
        }
    }

    private void RecordTelemetryAtConfiguredRate()
    {
        if (telemetryWriter == null)
            return;

        telemetryTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.2f, telemetryRateHz);
        if (telemetryTimer < interval)
            return;
        telemetryTimer %= interval;

        GetCurrentPositionMeters(out float northM, out float eastM);
        GetPatternCoordinates(out float alongM, out float crossTrackM);
        telemetryWriter.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0:F3},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9:F3}," +
            "{10:F3},{11:F3},{12:F3},{13:F4},{14:F4},{15:F4},{16:F4},{17:F4},{18:F4},{19:F4}",
            Time.time - telemetryStartTime,
            currentLeg,
            northM,
            eastM,
            alongM,
            crossTrackM,
            bridge.AglFt,
            bridge.AltitudeFt,
            bridge.SpeedKts,
            bridge.VerticalSpeedFps,
            bridge.HeadingDeg,
            bridge.PitchDeg,
            bridge.RollDeg,
            commandedAileron,
            commandedElevator,
            commandedRudder,
            commandedThrottle,
            commandedFlaps,
            flightInput != null && flightInput.GearDown ? 1f : 0f,
            commandedBrake));

        if (currentLeg == PatternLeg.Complete)
            StopTelemetry("complete");
    }

    private void StopTelemetry(string reason)
    {
        if (telemetryWriter == null)
            return;

        try
        {
            telemetryWriter.WriteLine("# " + reason);
            telemetryWriter.Dispose();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[PatternAP] Unable to close telemetry: " + exception.Message);
        }
        finally
        {
            telemetryWriter = null;
        }
    }

    private void LogStateIfNeeded()
    {
        if (!logState || logInterval <= 0f)
            return;
        logTimer += Time.deltaTime;
        if (logTimer < logInterval)
            return;
        logTimer = 0f;
        GetPatternCoordinates(out float alongM, out float crossTrackM);
        Debug.Log(string.Format(
            "[PatternAP] leg={0} along={1:F0}m cross={2:F0}m alt={3:F0}ft speed={4:F0}kt " +
            "roll/pitch/hdg={5:F1}/{6:F1}/{7:F1} cmd={8:F2}/{9:F2}/{10:F2}/{11:F2}",
            currentLeg,
            alongM,
            crossTrackM,
            bridge.AglFt,
            bridge.SpeedKts,
            bridge.RollDeg,
            bridge.PitchDeg,
            bridge.HeadingDeg,
            commandedAileron,
            commandedElevator,
            commandedRudder,
            commandedThrottle));
    }

    private static float ApplyOptionalInversion(float value, bool invert)
    {
        return invert ? -value : value;
    }
}

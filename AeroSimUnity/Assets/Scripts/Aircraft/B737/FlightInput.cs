using UnityEngine;

/// <summary>
/// 键盘飞行操控,把输入转成 JSBSim 控制量发送出去。
///
/// 默认按键:
///   W / S        : 升降舵(俯仰)。W 推杆低头,S 拉杆抬头。
///   A / D        : 副翼(滚转)。A 左滚,D 右滚。
///   Q / E        : 方向舵(偏航)。
///   Z / X        : 抬头配平 / 低头配平。
///   Shift / Ctrl : 油门加 / 收至怠速。
///   Shift + Ctrl : 在任何状态下增加反推。
///   F            : 襟翼切换(0 / 0.5 / 1)。
///   方向键不再控制飞机:上下左右留给相机视角移动。
///
/// 升降舵和油门采用保持型逻辑，松开按键后保持当前状态。
/// 副翼和方向舵松开按键后平滑回到中立位。
/// </summary>
[RequireComponent(typeof(JsbsimBridge))]
public class FlightInput : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private B737FlapController flapController;
    [SerializeField] private B737MechanicalController mechanicalController;
    [Tooltip("检测到图马思特侧杆后优先使用侧杆；设备断开时自动恢复键盘控制。")]
    [SerializeField] private ThrustmasterA320SidestickInput sidestickInput;

    private LandingGearDoorSequenceController[] landingGearDoorSequences;
    private LandingGearSynchronizedDoorGearController[] synchronizedLandingGearControllers;
    private LandingGearHingeRetractionController[] standaloneLandingGearHinges;

    [Header("Pause")]
    [SerializeField] private bool enableEscapePause = true;

    [Header("舵面灵敏度(每秒变化量)")]
    [SerializeField] private float elevatorRate = 0.8f;
    [SerializeField] private float aileronRate = 1.0f;
    [SerializeField] private float rudderRate = 0.8f;
    [Tooltip("松开按键后副翼和方向舵的回中速度。升降舵不会自动回中。")]
    [SerializeField] private float centerRate = 0.75f;

    [Header("俯仰配平")]
    [Tooltip("按住时逐渐增加抬头配平，不会改变 W/S 保持的升降舵状态。")]
    [InspectorName("抬头配平按键")]
    [SerializeField] private KeyCode pitchTrimNoseUpKey = KeyCode.Z;
    [Tooltip("按住时逐渐增加低头配平，不会改变 W/S 保持的升降舵状态。")]
    [InspectorName("低头配平按键")]
    [SerializeField] private KeyCode pitchTrimNoseDownKey = KeyCode.X;
    [Tooltip("每秒改变的归一化配平量。")]
    [InspectorName("配平变化速度")]
    [SerializeField, Min(0f)] private float pitchTrimRate = 0.25f;

    [Header("油门")]
    [SerializeField] private float throttleRate = 0.5f;
    [Tooltip("JSBSim 反推角度。π rad 使发动机推力完全反向，提供最大反推强度。")]
    [SerializeField, Range(1.571f, 3.142f)] private float reverseThrustAngleRad = Mathf.PI;

    [Header("High Lift and Drag Controls")]
    [SerializeField, Min(1)] private int flapStepCount = 4;
    [SerializeField, Min(1)] private int spoilerStepCount = 4;
    [SerializeField, Min(0f)] private float minimumGearRetractionAglFt = 10f;
    [Header("Control Tuning")]
    [Tooltip("中文名：升降舵权限。数值越大，W/S 对俯仰控制越强。")]
    [SerializeField, Range(0f, 1f)] private float elevatorAuthority = 0.75f;
    [Tooltip("中文名：副翼权限。数值越大，滚转和转弯建立越快，但太大会不好控制。")]
    [SerializeField, Range(0f, 1f)] private float aileronAuthority = 1.0f;
    [Tooltip("中文名：方向舵权限。数值越大，Q/E 的偏航辅助越强，太大会侧滑或把航向拉过头。")]
    [SerializeField, Range(0f, 1f)] private float rudderAuthority = 0.3f;
    [Tooltip("中文名：启用协调转弯辅助。开启后 Q/E 会自动混入副翼，让飞机带坡度转弯并自动回正。")]
    [SerializeField] private bool coordinatedTurnAssist = true;
    [Tooltip("中文名：偏航转副翼备用混合。没有 JSBSim 状态数据时使用，正常飞行时主要由坡度保持控制接管。")]
    [SerializeField, Range(0f, 1f)] private float yawToAileron = 0.15f;
    [Tooltip("中文名：转弯方向舵输入上限。限制 Q/E 按住时方向舵最多打多少，避免偏航过猛。")]
    [SerializeField, Range(0f, 1f)] private float maxTurnRudderInput = 0.7f;
    [Tooltip("中文名：目标坡度角。Q/E 按住时希望飞机达到的最大倾斜角，越大转弯越快但越难控。")]
    [SerializeField] private float coordinatedTurnBankDeg = 55f;
    [Tooltip("中文名：坡度保持增益。数值越大越积极追目标坡度，太大容易左右晃或过冲。")]
    [SerializeField] private float bankHoldGain = 0.025f;
    [Tooltip("中文名：目标坡度变化速度。数值越大，按下 Q/E 后越快进入倾斜。")]
    [SerializeField] private float bankTargetSlewRate = 80f;
    [Tooltip("中文名：滚转阻尼。数值越大越会压住滚转速度，减少过冲和机翼立起来。")]
    [SerializeField] private float rollRateDamping = 0.03f;
    [Tooltip("中文名：协调转弯死区。Q/E 输入小于这个值时不会触发自动转弯辅助。")]
    [SerializeField, Range(0f, 0.5f)] private float coordinatedTurnDeadzone = 0.08f;
    [Tooltip("中文名：转弯副翼补偿上限。限制自动转弯时副翼最多补多少，越大滚转越快。")]
    [SerializeField, Range(0f, 1f)] private float maxCoordinatedAileron = 0.75f;
    [Tooltip("中文名：回正死区。松开 Q/E 后，坡度小于这个角度就认为基本回正。")]
    [SerializeField] private float levelBankDeadzoneDeg = 1.5f;
    [Tooltip("中文名：自动回正副翼上限。松开 Q/E 后用于把飞机扶平的副翼最大补偿。")]
    [SerializeField, Range(0f, 1f)] private float maxLevelingAileron = 0.28f;
    [Header("侧滑保护")]
    [Tooltip("限制飞行中的侧滑，防止持续偏航发展成旋转。")]
    [SerializeField] private bool sideSlipProtection = true;
    [Tooltip("在硬限制前多少度开始渐进介入。")]
    [SerializeField, Min(0f)] private float sideSlipSoftZoneDeg = 2f;
    [Tooltip("允许的最大气动侧滑角绝对值。")]
    [SerializeField, Min(0.1f)] private float maxSideSlipDeg = 8f;
    [Tooltip("超过软限制起点后，每度侧滑施加的方向舵修正量。")]
    [SerializeField, Min(0f)] private float sideSlipCorrectionGain = 0.06f;
    [Tooltip("侧滑保护允许使用的最大自动方向舵修正量。")]
    [SerializeField, Range(0f, 1f)] private float maxSideSlipRudderCorrection = 0.45f;
    [Tooltip("低于此校准空速时关闭保护，避免影响地面滑行转向。")]
    [SerializeField, Min(0f)] private float sideSlipProtectionMinSpeedKts = 60f;

    [Header("地面前轮转向")]
    [Tooltip("中文名：启用地面前轮转向。开启后 Q/E 会同时控制 JSBSim 的前轮转向通道。")]
    [SerializeField] private bool groundSteeringEnabled = true;
    [Tooltip("中文名：地面转向力度。1 表示使用完整前轮转向范围，减小后滑行转弯会更缓。")]
    [SerializeField, Range(0f, 1f)] private float groundSteeringAuthority = 1f;
    [Tooltip("中文名：地面转向最大离地高度。高于该无线电高度后前轮转向自动归零。")]
    [SerializeField, Min(0f)] private float groundSteeringMaxAglFt = 15f;
    [Tooltip("中文名：反转 JSBSim 前轮转向。当前 737 场景保持关闭，使 Q 左转、E 右转。")]
    [SerializeField] private bool invertGroundSteering = false;

    [Header("坡度角保护")]
    [Tooltip("限制飞行中的最大坡度角，防止持续按 A/D 导致飞机翻转。")]
    [SerializeField] private bool bankAngleProtection = true;
    [Tooltip("允许的最大坡度角绝对值。达到此角度后只允许副翼回正。")]
    [SerializeField, Range(1f, 89f)] private float maxBankAngleDeg = 60f;
    [Tooltip("在最大坡度角前多少度开始渐进削弱继续滚转的输入。")]
    [SerializeField, Min(0f)] private float bankProtectionSoftZoneDeg = 15f;
    [Tooltip("超过软限制后，每度坡度施加的自动回正副翼量。")]
    [SerializeField, Min(0f)] private float bankProtectionCorrectionGain = 0.08f;
    [Tooltip("坡度保护允许使用的最大自动回正副翼量。")]
    [SerializeField, Range(0f, 1f)] private float maxBankProtectionCorrection = 1f;
    [Tooltip("根据当前滚转角速度提前预测坡度，防止高速滚转因惯性越过限制。")]
    [SerializeField, Range(0f, 1f)] private float bankProtectionLookAheadSeconds = 0.35f;
    [Tooltip("低于此空速时关闭坡度保护，避免影响地面操纵。")]
    [SerializeField, Min(0f)] private float bankProtectionMinSpeedKts = 60f;

    [Header("发送频率")]
    [Tooltip("每秒向 JSBSim 发送控制命令的次数。50 足够顺滑。")]
    [SerializeField] private float sendRate = 50f;

    // 当前归一化控制量 [-1,1]，油门 [-1,1]，负值表示反推。
    private float elevator;
    private float aileron;
    private float rudder;
    private float keyboardElevator;
    private float keyboardAileron;
    private float keyboardRudder;
    private float pitchTrim;
    private float turnInput;
    private float smoothedTurnBankTargetDeg;
    private float throttle = 0f;   // 地面起飞:初始油门怠速 0,推 Shift 才加速
    private int flapStep;
    private int spoilerStep;
    private bool gearDown = true;
    private bool brakes = true;    // 地面起飞:初始刹车锁定,飞机停得住;松刹车(B)再滑跑
    private bool directJoystickControlActive;
    private bool keyboardThrottleOverride;
    private bool shiftHoldingIdleAfterReverse;
    private bool shiftWasUsedForReverse;
    private float joystickThrottleAtKeyboardTakeover;

    private float sendTimer;
    private bool escapePaused;
    private float timeScaleBeforePause = 1f;

    private void Awake()
    {
        if (bridge == null) bridge = GetComponent<JsbsimBridge>();
        if (flapController == null) flapController = GetComponent<B737FlapController>();
        if (mechanicalController == null) mechanicalController = GetComponent<B737MechanicalController>();
        if (sidestickInput == null) sidestickInput = GetComponent<ThrustmasterA320SidestickInput>();
        landingGearDoorSequences = GetComponentsInChildren<LandingGearDoorSequenceController>(true);
        synchronizedLandingGearControllers = GetComponentsInChildren<LandingGearSynchronizedDoorGearController>(true);
        CacheStandaloneLandingGearHinges();
    }

    private void Update()
    {
        if (enableEscapePause && Input.GetKeyDown(KeyCode.Escape))
        {
            SetEscapePaused(!escapePaused);
            return;
        }

        if (escapePaused) return;

        float dt = Time.deltaTime;
        directJoystickControlActive = sidestickInput != null && sidestickInput.ControlActive;

        // ---- 升降舵:S 拉杆抬头, W 推杆低头；接入侧杆后作为保持型修正量叠加 ----
        float pitchInput = 0f;
        if (Input.GetKey(KeyCode.W)) pitchInput += 1f;
        if (Input.GetKey(KeyCode.S)) pitchInput -= 1f;
        keyboardElevator = StepHeldAxis(keyboardElevator, pitchInput, elevatorRate, dt);
        float joystickElevator = directJoystickControlActive ? sidestickInput.Pitch : 0f;
        elevator = Mathf.Clamp(joystickElevator + keyboardElevator, -1f, 1f);

        // 配平使用独立状态，不修改升降舵及其松键保持值。
        pitchTrim = StepPitchTrim(
            pitchTrim,
            Input.GetKey(pitchTrimNoseUpKey),
            Input.GetKey(pitchTrimNoseDownKey),
            pitchTrimRate,
            dt);

        // ---- 副翼:A 左滚, D 右滚；键盘修正量与侧杆横滚叠加 ----
        float rollInput = 0f;
        if (Input.GetKey(KeyCode.D)) rollInput += 1f;
        if (Input.GetKey(KeyCode.A)) rollInput -= 1f;
        keyboardAileron = StepAxis(keyboardAileron, rollInput, aileronRate, dt);
        float joystickAileron = directJoystickControlActive ? sidestickInput.Roll : 0f;
        aileron = Mathf.Clamp(joystickAileron + keyboardAileron, -1f, 1f);

        // ---- 方向舵:Q 左偏航, E 右偏航；键盘修正量与侧杆扭转叠加 ----
        float yawInput = 0f;
        turnInput = 0f;
        if (Input.GetKey(KeyCode.E))
        {
            yawInput += 1f;
            turnInput += 1f;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            yawInput -= 1f;
            turnInput -= 1f;
        }
        if (coordinatedTurnAssist)
        {
            float rudderTarget = yawInput * maxTurnRudderInput;
            keyboardRudder = Mathf.MoveTowards(keyboardRudder, rudderTarget, rudderRate * dt);
        }
        else
        {
            keyboardRudder = StepAxis(keyboardRudder, yawInput, rudderRate, dt);
        }
        float joystickRudder = directJoystickControlActive ? sidestickInput.Yaw : 0f;
        rudder = Mathf.Clamp(joystickRudder + keyboardRudder, -1f, 1f);

        // ---- 油门:Shift 加，Ctrl 收至怠速，同时按下可在任何状态增加反推 ----
        bool increaseThrottle = Input.GetKey(KeyCode.LeftShift);
        bool decreaseThrottle = Input.GetKey(KeyCode.LeftControl);
        if (!increaseThrottle)
        {
            shiftHoldingIdleAfterReverse = false;
            shiftWasUsedForReverse = false;
        }
        if (increaseThrottle && decreaseThrottle)
        {
            shiftHoldingIdleAfterReverse = false;
            shiftWasUsedForReverse = true;
        }
        if (Input.GetKeyDown(KeyCode.LeftShift) && !decreaseThrottle &&
            !shiftWasUsedForReverse && throttle < 0f)
            shiftHoldingIdleAfterReverse = true;

        bool hasKeyboardThrottleInput = increaseThrottle || decreaseThrottle;
        bool hasJoystickThrottle = sidestickInput != null && sidestickInput.ThrottleControlEnabled;
        bool keepFullReverseUntilShiftRepress =
            shiftWasUsedForReverse && increaseThrottle && !decreaseThrottle;
        if (keepFullReverseUntilShiftRepress)
        {
            if (hasJoystickThrottle)
            {
                if (!keyboardThrottleOverride)
                    joystickThrottleAtKeyboardTakeover = sidestickInput.Throttle;
                keyboardThrottleOverride = true;
            }
            else
            {
                keyboardThrottleOverride = false;
            }
            throttle = -1f;
        }
        else if (shiftHoldingIdleAfterReverse && !decreaseThrottle)
        {
            if (hasJoystickThrottle)
            {
                if (!keyboardThrottleOverride)
                    joystickThrottleAtKeyboardTakeover = sidestickInput.Throttle;
                keyboardThrottleOverride = true;
            }
            else
            {
                keyboardThrottleOverride = false;
            }
            throttle = 0f;
        }
        else if (hasJoystickThrottle)
        {
            float joystickThrottle = sidestickInput.Throttle;
            if (hasKeyboardThrottleInput)
            {
                if (!keyboardThrottleOverride)
                    joystickThrottleAtKeyboardTakeover = joystickThrottle;
                keyboardThrottleOverride = true;
                throttle = ReverseThrustMath.UpdateSignedThrottle(
                    throttle,
                    increaseThrottle,
                    decreaseThrottle,
                    true,
                    throttleRate,
                    dt);
            }
            else if (!keyboardThrottleOverride ||
                     Mathf.Abs(joystickThrottle - joystickThrottleAtKeyboardTakeover) >= 0.02f)
            {
                keyboardThrottleOverride = false;
                throttle = joystickThrottle;
            }
        }
        else
        {
            keyboardThrottleOverride = false;
            throttle = ReverseThrustMath.UpdateSignedThrottle(
                throttle,
                increaseThrottle,
                decreaseThrottle,
                true,
                throttleRate,
                dt);
        }

        // ---- 襟翼:F 增加一级,V 减少一级 ----
        if (Input.GetKeyDown(KeyCode.F))
            flapStep = Mathf.Min(flapStep + 1, flapStepCount);
        if (Input.GetKeyDown(KeyCode.V))
            flapStep = Mathf.Max(flapStep - 1, 0);

        // ---- 扰流板:R 增加一级,T 减少一级 ----
        if (Input.GetKeyDown(KeyCode.R))
            spoilerStep = Mathf.Min(spoilerStep + 1, spoilerStepCount);
        if (Input.GetKeyDown(KeyCode.T))
            spoilerStep = Mathf.Max(spoilerStep - 1, 0);

        // ---- 起落架:G 切换,默认开始为放下 ----
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (!LandingGearToggleGate.CanUseToggleInputThisFrame)
            {
                Debug.Log("[FlightInput] Landing gear toggle ignored while gear animation is running.");
            }
            else if (!gearDown)
            {
                gearDown = true;
            }
            else if (CanRetractGear())
            {
                gearDown = false;
            }
            else
            {
                Debug.LogWarning(string.Format(
                    "[FlightInput] 起落架收起被阻止：飞机尚未离地 {0:F0} ft。",
                    minimumGearRetractionAglFt));
            }
        }
        // ---- 机轮刹车:B 键切换。地面停靠/防止怠速蠕动;起飞前松开 ----
        if (Input.GetKeyDown(KeyCode.B))
        {
            brakes = !brakes;
        }
        // 推油门起飞时自动松刹车,避免忘记按 B 推不动
        if (throttle > 0.15f)
        {
            brakes = false;
        }

        // ---- 按节流频率发送 ----
        sendTimer += dt;
        if (sendTimer >= 1f / sendRate)
        {
            sendTimer = 0f;
            SendControls();
        }
    }

    private bool CanRetractGear()
    {
        return bridge != null &&
               bridge.HasState &&
               bridge.AglFt >= minimumGearRetractionAglFt;
    }
    private void LateUpdate()
    {
        flapController?.SetFlapInput(Flaps);
        mechanicalController?.SetGearExtended(gearDown);
    }
    private void SetEscapePaused(bool paused)
    {
        escapePaused = paused;

        if (paused)
        {
            if (Time.timeScale > 0f)
                timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        }

        bridge?.RequestJsbsimPause(paused);
    }

    private void OnDisable()
    {
        if (!escapePaused) return;
        escapePaused = false;
        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        bridge?.RequestJsbsimPause(false);
    }

    /// <summary>有输入时朝输入方向加速,无输入时回中。</summary>
    private float StepAxis(float current, float input, float rate, float dt)
    {
        if (Mathf.Abs(input) > 0.01f)
            current += input * rate * dt;
        else
            current = Mathf.MoveTowards(current, 0f, centerRate * dt);
        return Mathf.Clamp(current, -1f, 1f);
    }

    /// <summary>有输入时改变舵位，无输入时保持当前舵位。</summary>
    private static float StepHeldAxis(float current, float input, float rate, float dt)
    {
        if (Mathf.Abs(input) > 0.01f)
            current += input * Mathf.Max(0f, rate) * Mathf.Max(0f, dt);

        return Mathf.Clamp(current, -1f, 1f);
    }

    /// <summary>正值表示抬头配平，负值表示低头配平；松键后保持当前值。</summary>
    private static float StepPitchTrim(
        float current,
        bool noseUpPressed,
        bool noseDownPressed,
        float rate,
        float dt)
    {
        float direction = 0f;
        if (noseUpPressed) direction += 1f;
        if (noseDownPressed) direction -= 1f;

        current += direction * Mathf.Max(0f, rate) * Mathf.Max(0f, dt);
        return Mathf.Clamp(current, -1f, 1f);
    }

    private void SendControls()
    {
        if (bridge == null || !bridge.ControlConnected) return;
        float coordinatedAileron = aileron;
        bool useTurnAssist = coordinatedTurnAssist &&
                             (!directJoystickControlActive || Mathf.Abs(turnInput) > coordinatedTurnDeadzone);
        bool hasTurnInput = useTurnAssist && Mathf.Abs(turnInput) > coordinatedTurnDeadzone;
        float desiredBankDeg = useTurnAssist && hasTurnInput
            ? turnInput * coordinatedTurnBankDeg
            : 0f;

        smoothedTurnBankTargetDeg = Mathf.MoveTowards(
            smoothedTurnBankTargetDeg,
            desiredBankDeg,
            bankTargetSlewRate * Time.deltaTime);

        if (useTurnAssist && bridge.HasState &&
            (hasTurnInput ||
             Mathf.Abs(bridge.RollDeg) > levelBankDeadzoneDeg ||
             Mathf.Abs(smoothedTurnBankTargetDeg) > levelBankDeadzoneDeg))
        {
            float bankErrorDeg = smoothedTurnBankTargetDeg - bridge.RollDeg;
            float rollRateDeg = bridge.GetValue("p_rps", 0f) * Mathf.Rad2Deg;
            float aileronLimit = hasTurnInput ? maxCoordinatedAileron : maxLevelingAileron;
            float bankCommand = Mathf.Clamp(
                bankErrorDeg * bankHoldGain - rollRateDeg * rollRateDamping,
                -aileronLimit,
                aileronLimit);
            coordinatedAileron += bankCommand;
        }
        else if (useTurnAssist && hasTurnInput)
        {
            coordinatedAileron += turnInput * yawToAileron;
        }

        float elevatorCommand = Mathf.Clamp(elevator * elevatorAuthority, -1f, 1f);
        float aileronCommand = Mathf.Clamp(coordinatedAileron * aileronAuthority, -1f, 1f);
        if (bankAngleProtection && bridge.HasState && bridge.SpeedKts >= bankProtectionMinSpeedKts)
        {
            float rollRateDeg = bridge.GetValue("p_rps", 0f) * Mathf.Rad2Deg;
            float predictedBankAngleDeg = bridge.RollDeg + rollRateDeg * bankProtectionLookAheadSeconds;
            aileronCommand = BankAngleProtectionMath.CalculateAileronCommand(
                aileronCommand,
                predictedBankAngleDeg,
                maxBankAngleDeg,
                bankProtectionSoftZoneDeg,
                bankProtectionCorrectionGain,
                maxBankProtectionCorrection);
        }
        float rudderCommand = Mathf.Clamp(rudder * rudderAuthority, -1f, 1f);
        if (sideSlipProtection && bridge.HasState && bridge.SpeedKts >= sideSlipProtectionMinSpeedKts)
        {
            rudderCommand = SideSlipProtectionMath.CalculateRudderCommand(
                rudderCommand,
                bridge.SideSlipDeg,
                maxSideSlipDeg,
                sideSlipSoftZoneDeg,
                sideSlipCorrectionGain,
                maxSideSlipRudderCorrection);
        }

        bridge.SetProperty("fcs/elevator-cmd-norm", elevatorCommand);
        // 737.xml 将配平与升降舵同号相加；模型中负升降舵产生抬头力矩。
        bridge.SetProperty("fcs/pitch-trim-cmd-norm", -pitchTrim);
        bridge.SetProperty("fcs/aileron-cmd-norm", aileronCommand);
        bridge.SetProperty("fcs/rudder-cmd-norm", rudderCommand);
        bridge.SetProperty("fcs/steer-cmd-norm", CalculateGroundSteeringCommand());
        ReverseThrustMath.CalculateEngineCommands(
            throttle,
            true,
            reverseThrustAngleRad,
            out float engineThrottle,
            out float reverserAngleRad);
        // 先改变反推方向，再发送油门，避免切换瞬间产生错误方向的推力。
        bridge.SetProperty("propulsion/engine[0]/reverser-angle-rad", reverserAngleRad);
        bridge.SetProperty("propulsion/engine[1]/reverser-angle-rad", reverserAngleRad);
        bridge.SetProperty("fcs/throttle-cmd-norm[0]", engineThrottle);
        bridge.SetProperty("fcs/throttle-cmd-norm[1]", engineThrottle);
        bridge.SetProperty("fcs/flap-cmd-norm", Flaps);
        bridge.SetProperty("fcs/speedbrake-cmd-norm", Spoilers);
        bridge.SetProperty("gear/gear-cmd-norm", gearDown ? 1f : 0f);
        // 机轮刹车:左右主轮(737 前轮无刹车)。1=全刹,0=松开
        float b = brakes ? 1f : 0f;
        bridge.SetProperty("fcs/left-brake-cmd-norm", b);
        bridge.SetProperty("fcs/right-brake-cmd-norm", b);
    }

    private float CalculateGroundSteeringCommand()
    {
        if (!groundSteeringEnabled || bridge == null || !bridge.HasState ||
            bridge.AglFt > groundSteeringMaxAglFt)
        {
            return 0f;
        }

        float steeringInput = maxTurnRudderInput > 0.001f
            ? rudder / maxTurnRudderInput
            : rudder;
        float steeringCommand = Mathf.Clamp(steeringInput * groundSteeringAuthority, -1f, 1f);
        return invertGroundSteering ? -steeringCommand : steeringCommand;
    }

    // 给 HUD 读取
    public void SetThrottle(float value)
    {
        throttle = value < 0f ? -1f : Mathf.Clamp01(value);

        if (sidestickInput != null && sidestickInput.ThrottleControlEnabled)
        {
            keyboardThrottleOverride = true;
            joystickThrottleAtKeyboardTakeover = sidestickInput.Throttle;
        }
    }

    public void SetFlapStep(int step)
    {
        flapStep = Mathf.Clamp(step, 0, flapStepCount);
    }

    public void SetSpoilerStep(int step)
    {
        spoilerStep = Mathf.Clamp(step, 0, spoilerStepCount);
    }

    public bool TrySetGearDown(bool down, out string rejectionReason)
    {
        rejectionReason = string.Empty;
        if (!LandingGearToggleGate.CanUseToggleInputThisFrame)
        {
            rejectionReason = "起落架正在运动，请稍后重试";
            return false;
        }

        if (!down && !CanRetractGear())
        {
            rejectionReason = string.Format(
                "飞机尚未离地 {0:F0} 英尺，拒绝收起起落架",
                minimumGearRetractionAglFt);
            return false;
        }

        if (!TrySetLandingGearVisuals(down))
        {
            rejectionReason = "起落架可视机构未能接受指令，请稍后重试";
            return false;
        }

        gearDown = down;
        return true;
    }

    private bool TrySetLandingGearVisuals(bool extended)
    {
        bool accepted = true;

        if (landingGearDoorSequences != null)
        {
            foreach (LandingGearDoorSequenceController controller in landingGearDoorSequences)
            {
                if (controller != null && controller.isActiveAndEnabled)
                    accepted &= controller.TrySetGearExtended(extended);
            }
        }

        if (synchronizedLandingGearControllers != null)
        {
            foreach (LandingGearSynchronizedDoorGearController controller in synchronizedLandingGearControllers)
            {
                if (controller != null && controller.isActiveAndEnabled)
                    accepted &= controller.TrySetGearExtended(extended);
            }
        }

        if (standaloneLandingGearHinges != null)
        {
            foreach (LandingGearHingeRetractionController controller in standaloneLandingGearHinges)
            {
                if (controller != null && controller.isActiveAndEnabled)
                    accepted &= controller.TrySetGearExtended(extended);
            }
        }

        return accepted;
    }

    private void CacheStandaloneLandingGearHinges()
    {
        LandingGearHingeRetractionController[] allHinges =
            GetComponentsInChildren<LandingGearHingeRetractionController>(true);
        var standalone = new System.Collections.Generic.List<LandingGearHingeRetractionController>();
        foreach (LandingGearHingeRetractionController hinge in allHinges)
        {
            if (hinge == null)
                continue;

            bool managedByDoorSequence =
                hinge.GetComponentInParent<LandingGearDoorSequenceController>() != null ||
                hinge.GetComponentInParent<LandingGearSynchronizedDoorGearController>() != null;
            if (!managedByDoorSequence)
                standalone.Add(hinge);
        }

        standaloneLandingGearHinges = standalone.ToArray();
    }

    public void SetBrakes(bool enabled)
    {
        brakes = enabled;
    }

    public void AdjustPitchTrim(float delta)
    {
        pitchTrim = Mathf.Clamp(pitchTrim + delta, -1f, 1f);
    }

    public void SetPaused(bool paused)
    {
        if (escapePaused != paused)
        {
            SetEscapePaused(paused);
        }
    }

    public float Elevator => elevator;
    public float PitchTrim => pitchTrim;
    public float Aileron => aileron;
    public float Rudder => rudder;
    public float Throttle => throttle;
    public bool ReverseThrustActive => throttle < 0f;
    public float Flaps => flapStepCount > 0 ? (float)flapStep / flapStepCount : 0f;
    public int FlapStep => flapStep;
    public int FlapStepCount => flapStepCount;
    public float Spoilers => spoilerStepCount > 0 ? (float)spoilerStep / spoilerStepCount : 0f;
    public int SpoilerStep => spoilerStep;
    public int SpoilerStepCount => spoilerStepCount;
    public bool GearDown => gearDown;
    public bool Brakes => brakes;
    public bool IsPaused => escapePaused;
}

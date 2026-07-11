using UnityEngine;

/// <summary>
/// 键盘飞行操控,把输入转成 JSBSim 控制量发送出去。
///
/// 默认按键:
///   W / S        : 升降舵(俯仰)。W 推杆低头,S 拉杆抬头。
///   A / D        : 副翼(滚转)。A 左滚,D 右滚。
///   Q / E        : 方向舵(偏航)。
///   Shift / Ctrl : 油门加 / 减。
///   F            : 襟翼切换(0 / 0.5 / 1)。
///   方向键不再控制飞机:上下左右留给相机视角移动。
///
/// 控制量采用"自动回中"逻辑:松开按键后舵面平滑回到中立位,更接近真实手感。
/// 油门是保持型(不会自动回中)。
/// </summary>
[RequireComponent(typeof(JsbsimBridge))]
public class FlightInput : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private B737FlapController flapController;
    [SerializeField] private B737MechanicalController mechanicalController;

    [Header("Pause")]
    [SerializeField] private bool enableEscapePause = true;

    [Header("舵面灵敏度(每秒变化量)")]
    [SerializeField] private float elevatorRate = 0.5f;
    [SerializeField] private float aileronRate = 1.0f;
    [SerializeField] private float rudderRate = 0.8f;
    [Tooltip("松开按键后舵面回中速度。")]
    [SerializeField] private float centerRate = 0.75f;

    [Header("油门")]
    [SerializeField] private float throttleRate = 0.5f;

    [Header("High Lift and Drag Controls")]
    [SerializeField, Min(1)] private int flapStepCount = 4;
    [SerializeField, Min(1)] private int spoilerStepCount = 4;
    [SerializeField, Min(0f)] private float minimumGearRetractionAglFt = 10f;
    [Header("Control Tuning")]
    [Tooltip("中文名：升降舵中立偏置。负值会给飞机一点抬头趋势，正值会给一点低头趋势。")]
    [SerializeField, Range(-1f, 1f)] private float elevatorNeutralBias = 0f;
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
    [Tooltip("中文名：启用俯仰保持辅助。开启后转弯时会自动给一点抬头补偿，减少掉高度。")]
    [SerializeField] private bool pitchHoldAssist = true;
    [Tooltip("中文名：目标俯仰角。俯仰保持辅助希望维持的机头角度。")]
    [SerializeField] private float pitchHoldDeg = 0f;
    [Tooltip("中文名：俯仰保持增益。数值越大越积极拉回目标俯仰角，太大容易上下晃。")]
    [SerializeField] private float pitchHoldGain = 0.03f;
    [Tooltip("中文名：转弯抬头补偿。坡度越大时给越多抬头，减少大转弯掉高度。")]
    [SerializeField] private float turnPitchCompensation = 0.012f;
    [Tooltip("中文名：最大俯仰辅助。限制自动抬头补偿的最大强度，避免机头拉得过猛。")]
    [SerializeField, Range(0f, 1f)] private float maxPitchAssist = 0.45f;

    [Header("发送频率")]
    [Tooltip("每秒向 JSBSim 发送控制命令的次数。50 足够顺滑。")]
    [SerializeField] private float sendRate = 50f;

    // 当前归一化控制量 [-1,1],油门 [0,1]
    private float elevator;
    private float aileron;
    private float rudder;
    private float turnInput;
    private float smoothedTurnBankTargetDeg;
    private float throttle = 0f;   // 地面起飞:初始油门怠速 0,推 Shift 才加速
    private int flapStep;
    private int spoilerStep;
    private bool gearDown = true;
    private bool brakes = true;    // 地面起飞:初始刹车锁定,飞机停得住;松刹车(B)再滑跑

    private float sendTimer;
    private bool escapePaused;
    private float timeScaleBeforePause = 1f;

    private void Awake()
    {
        if (bridge == null) bridge = GetComponent<JsbsimBridge>();
        if (flapController == null) flapController = GetComponent<B737FlapController>();
        if (mechanicalController == null) mechanicalController = GetComponent<B737MechanicalController>();
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

        // ---- 升降舵:S 拉杆抬头, W 推杆低头 ----
        float pitchInput = 0f;
        if (Input.GetKey(KeyCode.W)) pitchInput += 1f;
        if (Input.GetKey(KeyCode.S)) pitchInput -= 1f;
        elevator = StepAxis(elevator, pitchInput, elevatorRate, dt);

        // ---- 副翼:A 左滚, D 右滚 ----
        float rollInput = 0f;
        if (Input.GetKey(KeyCode.D)) rollInput += 1f;
        if (Input.GetKey(KeyCode.A)) rollInput -= 1f;
        aileron = StepAxis(aileron, rollInput, aileronRate, dt);

        // ---- 方向舵:Q 左偏航, E 右偏航 ----
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
            rudder = Mathf.MoveTowards(rudder, rudderTarget, rudderRate * dt);
        }
        else
        {
            rudder = StepAxis(rudder, yawInput, rudderRate, dt);
        }

        // ---- 油门:LeftShift 加, LeftControl 减(保持型)----
        if (Input.GetKey(KeyCode.LeftShift)) throttle += throttleRate * dt;
        if (Input.GetKey(KeyCode.LeftControl)) throttle -= throttleRate * dt;
        throttle = Mathf.Clamp01(throttle);

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

    private void SendControls()
    {
        if (bridge == null || !bridge.ControlConnected) return;
        float pitchAssist = CalculatePitchAssist();
        float coordinatedAileron = aileron;
        bool hasTurnInput = Mathf.Abs(turnInput) > coordinatedTurnDeadzone;
        float desiredBankDeg = coordinatedTurnAssist && hasTurnInput
            ? turnInput * coordinatedTurnBankDeg
            : 0f;

        smoothedTurnBankTargetDeg = Mathf.MoveTowards(
            smoothedTurnBankTargetDeg,
            desiredBankDeg,
            bankTargetSlewRate * Time.deltaTime);

        if (coordinatedTurnAssist && bridge.HasState &&
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
        else if (coordinatedTurnAssist && hasTurnInput)
        {
            coordinatedAileron += turnInput * yawToAileron;
        }

        float elevatorCommand = Mathf.Clamp(elevator * elevatorAuthority + elevatorNeutralBias + pitchAssist, -1f, 1f);
        float aileronCommand = Mathf.Clamp(coordinatedAileron * aileronAuthority, -1f, 1f);
        float rudderCommand = Mathf.Clamp(rudder * rudderAuthority, -1f, 1f);

        bridge.SetProperty("fcs/elevator-cmd-norm", elevatorCommand);
        bridge.SetProperty("fcs/aileron-cmd-norm", aileronCommand);
        bridge.SetProperty("fcs/rudder-cmd-norm", rudderCommand);
        bridge.SetProperty("fcs/throttle-cmd-norm[0]", throttle);
        bridge.SetProperty("fcs/throttle-cmd-norm[1]", throttle);
        bridge.SetProperty("fcs/flap-cmd-norm", Flaps);
        bridge.SetProperty("fcs/speedbrake-cmd-norm", Spoilers);
        bridge.SetProperty("gear/gear-cmd-norm", gearDown ? 1f : 0f);
        // 机轮刹车:左右主轮(737 前轮无刹车)。1=全刹,0=松开
        float b = brakes ? 1f : 0f;
        bridge.SetProperty("fcs/left-brake-cmd-norm", b);
        bridge.SetProperty("fcs/right-brake-cmd-norm", b);
    }

    private float CalculatePitchAssist()
    {
        if (!pitchHoldAssist || bridge == null || !bridge.HasState)
            return 0f;

        float pitchError = pitchHoldDeg - bridge.PitchDeg;
        float pitchHold = -pitchError * pitchHoldGain;
        float turnHold = -Mathf.Abs(bridge.RollDeg) * turnPitchCompensation;
        return Mathf.Clamp(pitchHold + turnHold, -maxPitchAssist, maxPitchAssist);
    }

    // 给 HUD 读取
    public float Elevator => elevator;
    public float Aileron => aileron;
    public float Rudder => rudder;
    public float Throttle => throttle;
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

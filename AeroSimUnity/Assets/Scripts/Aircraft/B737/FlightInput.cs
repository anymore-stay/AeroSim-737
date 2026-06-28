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

    [Header("舵面灵敏度(每秒变化量)")]
    [SerializeField] private float elevatorRate = 0.5f;
    [SerializeField] private float aileronRate = 0.625f;
    [SerializeField] private float rudderRate = 0.5f;
    [Tooltip("松开按键后舵面回中速度。")]
    [SerializeField] private float centerRate = 0.75f;

    [Header("油门")]
    [SerializeField] private float throttleRate = 0.5f;

    [Header("发送频率")]
    [Tooltip("每秒向 JSBSim 发送控制命令的次数。50 足够顺滑。")]
    [SerializeField] private float sendRate = 50f;

    // 当前归一化控制量 [-1,1],油门 [0,1]
    private float elevator;
    private float aileron;
    private float rudder;
    private float throttle = 0f;   // 地面起飞:初始油门怠速 0,推 Shift 才加速
    private float flaps;
    private bool brakes = true;    // 地面起飞:初始刹车锁定,飞机停得住;松刹车(B)再滑跑

    private float sendTimer;

    private void Awake()
    {
        if (bridge == null) bridge = GetComponent<JsbsimBridge>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // ---- 升降舵:S 拉杆抬头, W 推杆低头 ----
        float pitchInput = 0f;
        if (Input.GetKey(KeyCode.W)) pitchInput += 1f;
        if (Input.GetKey(KeyCode.S)) pitchInput -= 1f;
        elevator = StepAxis(elevator, pitchInput, elevatorRate, dt);

        // ---- 副翼:A 左滚, D 右滚 ----
        float rollInput = 0f;
        if (Input.GetKey(KeyCode.D)) rollInput -= 1f;
        if (Input.GetKey(KeyCode.A)) rollInput += 1f;
        aileron = StepAxis(aileron, rollInput, aileronRate, dt);

        // ---- 方向舵:Q 左偏航, E 右偏航 ----
        float yawInput = 0f;
        if (Input.GetKey(KeyCode.E)) yawInput -= 1f;
        if (Input.GetKey(KeyCode.Q)) yawInput += 1f;
        rudder = StepAxis(rudder, yawInput, rudderRate, dt);

        // ---- 油门:LeftShift 加, LeftControl 减(保持型)----
        if (Input.GetKey(KeyCode.LeftShift)) throttle += throttleRate * dt;
        if (Input.GetKey(KeyCode.LeftControl)) throttle -= throttleRate * dt;
        throttle = Mathf.Clamp01(throttle);

        // ---- 襟翼:F 键循环 0 -> 0.5 -> 1 -> 0 ----
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (flaps < 0.25f) flaps = 0.5f;
            else if (flaps < 0.75f) flaps = 1f;
            else flaps = 0f;
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
        bridge.SetProperty("fcs/elevator-cmd-norm", elevator);
        bridge.SetProperty("fcs/aileron-cmd-norm", aileron);
        bridge.SetProperty("fcs/rudder-cmd-norm", rudder);
        bridge.SetProperty("fcs/throttle-cmd-norm[0]", throttle);
        bridge.SetProperty("fcs/throttle-cmd-norm[1]", throttle);
        bridge.SetProperty("fcs/flap-cmd-norm", flaps);
        // 机轮刹车:左右主轮(737 前轮无刹车)。1=全刹,0=松开
        float b = brakes ? 1f : 0f;
        bridge.SetProperty("fcs/left-brake-cmd-norm", b);
        bridge.SetProperty("fcs/right-brake-cmd-norm", b);
    }

    // 给 HUD 读取
    public float Elevator => elevator;
    public float Aileron => aileron;
    public float Rudder => rudder;
    public float Throttle => throttle;
    public float Flaps => flaps;
    public bool Brakes => brakes;
}

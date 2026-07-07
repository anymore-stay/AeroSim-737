using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// JSBSim <-> Unity 桥接核心。
///
/// 数据流:
///   - UDP 5501 (JSBSim -> Unity): 接收飞机状态(CSV 文本),驱动飞机 GameObject 的位置和姿态。
///   - TCP 5502 (Unity -> JSBSim): 发送 telnet 风格控制命令,如 "set fcs/throttle-cmd-norm 0.8\n"。
///
/// 坐标系转换:
///   JSBSim 用 NED(North-East-Down)+ 机体欧拉角 phi(滚转)/theta(俯仰)/psi(偏航,真航向)。
///   Unity 用左手系 X=右(East)、Y=上(Up)、Z=前(North)。
///   位置:以首帧经纬度为原点,用局部平面近似把经纬度差换算成米。
///   姿态:psi 绕 Unity 的 Y 轴(航向),theta 绕 X 轴(俯仰),phi 绕 Z 轴(滚转)。
///
/// 用法:挂到场景里任意 GameObject(建议空物体 "JSBSimBridge"),
///       把要驱动的飞机 Transform 拖到 aircraft 字段。
/// </summary>
public class JsbsimBridge : MonoBehaviour
{
    public static JsbsimBridge Instance { get; private set; }

    [Header("网络设置")]
    [Tooltip("接收 JSBSim 状态的 UDP 端口,需与 unity_output.xml 一致。")]
    [SerializeField] private int stateUdpPort = 5501;
    [Tooltip("发送控制命令的 JSBSim 主机地址。")]
    [SerializeField] private string controlHost = "127.0.0.1";
    [Tooltip("发送控制命令的 TCP 端口,需与脚本里 <input port> 一致。")]
    [SerializeField] private int controlTcpPort = 5502;

    [Header("被驱动的飞机")]
    [Tooltip("JSBSim 会驱动这个 Transform 的位置和姿态。留空则驱动本物体。")]
    [SerializeField] private Transform aircraft;

    [Header("世界缩放")]
    [Tooltip("英尺转米。1 英尺 = 0.3048 米。")]
    [SerializeField] private float feetToMeters = 0.3048f;
    [Tooltip("把飞机放到场景里的高度偏移(米),用于让飞机出现在地形之上便于观察。")]
    [SerializeField] private float altitudeOffset = 0f;

    [Header("模型朝向修正")]
    [Tooltip("绕 Y 轴的额外偏航修正(度),用于对齐模型几何朝向与飞行方向。\n本项目 737 模型机头朝 -Z(机尾朝 +Z),与 JSBSim 航向约定差 180°,故默认 180。")]
    [SerializeField] private float modelYawOffsetDeg = 180f;

    [Header("平滑")]
    [Tooltip("位置/姿态插值速度。0 表示直接硬切(最跟手),越大越平滑但有延迟。建议 10~20。")]
    [SerializeField] private float smoothing = 15f;

    [Header("浮动原点(消除渲染抖动)")]
    [Tooltip("开启后,由 FloatingOriginManager 在飞机远离原点时触发三维原点平移。\n" +
             "这消除了 GPU 顶点变换时大数相减导致的精度抖动(发光贴图尤其明显)。\n" +
             "原理:相机世界坐标恒为 0,Model 矩阵的值就是相对相机的偏移,精度最佳。")]
    [SerializeField] private bool useFloatingOrigin = true;
    [Tooltip("兼容旧配置:需要随飞机一起平移的场景根物体(地形、天空盒挂点、其他环境)。新对象优先挂 FloatingOriginObject。飞机本身不要放进来。")]
    [SerializeField] private Transform[] floatingOriginObjects;

    [Header("调试")]
    [SerializeField] private bool logState = false;

    // ---- 对外只读的最新状态(主线程可安全读) ----
    public bool HasState { get; private set; }
    public float AltitudeFt { get; private set; }
    public float AglFt { get; private set; }
    public float SpeedKts { get; private set; }       // 校准空速
    public float TrueSpeedKts { get; private set; }
    public float VerticalSpeedFps { get; private set; }
    public float HeadingDeg { get; private set; }     // psi
    public float PitchDeg { get; private set; }       // theta
    public float RollDeg { get; private set; }        // phi
    public float Rpm { get; private set; }
    public bool ControlConnected { get; private set; }

    public event Action OnStateUpdated;

    public IReadOnlyDictionary<string, float> Snapshot
    {
        get
        {
            lock (stateLock)
            {
                return new Dictionary<string, float>(latest);
            }
        }
    }

    public string[] AvailableKeys
    {
        get
        {
            lock (stateLock)
            {
                var keys = new string[latest.Keys.Count];
                latest.Keys.CopyTo(keys, 0);
                return keys;
            }
        }
    }

    // ---- UDP 接收线程相关 ----
    private UdpClient udpClient;
    private Thread udpThread;
    private volatile bool running;
    private string[] labels;
    private readonly object stateLock = new object();
    private Dictionary<string, float> latest = new Dictionary<string, float>();
    private bool newStateAvailable;

    // ---- TCP 控制相关 ----
    private TcpClient tcpClient;
    private NetworkStream tcpStream;
    private Thread tcpConnectThread;
    private Thread tcpDrainThread;

    // ---- 经纬度原点(首帧锚定) ----
    private bool originSet;
    private double originLatRad;
    private double originLonRad;
    private float originAltM;          // 首帧海拔(米),用于让高度相对于场景位置
    private const double EarthRadiusM = 6378137.0; // WGS84 赤道半径

    // ---- 场景起飞位置:飞机在编辑器中摆的位置,JSBSim 经纬度原点映射到这里 ----
    private Vector3 sceneStartPos;

    // 目标位姿(线程收到后,主线程插值逼近)
    private Vector3 targetPos;
    private Quaternion targetRot = Quaternion.identity;
    private bool firstPose = true;

    // 浮动原点累计偏移:把绝对世界坐标换算到当前已平移的渲染坐标
    private Vector3 accumulatedOriginShift = Vector3.zero;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[JSBSim] Scene has more than one JsbsimBridge. Instance keeps the first enabled bridge.");
        }
        else
        {
            Instance = this;
        }

        if (aircraft == null) aircraft = transform;
        // 记录飞机在编辑器中的位置,JSBSim 首帧经纬度原点映射到这里,
        // 飞机在 Unity 场景中摆在哪就从哪起飞。
        sceneStartPos = aircraft.position;

        EnsureFloatingOriginManager();
    }

    private void OnEnable()
    {
        FloatingOriginManager.OriginShifted += HandleOriginShift;
        StartUdpReceiver();
        StartTcpControl();
    }

    private void OnDisable()
    {
        FloatingOriginManager.OriginShifted -= HandleOriginShift;
        StopAll();
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit()
    {
        StopAll();
    }

    // ====================== UDP 接收 ======================

    private void StartUdpReceiver()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, stateUdpPort));
            running = true;
            udpThread = new Thread(UdpLoop) { IsBackground = true };
            udpThread.Start();
            Debug.Log("[JSBSim] UDP 状态接收已启动,端口 " + stateUdpPort);
        }
        catch (Exception e)
        {
            Debug.LogError("[JSBSim] UDP 启动失败: " + e.Message);
        }
    }

    private void UdpLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remote);
                string text = Encoding.ASCII.GetString(data).Trim();
                ParsePacket(text);
            }
            catch (SocketException)
            {
                // 关闭时会抛异常,正常退出
                if (!running) break;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JSBSim] UDP 接收异常: " + e.Message);
            }
        }
    }

    private void ParsePacket(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 一个 UDP 包可能含多行;逐行处理
        string[] lines = text.Split('\n');
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("<LABELS>"))
            {
                string body = line.Substring("<LABELS>".Length).Trim().TrimStart(',');
                labels = SplitClean(body);
                continue;
            }

            string[] vals = SplitClean(line);
            if (labels == null || vals.Length != labels.Length) continue;

            lock (stateLock)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    float v;
                    if (float.TryParse(vals[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        latest[labels[i]] = v;
                }
                newStateAvailable = true;
            }
        }
    }

    private static string[] SplitClean(string s)
    {
        string[] parts = s.Split(',');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    public bool TryGetValue(string key, out float value)
    {
        lock (stateLock)
        {
            return latest.TryGetValue(key, out value);
        }
    }

    public float GetValue(string key, float fallback = 0f)
    {
        float value;
        return TryGetValue(key, out value) ? value : fallback;
    }

    // ====================== 主线程驱动飞机 ======================

    private void Update()
    {
        bool hasNewState;
        lock (stateLock)
        {
            hasNewState = newStateAvailable;
        }

        if (hasNewState)
        {
            Dictionary<string, float> snap;
            lock (stateLock)
            {
                snap = new Dictionary<string, float>(latest);
                newStateAvailable = false;
            }
            ApplyState(snap);
            HasState = true;
            OnStateUpdated?.Invoke();
        }

        if (HasState)
        {
            if (firstPose || smoothing <= 0f)
            {
                aircraft.position = targetPos;
                aircraft.rotation = targetRot;
                firstPose = false;
            }
            else
            {
                float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
                aircraft.position = Vector3.Lerp(aircraft.position, targetPos, t);
                aircraft.rotation = Quaternion.Slerp(aircraft.rotation, targetRot, t);
            }
        }
    }

    private void EnsureFloatingOriginManager()
    {
        if (!useFloatingOrigin || aircraft == null) return;

        var manager = FloatingOriginManager.Instance;
        if (manager == null)
        {
            manager = gameObject.AddComponent<FloatingOriginManager>();
        }
        manager.SetTarget(aircraft);
    }

    private void HandleOriginShift(Vector3 offset)
    {
        if (!useFloatingOrigin) return;

        accumulatedOriginShift += offset;
        targetPos += offset;

        if (aircraft != null)
            aircraft.position += offset;

        if (floatingOriginObjects == null) return;

        for (int i = 0; i < floatingOriginObjects.Length; i++)
        {
            Transform target = floatingOriginObjects[i];
            if (target == null) continue;
            if (target.GetComponent<FloatingOriginObject>() != null) continue;
            if (target.GetComponent<FloatingOriginRigidbody>() != null) continue;

            target.position += offset;
        }
    }

    private void ApplyState(Dictionary<string, float> s)
    {
        float Get(string k, float def = 0f) { float v; return s.TryGetValue(k, out v) ? v : def; }

        double latDeg = Get("lat_deg");
        double lonDeg = Get("lon_deg");
        AltitudeFt = Get("alt_ft");
        AglFt = Get("agl_ft");

        float phi = Get("phi_rad");    // 滚转
        float theta = Get("theta_rad"); // 俯仰
        float psi = Get("psi_rad");    // 偏航(真航向)

        SpeedKts = Get("vc_kts");
        TrueSpeedKts = Get("vtrue_kts");
        VerticalSpeedFps = Get("hdot_fps");
        Rpm = Get("rpm");

        RollDeg = phi * Mathf.Rad2Deg;
        PitchDeg = theta * Mathf.Rad2Deg;
        HeadingDeg = psi * Mathf.Rad2Deg;

        // ---- 位置:经纬度 -> 局部平面(米)----
        double latRad = latDeg * Math.PI / 180.0;
        double lonRad = lonDeg * Math.PI / 180.0;
        if (!originSet)
        {
            originLatRad = latRad;
            originLonRad = lonRad;
            originAltM = AltitudeFt * feetToMeters; // 首帧海拔作为地面基准
            originSet = true;
        }
        // 等距圆柱近似:北向距离 = R * dLat,东向距离 = R * dLon * cos(lat)
        double north = EarthRadiusM * (latRad - originLatRad);
        double east = EarthRadiusM * (lonRad - originLonRad) * Math.Cos(originLatRad);
        float altM = AltitudeFt * feetToMeters;

        // Unity: X=East, Y=Up, Z=North (标准约定)
        // 但本项目模型机头朝 -Z(机尾朝 +Z),如果用标准约定则飞机"机尾朝前"移动。
        // 修正方案:翻转位移方向(north→-Z, east→-X),让飞机向北飞时往 -Z 走,
        // 天然对齐模型机头方向,不需要旋转父物体(避免子物体 localPos 偏移导致视觉跳变)。
        targetPos = sceneStartPos
                    + new Vector3(-(float)east, altM - originAltM, -(float)north)
                    + accumulatedOriginShift;

        // ---- 姿态:NED 欧拉角 -> Unity 四元数 ----
        // 位移已翻转(north→-Z, east→-X),等价于把世界坐标系绕 Y 轴镜像 180°,
        // X 轴和 Z 轴方向都反了,所以绕这两个轴的旋转符号要翻转。
        // PitchDeg(绕 X 轴):X 轴翻了,符号反 → +PitchDeg。
        // RollDeg(绕 Z 轴):Z 轴翻了,但 JSBSim phi>0 是右滚,Unity 绕 +Z 是左滚,
        //   两个翻转抵消,符号仍为 -RollDeg。
        // HeadingDeg:Y 轴未变,模型自身机头朝 -Z 抵消了 180°,直接用 HeadingDeg。
        targetRot = Quaternion.Euler(PitchDeg, HeadingDeg, -RollDeg);

        if (logState)
            Debug.Log(string.Format("[JSBSim] alt={0:F0}ft spd={1:F0}kt hdg={2:F0} pitch={3:F1} roll={4:F1}",
                AltitudeFt, SpeedKts, HeadingDeg, PitchDeg, RollDeg));
    }

    // ====================== TCP 控制发送 ======================

    private void StartTcpControl()
    {
        tcpConnectThread = new Thread(ConnectTcpLoop) { IsBackground = true };
        tcpConnectThread.Start();
    }

    private void ConnectTcpLoop()
    {
        while (running || !ControlConnected)
        {
            if (!running) { }
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(controlHost, controlTcpPort);
                tcpStream = tcpClient.GetStream();
                ControlConnected = true;
                Debug.Log("[JSBSim] 控制通道已连接 TCP " + controlHost + ":" + controlTcpPort);
                // 启动回显读取线程:JSBSim telnet 会回 "JSBSim>" 提示符等文本,
                // 若不读走,JSBSim 端发送缓冲会出错并刷 "Socket error in Reply"。
                tcpDrainThread = new Thread(DrainTcpLoop) { IsBackground = true };
                tcpDrainThread.Start();
                return;
            }
            catch (Exception)
            {
                ControlConnected = false;
                Thread.Sleep(1000); // JSBSim 还没起,稍后重试
            }
            if (!running) return;
        }
    }

    /// <summary>设置 JSBSim 的某个属性(归一化控制量等)。线程安全。</summary>
    public void SetProperty(string property, float value)
    {
        if (!ControlConnected || tcpStream == null) return;
        try
        {
            string cmd = "set " + property + " " + value.ToString("F4", CultureInfo.InvariantCulture) + "\n";
            byte[] bytes = Encoding.ASCII.GetBytes(cmd);
            tcpStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[JSBSim] 发送控制失败: " + e.Message);
            ControlConnected = false;
        }
    }

    /// <summary>持续读走 JSBSim 的 telnet 回显并丢弃,防止其发送缓冲堵塞报错。</summary>
    private void DrainTcpLoop()
    {
        byte[] buf = new byte[1024];
        while (running && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                int n = tcpStream.Read(buf, 0, buf.Length);
                if (n <= 0) break; // 对端关闭
            }
            catch (Exception)
            {
                break; // 关闭或出错时退出
            }
        }
    }

    // ====================== 清理 ======================

    private void StopAll()
    {
        running = false;
        try { udpClient?.Close(); } catch { }
        try { tcpStream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        udpClient = null;
        tcpClient = null;
        tcpStream = null;
        ControlConnected = false;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [Tooltip("在 JSBSim 高度基础上额外施加的垂直偏移(米)。正值抬高飞机，负值降低飞机。")]
    [SerializeField] private float altitudeOffset = 0f;

    [Header("模型朝向修正")]
    [Tooltip("绕 Y 轴的额外偏航修正(度),用于对齐模型几何朝向与飞行方向。\n本项目 737 模型机头朝 -Z(机尾朝 +Z),与 JSBSim 航向约定差 180°,故默认 180。")]
    [SerializeField] private float modelYawOffsetDeg = 180f;

    [Header("平滑")]
    [Tooltip("位置/姿态插值速度。0 表示直接硬切(最跟手),越大越平滑但有延迟。建议 10~20。")]
    [SerializeField] private float smoothing = 15f;

    [Header("Cesium")]
    [SerializeField] private bool useCesiumGeoreference = true;
    [SerializeField] private double cesiumOriginShiftDistance = 250.0;
    [SerializeField] private bool anchorStaticCesiumChildren = true;
    [SerializeField] private bool preserveStaticCesiumChildRotation = true;

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
    public float AngleOfAttackDeg { get; private set; }
    public float Rpm { get; private set; }
    public bool ControlConnected => controlConnected;
    public Transform Aircraft => aircraft;


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
    private readonly Dictionary<string, float> mainThreadSnapshot = new Dictionary<string, float>();
    private bool newStateAvailable;

    // ---- TCP 控制相关 ----
    private TcpClient tcpClient;
    private NetworkStream tcpStream;
    private Thread tcpConnectThread;
    private readonly object tcpLock = new object();
    private volatile bool controlConnected;
    private volatile bool jsbsimPauseRequested;
    private bool timeScalePaused;
    private bool applicationPaused;
#if UNITY_EDITOR
    private bool editorPaused;
#endif

    // ---- 经纬度原点(首帧锚定) ----
    private bool originSet;
    private double originLatRad;
    private double originLonRad;
    private float originAltM;          // 首帧海拔(米),用于让高度相对于场景位置
    private const double EarthRadiusM = 6378137.0; // WGS84 赤道半径

    // ---- 场景起飞位置:飞机在编辑器中摆的位置,JSBSim 经纬度原点映射到这里 ----
    private Vector3 sceneStartPos;
    private Quaternion sceneStartRot = Quaternion.identity;

    // 目标位姿(线程收到后,主线程插值逼近)
    private Vector3 targetPos;
    private Quaternion targetRot = Quaternion.identity;
    private bool firstPose = true;

    // 浮动原点累计偏移:把绝对世界坐标换算到当前已平移的渲染坐标
    private Vector3 accumulatedOriginShift = Vector3.zero;

    private CesiumGeoreference cesiumGeoreference;
    private CesiumGlobeAnchor cesiumAnchor;
    private CesiumOriginShift cesiumOriginShift;
    private bool usingCesiumCoordinates;
    private double4x4 cesiumStartLocalToEcef = double4x4.identity;
    private float originHeadingDeg;
    private float originPitchDeg;
    private float originRollDeg;
    private float cesiumHorizontalAlignmentDeg;
    private bool hasCesiumTarget;
    private double3 targetCesiumEcefPosition;
    private double3 currentCesiumEcefPosition;
    private Quaternion targetCesiumRotation = Quaternion.identity;
    private Quaternion currentCesiumRotation = Quaternion.identity;
    private readonly List<StaticCesiumChild> staticCesiumChildren = new List<StaticCesiumChild>();

    private struct StaticCesiumChild
    {
        public Transform transform;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

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
        sceneStartRot = aircraft.rotation;

        ConfigureCesiumCoordinates();
        if (!usingCesiumCoordinates)
            EnsureFloatingOriginManager();
    }

    private void OnEnable()
    {
        if (!usingCesiumCoordinates)
            FloatingOriginManager.OriginShifted += HandleOriginShift;
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += HandleEditorPauseStateChanged;
        editorPaused = EditorApplication.isPaused;
#endif
        timeScalePaused = Mathf.Approximately(Time.timeScale, 0f);
        applicationPaused = false;
        UpdateJsbsimPauseRequest();
        running = true;
        StartUdpReceiver();
        StartTcpControl();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged -= HandleEditorPauseStateChanged;
#endif
        RequestJsbsimPause(true);
        if (cesiumGeoreference != null)
            cesiumGeoreference.changed -= HandleCesiumGeoreferenceChanged;
        if (!usingCesiumCoordinates)
            FloatingOriginManager.OriginShifted -= HandleOriginShift;
        StopAll();
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit()
    {
        RequestJsbsimPause(true);
        StopAll();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        applicationPaused = pauseStatus;
        UpdateJsbsimPauseRequest();
    }

#if UNITY_EDITOR
    private void HandleEditorPauseStateChanged(PauseState state)
    {
        editorPaused = state == PauseState.Paused;
        UpdateJsbsimPauseRequest();
    }
#endif

    // ====================== UDP 接收 ======================

    private void StartUdpReceiver()
    {
        try
        {
            udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Client.ExclusiveAddressUse = true;
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, stateUdpPort));
            UdpClient receiver = udpClient;
            udpThread = new Thread(() => UdpLoop(receiver)) { IsBackground = true };
            udpThread.Start();
            Debug.Log("[JSBSim] UDP 状态接收已启动,端口 " + stateUdpPort);
        }
        catch (Exception e)
        {
            try { udpClient?.Close(); } catch { }
            udpClient = null;
            Debug.LogError("[JSBSim] UDP 启动失败: " + e.Message);
        }
    }

    private void UdpLoop(UdpClient receiver)
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (running && ReferenceEquals(receiver, udpClient))
        {
            try
            {
                byte[] data = receiver.Receive(ref remote);
                string text = Encoding.ASCII.GetString(data).Trim();
                ParsePacket(text);
            }
            catch (SocketException)
            {
                // 关闭时会抛异常,正常退出
                if (!running || !ReferenceEquals(receiver, udpClient)) break;
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
        bool isTimeScalePaused = Mathf.Approximately(Time.timeScale, 0f);
        if (timeScalePaused != isTimeScalePaused)
        {
            timeScalePaused = isTimeScalePaused;
            UpdateJsbsimPauseRequest();
        }

        bool hasNewState;
        lock (stateLock)
        {
            hasNewState = newStateAvailable;
        }

        if (hasNewState)
        {
            lock (stateLock)
            {
                mainThreadSnapshot.Clear();
                foreach (KeyValuePair<string, float> pair in latest)
                {
                    mainThreadSnapshot[pair.Key] = pair.Value;
                }
                newStateAvailable = false;
            }
            ApplyState(mainThreadSnapshot);
            HasState = true;
            OnStateUpdated?.Invoke();
        }

        if (HasState && !usingCesiumCoordinates)
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
        else if (HasState && hasCesiumTarget && cesiumAnchor != null)
        {
            if (firstPose || smoothing <= 0f)
            {
                currentCesiumEcefPosition = targetCesiumEcefPosition;
                currentCesiumRotation = targetCesiumRotation;
            }
            else
            {
                float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
                currentCesiumEcefPosition = math.lerp(currentCesiumEcefPosition, targetCesiumEcefPosition, t);
                currentCesiumRotation = Quaternion.Slerp(currentCesiumRotation, targetCesiumRotation, t);
            }

            ApplyCesiumPose(currentCesiumEcefPosition, currentCesiumRotation);
            firstPose = false;
        }
    }

    private void ApplyCesiumPose(double3 ecefPosition, Quaternion rotation)
    {
        cesiumAnchor.positionGlobeFixed = ecefPosition;
        cesiumAnchor.rotationEastUpNorth = new quaternion(
            rotation.x,
            rotation.y,
            rotation.z,
            rotation.w);
    }

    private float CalculateCesiumHorizontalAlignment()
    {
        Vector3 sceneNose = sceneStartRot * Vector3.back;
        sceneNose.y = 0f;
        if (sceneNose.sqrMagnitude < 0.0001f)
            return 0f;

        float headingRad = originHeadingDeg * Mathf.Deg2Rad;
        Vector3 jsbsimStartDirection = new Vector3(-Mathf.Sin(headingRad), 0f, -Mathf.Cos(headingRad));
        return Vector3.SignedAngle(jsbsimStartDirection.normalized, sceneNose.normalized, Vector3.up);
    }

    private void ConfigureCesiumCoordinates()
    {
        if (!useCesiumGeoreference || aircraft == null) return;

        cesiumGeoreference = aircraft.GetComponentInParent<CesiumGeoreference>();
        if (cesiumGeoreference == null)
            cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
        if (cesiumGeoreference == null) return;

        if (aircraft.GetComponentInParent<CesiumGeoreference>() == null)
            aircraft.SetParent(cesiumGeoreference.transform, true);

        sceneStartPos = aircraft.localPosition;
        sceneStartRot = aircraft.localRotation;

        cesiumGeoreference.Initialize();
        cesiumStartLocalToEcef = cesiumGeoreference.localToEcefMatrix;

        cesiumAnchor = aircraft.GetComponent<CesiumGlobeAnchor>();
        if (cesiumAnchor == null)
            cesiumAnchor = aircraft.gameObject.AddComponent<CesiumGlobeAnchor>();
        cesiumAnchor.detectTransformChanges = false;
        cesiumAnchor.adjustOrientationForGlobeWhenMoving = false;

        cesiumOriginShift = aircraft.GetComponent<CesiumOriginShift>();
        if (cesiumOriginShift == null)
            cesiumOriginShift = aircraft.gameObject.AddComponent<CesiumOriginShift>();
        cesiumOriginShift.distance = cesiumOriginShiftDistance;

        usingCesiumCoordinates = true;
        NeutralizeLegacyFloatingOriginForCesium();

        AnchorStaticCesiumChildren();
    }

    private void NeutralizeLegacyFloatingOriginForCesium()
    {
        FloatingOriginManager.OriginShifted -= HandleOriginShift;
        accumulatedOriginShift = Vector3.zero;

        FloatingOriginManager manager = aircraft.GetComponent<FloatingOriginManager>();
        if (manager != null && manager.Target == aircraft)
            manager.SetTarget(null);

        if (cesiumGeoreference == null) return;

        FloatingOriginObject[] floatingObjects = cesiumGeoreference.GetComponentsInChildren<FloatingOriginObject>(true);
        for (int i = 0; i < floatingObjects.Length; i++)
        {
            if (floatingObjects[i] == null) continue;
            Destroy(floatingObjects[i]);
        }
    }

    private void AnchorStaticCesiumChildren()
    {
        if (!anchorStaticCesiumChildren || cesiumGeoreference == null) return;

        staticCesiumChildren.Clear();
        cesiumGeoreference.changed -= HandleCesiumGeoreferenceChanged;

        for (int i = 0; i < cesiumGeoreference.transform.childCount; i++)
        {
            Transform child = cesiumGeoreference.transform.GetChild(i);
            if (child == null || child == aircraft) continue;
            if (child.GetComponent<Cesium3DTileset>() != null) continue;
            if (child.GetComponent<CesiumGlobeAnchor>() != null) continue;

            Quaternion originalRotation = child.localRotation;
            Vector3 originalScale = child.localScale;
            CesiumGlobeAnchor anchor = child.GetComponent<CesiumGlobeAnchor>();
            if (anchor == null)
                anchor = child.gameObject.AddComponent<CesiumGlobeAnchor>();

            anchor.detectTransformChanges = false;
            anchor.adjustOrientationForGlobeWhenMoving = false;
            anchor.Sync();
            child.localRotation = originalRotation;
            child.localScale = originalScale;

            staticCesiumChildren.Add(new StaticCesiumChild
            {
                transform = child,
                localRotation = originalRotation,
                localScale = originalScale
            });
        }

        if (staticCesiumChildren.Count > 0)
            cesiumGeoreference.changed += HandleCesiumGeoreferenceChanged;

        HandleCesiumGeoreferenceChanged();
    }

    private void HandleCesiumGeoreferenceChanged()
    {
        if (!preserveStaticCesiumChildRotation) return;

        for (int i = 0; i < staticCesiumChildren.Count; i++)
        {
            StaticCesiumChild child = staticCesiumChildren[i];
            if (child.transform == null) continue;

            child.transform.localRotation = child.localRotation;
            child.transform.localScale = child.localScale;
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
        AngleOfAttackDeg = Get("aero_alpha_deg");
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
            originHeadingDeg = HeadingDeg;
            originPitchDeg = PitchDeg;
            originRollDeg = RollDeg;
            cesiumHorizontalAlignmentDeg = CalculateCesiumHorizontalAlignment();
            originAltM = AltitudeFt * feetToMeters; // 首帧海拔作为地面基准
            originSet = true;
        }
        // 等距圆柱近似:北向距离 = R * dLat,东向距离 = R * dLon * cos(lat)
        double north = EarthRadiusM * (latRad - originLatRad);
        double east = EarthRadiusM * (lonRad - originLonRad) * Math.Cos(originLatRad);
        float altM = AltitudeFt * feetToMeters;
        float verticalOffsetM = altM - originAltM;

        float northFt;
        float eastFt;
        float upFt;
        if (s.TryGetValue("position_from_start_neu_n_ft", out northFt) &&
            s.TryGetValue("position_from_start_neu_e_ft", out eastFt) &&
            s.TryGetValue("position_from_start_neu_u_ft", out upFt))
        {
            north = northFt * feetToMeters;
            east = eastFt * feetToMeters;
            verticalOffsetM = upFt * feetToMeters;
        }

        // Unity: X=East, Y=Up, Z=North (标准约定)
        // 但本项目模型机头朝 -Z(机尾朝 +Z),如果用标准约定则飞机"机尾朝前"移动。
        // 修正方案:翻转位移方向(north→-Z, east→-X),让飞机向北飞时往 -Z 走,
        // 天然对齐模型机头方向,不需要旋转父物体(避免子物体 localPos 偏移导致视觉跳变)。
        Vector3 horizontalOffset = new Vector3(-(float)east, 0f, -(float)north);
        if (usingCesiumCoordinates)
            horizontalOffset = Quaternion.Euler(0f, cesiumHorizontalAlignmentDeg, 0f) * horizontalOffset;

        Vector3 unshiftedTargetPos = CalculateTargetPosition(
            sceneStartPos,
            horizontalOffset,
            verticalOffsetM,
            altitudeOffset);
        targetPos = unshiftedTargetPos + accumulatedOriginShift;

        // ---- 姿态:NED 欧拉角 -> Unity 四元数 ----
        // 位移已翻转(north→-Z, east→-X),等价于把世界坐标系绕 Y 轴镜像 180°,
        // X 轴和 Z 轴方向都反了,所以绕这两个轴的旋转符号要翻转。
        // PitchDeg(绕 X 轴):X 轴翻了,符号反 → +PitchDeg。
        // RollDeg(绕 Z 轴):Z 轴翻了,但 JSBSim phi>0 是右滚,Unity 绕 +Z 是左滚,
        //   两个翻转抵消,符号仍为 -RollDeg。
        // HeadingDeg:Y 轴未变,模型自身机头朝 -Z 抵消了 180°,直接用 HeadingDeg。
        targetRot = Quaternion.Euler(PitchDeg, HeadingDeg, RollDeg);

        if (usingCesiumCoordinates && cesiumAnchor != null)
        {
            Vector3 cesiumLocalTargetPos = unshiftedTargetPos;
            double3 localPosition = new double3(
                cesiumLocalTargetPos.x,
                cesiumLocalTargetPos.y,
                cesiumLocalTargetPos.z);
            double3 ecefPosition = math.mul(
                cesiumStartLocalToEcef,
                new double4(localPosition, 1.0)).xyz;
            float deltaPitchDeg = Mathf.DeltaAngle(originPitchDeg, PitchDeg);
            float deltaHeadingDeg = Mathf.DeltaAngle(originHeadingDeg, HeadingDeg);
            float deltaRollDeg = Mathf.DeltaAngle(originRollDeg, RollDeg);
            Quaternion cesiumRotation = sceneStartRot
                                        * Quaternion.Euler(deltaPitchDeg, deltaHeadingDeg, deltaRollDeg);

            targetCesiumEcefPosition = ecefPosition;
            targetCesiumRotation = cesiumRotation;
            hasCesiumTarget = true;
        }

        if (logState)
            Debug.Log(string.Format("[JSBSim] alt={0:F0}ft spd={1:F0}kt hdg={2:F0} pitch={3:F1} roll={4:F1}",
                AltitudeFt, SpeedKts, HeadingDeg, PitchDeg, RollDeg));
    }

    /// <summary>
    /// 将 JSBSim 的相对位移和临时高度校准量转换为 Unity 目标位置。
    /// </summary>
    private static Vector3 CalculateTargetPosition(
        Vector3 sceneStartPosition,
        Vector3 horizontalOffset,
        float verticalOffsetM,
        float altitudeOffsetM)
    {
        return sceneStartPosition
               + horizontalOffset
               + new Vector3(0f, verticalOffsetM + altitudeOffsetM, 0f);
    }

    // ====================== TCP 控制发送 ======================

    private void StartTcpControl()
    {
        if (tcpConnectThread != null && tcpConnectThread.IsAlive) return;
        tcpConnectThread = new Thread(ConnectTcpLoop) { IsBackground = true };
        tcpConnectThread.Start();
    }

    private void ConnectTcpLoop()
    {
        while (running)
        {
            TcpClient connectedClient = null;
            NetworkStream connectedStream = null;

            try
            {
                connectedClient = new TcpClient(AddressFamily.InterNetwork);
                connectedClient.NoDelay = true;
                connectedClient.Connect(controlHost, controlTcpPort);
                connectedStream = connectedClient.GetStream();

                lock (tcpLock)
                {
                    if (!running) return;
                    tcpClient = connectedClient;
                    tcpStream = connectedStream;
                    controlConnected = true;
                }

                Debug.Log("[JSBSim] 控制通道已连接 TCP " + controlHost + ":" + controlTcpPort);
                SendPauseCommand(jsbsimPauseRequested);
                byte[] buffer = new byte[1024];
                while (running)
                {
                    int count = connectedStream.Read(buffer, 0, buffer.Length);
                    if (count <= 0) break;
                }
            }
            catch (Exception e)
            {
                if (running && controlConnected)
                    Debug.LogWarning("[JSBSim] TCP 控制通道断开: " + e.Message);
            }
            finally
            {
                lock (tcpLock)
                {
                    if (ReferenceEquals(tcpStream, connectedStream))
                    {
                        tcpStream = null;
                        tcpClient = null;
                        controlConnected = false;
                    }
                }

                try { connectedStream?.Close(); } catch { }
                try { connectedClient?.Close(); } catch { }
            }

            if (running) Thread.Sleep(1000);
        }
    }

    /// <summary>设置 JSBSim 的某个属性(归一化控制量等)。线程安全。</summary>
    private void UpdateJsbsimPauseRequest()
    {
#if UNITY_EDITOR
        bool shouldPause = editorPaused || timeScalePaused || applicationPaused;
#else
        bool shouldPause = timeScalePaused || applicationPaused;
#endif
        RequestJsbsimPause(shouldPause);
    }

    public void RequestJsbsimPause(bool pause)
    {
        jsbsimPauseRequested = pause;
        SendPauseCommand(pause);
    }

    private void SendPauseCommand(bool pause)
    {
        lock (tcpLock)
        {
            if (!controlConnected || tcpStream == null) return;

            try
            {
                byte[] command = Encoding.ASCII.GetBytes(pause ? "hold\n" : "resume\n");
                tcpStream.Write(command, 0, command.Length);
                tcpStream.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JSBSim] 发送暂停状态失败: " + e.Message);
                controlConnected = false;
                try { tcpStream?.Close(); } catch { }
                try { tcpClient?.Close(); } catch { }
            }
        }
    }
    public void SetProperty(string property, float value)
    {
        lock (tcpLock)
        {
            if (!controlConnected || tcpStream == null) return;

            try
            {
                string cmd = "set " + property + " " + value.ToString("F4", CultureInfo.InvariantCulture) + "\n";
                byte[] bytes = Encoding.ASCII.GetBytes(cmd);
                tcpStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JSBSim] 发送控制失败: " + e.Message);
                controlConnected = false;
                try { tcpStream?.Close(); } catch { }
                try { tcpClient?.Close(); } catch { }
            }
        }
    }

    // ====================== 清理 ======================

    private void StopAll()
    {
        running = false;
        try { udpClient?.Close(); } catch { }
        udpClient = null;

        lock (tcpLock)
        {
            controlConnected = false;
            try { tcpStream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            tcpClient = null;
            tcpStream = null;
        }
    }
}

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
    private static readonly RaycastHit[] GroundRaycastHits = new RaycastHit[64];

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

    [Header("地面防穿模")]
    [Tooltip("开启后,JSBSim 给出的可视位置如果低于 Unity 跑道/地面 Collider,会被夹到地面上方。只修正 Unity 显示高度,不改 JSBSim 飞行动力学。")]
    [SerializeField] private bool preventGroundClipping = true;
    [Tooltip("用于地面检测的 Layer。默认检测所有可被 Physics Raycast 命中的层。")]
    [SerializeField] private LayerMask groundCollisionMask = Physics.DefaultRaycastLayers;
    [Tooltip("从飞机目标位置上方多高处向下探测地面。")]
    [SerializeField] private float groundRaycastStartHeight = 120f;
    [Tooltip("地面向下探测的最大距离。")]
    [SerializeField] private float groundRaycastDistance = 250f;
    [Tooltip("飞机根节点必须保持在地面 Collider 上方的最小高度。按当前 B737 模型根节点到最低可见几何约 6.3m 设置,避免机体/轮胎视觉穿入跑道。")]
    [SerializeField] private float groundClearanceMeters = 6.35f;
    [Tooltip("防穿地足迹采样半宽/半长。用于在俯仰或滚转时保护机头、机尾和两侧低点不穿过跑道。")]
    [SerializeField] private Vector2 groundProbeFootprintHalfExtentsMeters = new Vector2(18f, 22f);
    [Tooltip("防穿地足迹机尾方向(+Z)半长。机尾比机头更容易在抬头接地时穿地，因此单独加长。")]
    [SerializeField] private float groundProbeTailExtentMeters = 34f;

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
    public float SideSlipDeg { get; private set; }
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
    private readonly byte[] udpReceiveBuffer = new byte[65535];
    private string[] labels;
    private float[] parsedValues = Array.Empty<float>();
    private bool[] parsedValueValid = Array.Empty<bool>();
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
    private Vector3[] groundProbeLocalPoints;
    private float cachedGroundProbeClearance = -1f;
    private Vector2 cachedGroundProbeHalfExtents = new Vector2(-1f, -1f);
    private float cachedGroundProbeTailExtent = -1f;

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
            UdpClient receiverOwner = udpClient;
            Socket receiver = receiverOwner.Client;
            udpThread = new Thread(() => UdpLoop(receiverOwner, receiver)) { IsBackground = true };
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

    private void UdpLoop(UdpClient receiverOwner, Socket receiver)
    {
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (running && ReferenceEquals(receiverOwner, udpClient))
        {
            try
            {
                int byteCount = receiver.ReceiveFrom(
                    udpReceiveBuffer,
                    0,
                    udpReceiveBuffer.Length,
                    SocketFlags.None,
                    ref remote);
                ParsePacket(udpReceiveBuffer, byteCount);
            }
            catch (SocketException)
            {
                // 关闭时会抛异常,正常退出
                if (!running || !ReferenceEquals(receiverOwner, udpClient)) break;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JSBSim] UDP 接收异常: " + e.Message);
            }
        }
    }

    private void ParsePacket(byte[] buffer, int byteCount)
    {
        if (buffer == null || byteCount <= 0)
        {
            return;
        }

        int packetEnd = Math.Min(buffer.Length, byteCount);
        int lineStart = 0;
        for (int i = 0; i <= packetEnd; i++)
        {
            if (i < packetEnd && buffer[i] != (byte)'\n')
            {
                continue;
            }

            ParsePacketLine(buffer, lineStart, i - lineStart);
            lineStart = i + 1;
        }
    }

    private void ParsePacketLine(byte[] buffer, int start, int length)
    {
        TrimAsciiWhitespace(buffer, ref start, ref length);
        if (length <= 0)
        {
            return;
        }

        const string labelsPrefix = "<LABELS>";
        if (StartsWithAscii(buffer, start, length, labelsPrefix))
        {
            start += labelsPrefix.Length;
            length -= labelsPrefix.Length;
            TrimAsciiWhitespace(buffer, ref start, ref length);
            if (length > 0 && buffer[start] == (byte)',')
            {
                start++;
                length--;
            }
            ParseLabels(buffer, start, length);
            return;
        }

        string[] currentLabels = labels;
        if (currentLabels == null || currentLabels.Length == 0)
        {
            return;
        }

        int fieldCount = JsbsimAsciiParser.ParseValues(
            buffer,
            start,
            length,
            parsedValues,
            parsedValueValid);
        if (fieldCount != currentLabels.Length)
        {
            return;
        }

        lock (stateLock)
        {
            for (int i = 0; i < currentLabels.Length; i++)
            {
                if (parsedValueValid[i])
                {
                    latest[currentLabels[i]] = parsedValues[i];
                }
            }
            newStateAvailable = true;
        }
    }

    private void ParseLabels(byte[] buffer, int start, int length)
    {
        TrimAsciiWhitespace(buffer, ref start, ref length);
        if (length <= 0)
        {
            return;
        }

        int fieldCount = 1;
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            if (buffer[i] == (byte)',')
            {
                fieldCount++;
            }
        }

        string[] nextLabels = new string[fieldCount];
        int fieldIndex = 0;
        int tokenStart = start;
        for (int i = start; i <= end; i++)
        {
            if (i < end && buffer[i] != (byte)',')
            {
                continue;
            }

            int tokenLength = i - tokenStart;
            TrimAsciiWhitespace(buffer, ref tokenStart, ref tokenLength);
            nextLabels[fieldIndex++] = Encoding.ASCII.GetString(buffer, tokenStart, tokenLength);
            tokenStart = i + 1;
        }

        labels = nextLabels;
        parsedValues = new float[fieldCount];
        parsedValueValid = new bool[fieldCount];
    }

    private static bool StartsWithAscii(byte[] buffer, int start, int length, string expected)
    {
        if (length < expected.Length)
        {
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (buffer[start + i] != (byte)expected[i])
            {
                return false;
            }
        }
        return true;
    }

    private static void TrimAsciiWhitespace(byte[] buffer, ref int start, ref int length)
    {
        int end = start + length;
        while (start < end && IsAsciiWhitespace(buffer[start]))
        {
            start++;
        }
        while (end > start && IsAsciiWhitespace(buffer[end - 1]))
        {
            end--;
        }
        length = end - start;
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
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
            Vector3 nextPosition;
            Quaternion nextRotation;
            if (firstPose || smoothing <= 0f)
            {
                nextPosition = targetPos;
                nextRotation = targetRot;
                firstPose = false;
            }
            else
            {
                float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
                nextPosition = Vector3.Lerp(aircraft.position, targetPos, t);
                nextRotation = Quaternion.Slerp(aircraft.rotation, targetRot, t);
            }

            aircraft.position = ClampPositionAboveGround(
                nextPosition,
                preventGroundClipping,
                groundCollisionMask,
                groundRaycastStartHeight,
                groundRaycastDistance,
                groundClearanceMeters,
                nextRotation,
                aircraft,
                GetGroundProbeLocalPoints());
            aircraft.rotation = nextRotation;
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

            currentCesiumEcefPosition = ClampCesiumEcefPositionAboveGround(currentCesiumEcefPosition);
            ApplyCesiumPose(currentCesiumEcefPosition, currentCesiumRotation);
            firstPose = false;
        }
    }

    private double3 ClampCesiumEcefPositionAboveGround(double3 ecefPosition)
    {
        if (!preventGroundClipping || aircraft == null)
            return ecefPosition;

        double4x4 ecefToStartLocal = math.inverse(cesiumStartLocalToEcef);
        double3 localPosition = math.mul(ecefToStartLocal, new double4(ecefPosition, 1.0)).xyz;
        Vector3 localVector = new Vector3(
            (float)localPosition.x,
            (float)localPosition.y,
            (float)localPosition.z);
        Vector3 worldPosition = aircraft.parent != null
            ? aircraft.parent.TransformPoint(localVector)
            : localVector;
        Vector3 clampedWorldPosition = ClampPositionAboveGround(
            worldPosition,
            preventGroundClipping,
            groundCollisionMask,
            groundRaycastStartHeight,
            groundRaycastDistance,
            groundClearanceMeters,
            currentCesiumRotation,
            aircraft,
            GetGroundProbeLocalPoints());

        if (clampedWorldPosition == worldPosition)
            return ecefPosition;

        Vector3 clampedLocalVector = aircraft.parent != null
            ? aircraft.parent.InverseTransformPoint(clampedWorldPosition)
            : clampedWorldPosition;
        double3 clampedLocalPosition = new double3(
            clampedLocalVector.x,
            clampedLocalVector.y,
            clampedLocalVector.z);
        return math.mul(cesiumStartLocalToEcef, new double4(clampedLocalPosition, 1.0)).xyz;
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
        SideSlipDeg = Get("aero_beta_deg");
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

        targetRot = Quaternion.Euler(PitchDeg, HeadingDeg, RollDeg);
        Vector3 unshiftedTargetPos = CalculateTargetPosition(
            sceneStartPos,
            horizontalOffset,
            verticalOffsetM,
            altitudeOffset);
        Vector3 visualTargetPos = unshiftedTargetPos + accumulatedOriginShift;
        targetPos = ClampPositionAboveGround(
            visualTargetPos,
            preventGroundClipping,
            groundCollisionMask,
            groundRaycastStartHeight,
            groundRaycastDistance,
            groundClearanceMeters,
            targetRot,
            aircraft,
            GetGroundProbeLocalPoints());

        // ---- 姿态:NED 欧拉角 -> Unity 四元数 ----
        // 位移已翻转(north→-Z, east→-X),等价于把世界坐标系绕 Y 轴镜像 180°,
        // X 轴和 Z 轴方向都反了,所以绕这两个轴的旋转符号要翻转。
        // PitchDeg(绕 X 轴):X 轴翻了,符号反 → +PitchDeg。
        // RollDeg(绕 Z 轴):Z 轴翻了,但 JSBSim phi>0 是右滚,Unity 绕 +Z 是左滚,
        //   两个翻转抵消,符号仍为 -RollDeg。
        // HeadingDeg:Y 轴未变,模型自身机头朝 -Z 抵消了 180°,直接用 HeadingDeg。
        if (usingCesiumCoordinates && cesiumAnchor != null)
        {
            float deltaPitchDeg = Mathf.DeltaAngle(originPitchDeg, PitchDeg);
            float deltaHeadingDeg = Mathf.DeltaAngle(originHeadingDeg, HeadingDeg);
            float deltaRollDeg = Mathf.DeltaAngle(originRollDeg, RollDeg);
            Quaternion cesiumRotation = sceneStartRot
                                        * Quaternion.Euler(deltaPitchDeg, deltaHeadingDeg, deltaRollDeg);
            Vector3 cesiumWorldTargetPos = aircraft.parent != null
                ? aircraft.parent.TransformPoint(unshiftedTargetPos)
                : unshiftedTargetPos;
            Vector3 clampedCesiumWorldTargetPos = ClampPositionAboveGround(
                cesiumWorldTargetPos,
                preventGroundClipping,
                groundCollisionMask,
                groundRaycastStartHeight,
                groundRaycastDistance,
                groundClearanceMeters,
                cesiumRotation,
                aircraft,
                GetGroundProbeLocalPoints());
            Vector3 cesiumLocalTargetPos = aircraft.parent != null
                ? aircraft.parent.InverseTransformPoint(clampedCesiumWorldTargetPos)
                : clampedCesiumWorldTargetPos;
            double3 localPosition = new double3(
                cesiumLocalTargetPos.x,
                cesiumLocalTargetPos.y,
                cesiumLocalTargetPos.z);
            double3 ecefPosition = math.mul(
                cesiumStartLocalToEcef,
                new double4(localPosition, 1.0)).xyz;

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

    private Vector3[] GetGroundProbeLocalPoints()
    {
        float bottomY = -Mathf.Max(0f, groundClearanceMeters);
        Vector2 extents = new Vector2(
            Mathf.Max(0f, groundProbeFootprintHalfExtentsMeters.x),
            Mathf.Max(0f, groundProbeFootprintHalfExtentsMeters.y));
        float tailExtent = Mathf.Max(extents.y, groundProbeTailExtentMeters);

        if (groundProbeLocalPoints != null &&
            Mathf.Approximately(cachedGroundProbeClearance, bottomY) &&
            Mathf.Approximately(cachedGroundProbeHalfExtents.x, extents.x) &&
            Mathf.Approximately(cachedGroundProbeHalfExtents.y, extents.y) &&
            Mathf.Approximately(cachedGroundProbeTailExtent, tailExtent))
        {
            return groundProbeLocalPoints;
        }

        cachedGroundProbeClearance = bottomY;
        cachedGroundProbeHalfExtents = extents;
        cachedGroundProbeTailExtent = tailExtent;
        groundProbeLocalPoints = new[]
        {
            new Vector3(0f, bottomY, 0f),
            new Vector3(-extents.x, bottomY, 0f),
            new Vector3(extents.x, bottomY, 0f),
            new Vector3(0f, bottomY, -extents.y),
            new Vector3(0f, bottomY, tailExtent),
            new Vector3(-extents.x, bottomY, -extents.y),
            new Vector3(extents.x, bottomY, -extents.y),
            new Vector3(-extents.x, bottomY, tailExtent),
            new Vector3(extents.x, bottomY, tailExtent)
        };

        return groundProbeLocalPoints;
    }

    private static Vector3 ClampPositionAboveGround(
        Vector3 desiredPosition,
        bool preventGroundClipping,
        int groundCollisionMask,
        float groundRaycastStartHeight,
        float groundRaycastDistance,
        float groundClearanceMeters,
        Transform ignoredRoot)
    {
        return ClampPositionAboveGround(
            desiredPosition,
            preventGroundClipping,
            groundCollisionMask,
            groundRaycastStartHeight,
            groundRaycastDistance,
            groundClearanceMeters,
            Quaternion.identity,
            ignoredRoot,
            null);
    }

    private static Vector3 ClampPositionAboveGround(
        Vector3 desiredPosition,
        bool preventGroundClipping,
        int groundCollisionMask,
        float groundRaycastStartHeight,
        float groundRaycastDistance,
        float groundClearanceMeters,
        Quaternion desiredRotation,
        Transform ignoredRoot,
        Vector3[] localGroundProbePoints)
    {
        if (!preventGroundClipping)
            return desiredPosition;

        if (groundRaycastStartHeight <= 0f || groundRaycastDistance <= 0f)
            return desiredPosition;

        float clampedRootY = desiredPosition.y;
        if (TryFindHighestGroundY(
            desiredPosition,
            groundCollisionMask,
            groundRaycastStartHeight,
            groundRaycastDistance,
            ignoredRoot,
            out float rootGroundY))
        {
            clampedRootY = Mathf.Max(
                clampedRootY,
                rootGroundY + Mathf.Max(0f, groundClearanceMeters));
        }

        const float defaultSurfaceMarginMeters = 0.05f;
        if (localGroundProbePoints != null)
        {
            for (int index = 0; index < localGroundProbePoints.Length; index++)
            {
                Vector3 rotatedLocalProbe = desiredRotation * localGroundProbePoints[index];
                Vector3 probeWorldPosition = desiredPosition + rotatedLocalProbe;
                if (!TryFindHighestGroundY(
                    probeWorldPosition,
                    groundCollisionMask,
                    groundRaycastStartHeight,
                    groundRaycastDistance,
                    ignoredRoot,
                    out float probeGroundY))
                {
                    continue;
                }

                float minimumProbeY = probeGroundY + defaultSurfaceMarginMeters;
                float requiredRootY = desiredPosition.y +
                    (minimumProbeY - probeWorldPosition.y);
                clampedRootY = Mathf.Max(clampedRootY, requiredRootY);
            }
        }

        if (desiredPosition.y >= clampedRootY)
            return desiredPosition;

        desiredPosition.y = clampedRootY;
        return desiredPosition;
    }

    private static bool TryFindHighestGroundY(
        Vector3 probeWorldPosition,
        int groundCollisionMask,
        float groundRaycastStartHeight,
        float groundRaycastDistance,
        Transform ignoredRoot,
        out float highestGroundY)
    {
        Vector3 rayOrigin = probeWorldPosition + Vector3.up * groundRaycastStartHeight;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            GroundRaycastHits,
            groundRaycastDistance,
            groundCollisionMask,
            QueryTriggerInteraction.Ignore);

        highestGroundY = float.NegativeInfinity;
        if (hitCount <= 0)
            return false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = GroundRaycastHits[i].collider;
            if (collider == null)
                continue;

            if (ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot))
                continue;

            if (GroundRaycastHits[i].point.y > highestGroundY)
                highestGroundY = GroundRaycastHits[i].point.y;
        }

        return !float.IsNegativeInfinity(highestGroundY);
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

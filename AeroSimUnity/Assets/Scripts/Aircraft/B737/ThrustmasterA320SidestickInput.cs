using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 通过 Windows 多媒体摇杆接口读取图马思特 TCA/A320 侧杆。
/// 不依赖 Unity InputManager，轴、POV 帽和按钮数据均可实时查看。
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class ThrustmasterA320SidestickInput : MonoBehaviour
{
    public enum DeviceAxis
    {
        [InspectorName("X 轴")] X,
        [InspectorName("Y 轴")] Y,
        [InspectorName("Z 轴")] Z,
        [InspectorName("R 轴")] R,
        [InspectorName("U 轴")] U,
        [InspectorName("V 轴")] V
    }

    [Serializable]
    public sealed class AxisMapping
    {
        [InspectorName("读取轴")]
        public DeviceAxis axis;

        [InspectorName("反向")]
        public bool invert;

        [InspectorName("死区")]
        [Range(0f, 0.5f)]
        public float deadzone = 0.03f;

        [InspectorName("中心偏移")]
        [Range(-0.5f, 0.5f)]
        public float centerOffset;

        [InspectorName("响应曲线指数")]
        [Range(0.2f, 3f)]
        public float responseExponent = 1f;

        [InspectorName("灵敏度")]
        [Range(0.1f, 2f)]
        public float sensitivity = 1f;

        public AxisMapping(DeviceAxis axis, bool invert)
        {
            this.axis = axis;
            this.invert = invert;
        }
    }

    [Header("设备连接")]
    [InspectorName("启用侧杆输入")]
    [SerializeField] private bool inputEnabled = true;

    [Tooltip("可用 |、逗号或分号分隔多个关键词；匹配任意一个即可。留空表示选择第一个控制器。")]
    [InspectorName("设备名称关键词")]
    [SerializeField] private string preferredDeviceKeywords = "TCA|A320|Thrustmaster|图马思特";

    [Tooltip("格式为十六进制 VID:PID。当前图马思特 A320 侧杆是 044F:0406；可用 | 分隔多个设备。")]
    [InspectorName("首选硬件 ID")]
    [SerializeField] private string preferredHardwareIds = "044F:0406";

    [Tooltip("没有匹配名称时，如果只接入了一个控制器，则自动使用该设备。")]
    [InspectorName("单设备时自动连接")]
    [SerializeField] private bool useOnlyConnectedDeviceAsFallback = true;

    [InspectorName("断线重连间隔（秒）")]
    [SerializeField, Min(0.1f)] private float reconnectInterval = 1f;

    [Header("输入过滤")]
    [Tooltip("发送给飞机的轴和油门按此步进截断。0.1 表示只保留一位小数并忽略第二位小数。")]
    [InspectorName("输出数值步进")]
    [SerializeField, Range(0.01f, 0.5f)] private float inputValueStep = 0.1f;

    [Header("飞行轴映射")]
    [InspectorName("横滚（左右）")]
    [SerializeField] private AxisMapping roll = new AxisMapping(DeviceAxis.X, false)
    {
        centerOffset = 0.03f
    };

    [InspectorName("俯仰（前后）")]
    [SerializeField] private AxisMapping pitch = new AxisMapping(DeviceAxis.Y, true);

    [InspectorName("方向舵（扭转）")]
    [SerializeField] private AxisMapping yaw = new AxisMapping(DeviceAxis.R, false);

    [Header("油门轴映射")]
    [InspectorName("使用侧杆油门")]
    [SerializeField] private bool throttleEnabled = true;

    [InspectorName("油门")]
    [SerializeField] private AxisMapping throttle = new AxisMapping(DeviceAxis.Z, true);

    private const uint JoyReturnAll = 0x000000FF;
    private const uint JoyPovCentered = 0x0000FFFF;
    private const uint JoyErrorNoError = 0;

    private readonly List<string> detectedDeviceNames = new List<string>();
    private JoyCaps capabilities;
    private JoyInfoEx state;
    private uint connectedDeviceId;
    private float nextReconnectTime;
    private bool connected;
    private float outputRoll;
    private float outputPitch;
    private float outputYaw;
    private float outputThrottle;
    private string connectionMessage = "尚未扫描设备";

    public bool InputEnabled => inputEnabled;
    public bool IsConnected => connected;
    public bool ControlActive => inputEnabled && connected;
    public bool ThrottleControlEnabled => ControlActive && throttleEnabled;
    public string ConnectedDeviceName => connected ? capabilities.productName : string.Empty;
    public string ConnectionMessage => connectionMessage;
    public uint ConnectedDeviceId => connectedDeviceId;
    public ushort ManufacturerId => capabilities.manufacturerId;
    public ushort ProductId => capabilities.productId;
    public int AxisCount => connected ? (int)capabilities.axisCount : 0;
    public int ButtonCount => connected ? Mathf.Clamp((int)capabilities.buttonCount, 0, 32) : 0;
    public IReadOnlyList<string> DetectedDeviceNames => detectedDeviceNames;

    public uint RawX => state.x;
    public uint RawY => state.y;
    public uint RawZ => state.z;
    public uint RawR => state.r;
    public uint RawU => state.u;
    public uint RawV => state.v;
    public uint RawButtons => state.buttons;
    public int PovDegrees => state.pov == JoyPovCentered ? -1 : Mathf.RoundToInt(state.pov / 100f);
    public Vector2 PovLookDirection => ConvertPovToLookDirection(PovDegrees);

    public float NormalizedX => NormalizeRawAxis(DeviceAxis.X);
    public float NormalizedY => NormalizeRawAxis(DeviceAxis.Y);
    public float NormalizedZ => NormalizeRawAxis(DeviceAxis.Z);
    public float NormalizedR => NormalizeRawAxis(DeviceAxis.R);
    public float NormalizedU => NormalizeRawAxis(DeviceAxis.U);
    public float NormalizedV => NormalizeRawAxis(DeviceAxis.V);

    public float Roll => outputRoll;
    public float Pitch => outputPitch;
    public float Yaw => outputYaw;
    public float Throttle => outputThrottle;

    private void OnEnable()
    {
        nextReconnectTime = 0f;
        RescanDevices();
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            connected = false;
            connectionMessage = "侧杆输入已关闭";
            return;
        }

        if (!connected)
        {
            if (Time.unscaledTime >= nextReconnectTime)
            {
                RescanDevices();
                nextReconnectTime = Time.unscaledTime + reconnectInterval;
            }
            return;
        }

        if (!TryReadState(connectedDeviceId, out state))
        {
            string disconnectedName = capabilities.productName;
            connected = false;
            connectionMessage = "设备已断开，正在等待重连";
            nextReconnectTime = Time.unscaledTime + reconnectInterval;
            Debug.LogWarning($"[侧杆输入] 设备已断开：{disconnectedName}", this);
        }
        else
        {
            UpdateOutputValues();
        }
    }

    [ContextMenu("重新扫描侧杆设备")]
    public void RescanDevices()
    {
        detectedDeviceNames.Clear();

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        uint deviceSlots = joyGetNumDevs();
        var availableDevices = new List<DeviceCandidate>();
        uint capsSize = (uint)Marshal.SizeOf(typeof(JoyCaps));

        for (uint id = 0; id < deviceSlots; id++)
        {
            var caps = new JoyCaps();
            if (joyGetDevCaps(new UIntPtr(id), ref caps, capsSize) != JoyErrorNoError)
                continue;
            if (!TryReadState(id, out JoyInfoEx candidateState))
                continue;

            string name = string.IsNullOrWhiteSpace(caps.productName)
                ? $"游戏控制器 {id}"
                : caps.productName.Trim();
            caps.productName = name;
            detectedDeviceNames.Add($"{id}: {name}");
            availableDevices.Add(new DeviceCandidate(id, caps, candidateState));
        }

        int selectedIndex = FindPreferredDevice(availableDevices);
        if (selectedIndex < 0 && useOnlyConnectedDeviceAsFallback && availableDevices.Count == 1)
            selectedIndex = 0;

        if (selectedIndex < 0)
        {
            connected = false;
            connectionMessage = availableDevices.Count == 0
                ? "未检测到 Windows 游戏控制器"
                : "检测到控制器，但名称未匹配；请修改设备名称关键词";
            return;
        }

        DeviceCandidate selected = availableDevices[selectedIndex];
        bool changedDevice = !connected || connectedDeviceId != selected.id;
        connectedDeviceId = selected.id;
        capabilities = selected.capabilities;
        state = selected.state;
        connected = inputEnabled;
        connectionMessage = connected ? "设备已连接" : "侧杆输入已关闭";
        UpdateOutputValues();

        if (changedDevice && connected)
        {
            Debug.Log(
                $"[侧杆输入] 已连接 {capabilities.productName} " +
                $"(设备 {connectedDeviceId}, VID {ManufacturerId:X4}, PID {ProductId:X4}, " +
                $"轴 {AxisCount}, 按钮 {ButtonCount})",
                this);
        }
#else
        connected = false;
        connectionMessage = "当前平台不支持 Windows 侧杆接口";
#endif
    }

    public bool GetButton(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= 32)
            return false;
        return (state.buttons & (1u << buttonIndex)) != 0;
    }

    /// <summary>
    /// Windows POV 角度：0 上、90 右、180 下、270 左。斜向角度会同时输出两个方向。
    /// </summary>
    internal static Vector2 ConvertPovToLookDirection(int povDegrees)
    {
        if (povDegrees < 0)
            return Vector2.zero;

        float radians = povDegrees * Mathf.Deg2Rad;
        float horizontal = Mathf.Sin(radians);
        float vertical = Mathf.Cos(radians);
        if (Mathf.Abs(horizontal) < 0.0001f) horizontal = 0f;
        if (Mathf.Abs(vertical) < 0.0001f) vertical = 0f;
        return new Vector2(horizontal, vertical);
    }

    private int FindPreferredDevice(List<DeviceCandidate> devices)
    {
        for (int deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
        {
            JoyCaps caps = devices[deviceIndex].capabilities;
            if (MatchesPreferredHardwareId(caps.manufacturerId, caps.productId))
                return deviceIndex;
        }

        if (string.IsNullOrWhiteSpace(preferredDeviceKeywords))
            return devices.Count > 0 ? 0 : -1;

        string[] keywords = preferredDeviceKeywords.Split(new[] { '|', ',', ';', '，', '；' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
        {
            string deviceName = devices[deviceIndex].capabilities.productName;
            for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
            {
                string keyword = keywords[keywordIndex].Trim();
                if (keyword.Length > 0 && deviceName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return deviceIndex;
            }
        }
        return -1;
    }

    private bool MatchesPreferredHardwareId(ushort manufacturerId, ushort productId)
    {
        if (string.IsNullOrWhiteSpace(preferredHardwareIds))
            return false;

        string[] ids = preferredHardwareIds.Split(new[] { '|', ',', ';', '，', '；' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < ids.Length; index++)
        {
            string[] pair = ids[index].Trim().Split(':');
            if (pair.Length != 2)
                continue;
            if (ushort.TryParse(pair[0], System.Globalization.NumberStyles.HexNumber, null, out ushort vid) &&
                ushort.TryParse(pair[1], System.Globalization.NumberStyles.HexNumber, null, out ushort pid) &&
                vid == manufacturerId && pid == productId)
            {
                return true;
            }
        }
        return false;
    }

    private float EvaluateMapping(AxisMapping mapping)
    {
        if (mapping == null)
            return 0f;

        float value = NormalizeRawAxis(mapping.axis);
        value = Mathf.Clamp(value + mapping.centerOffset, -1f, 1f);
        if (mapping.invert)
            value = -value;
        return ApplyAxisResponse(value, mapping.deadzone, mapping.responseExponent, mapping.sensitivity);
    }

    private void UpdateOutputValues()
    {
        outputRoll = DiscardRemainder(EvaluateMapping(roll), inputValueStep);
        outputPitch = DiscardRemainder(EvaluateMapping(pitch), inputValueStep);
        outputYaw = DiscardRemainder(EvaluateMapping(yaw), inputValueStep);
        float throttleValue = Mathf.Clamp01((EvaluateMapping(throttle) + 1f) * 0.5f);
        outputThrottle = Mathf.Clamp01(DiscardRemainder(throttleValue, inputValueStep));
    }

    internal static float DiscardRemainder(float value, float step)
    {
        step = Mathf.Max(0.000001f, step);
        if (Mathf.Approximately(value, 0f))
            return 0f;

        float scaled = value / step;
        int wholeSteps = scaled > 0f
            ? Mathf.FloorToInt(scaled + 0.000001f)
            : Mathf.CeilToInt(scaled - 0.000001f);
        return wholeSteps * step;
    }

    private float NormalizeRawAxis(DeviceAxis axis)
    {
        GetAxisData(axis, out uint raw, out uint min, out uint max);
        if (max <= min)
            return 0f;
        return Mathf.Clamp((raw - min) / (float)(max - min) * 2f - 1f, -1f, 1f);
    }

    private void GetAxisData(DeviceAxis axis, out uint raw, out uint min, out uint max)
    {
        switch (axis)
        {
            case DeviceAxis.Y:
                raw = state.y; min = capabilities.yMin; max = capabilities.yMax; return;
            case DeviceAxis.Z:
                raw = state.z; min = capabilities.zMin; max = capabilities.zMax; return;
            case DeviceAxis.R:
                raw = state.r; min = capabilities.rMin; max = capabilities.rMax; return;
            case DeviceAxis.U:
                raw = state.u; min = capabilities.uMin; max = capabilities.uMax; return;
            case DeviceAxis.V:
                raw = state.v; min = capabilities.vMin; max = capabilities.vMax; return;
            default:
                raw = state.x; min = capabilities.xMin; max = capabilities.xMax; return;
        }
    }

    internal static float ApplyAxisResponse(float value, float deadzone, float exponent, float sensitivity)
    {
        value = Mathf.Clamp(value, -1f, 1f);
        deadzone = Mathf.Clamp(deadzone, 0f, 0.99f);
        float magnitude = Mathf.Abs(value);
        if (magnitude <= deadzone)
            return 0f;

        float remapped = (magnitude - deadzone) / (1f - deadzone);
        float curved = Mathf.Pow(remapped, Mathf.Max(0.01f, exponent));
        return Mathf.Clamp(Mathf.Sign(value) * curved * Mathf.Max(0f, sensitivity), -1f, 1f);
    }

    private static bool TryReadState(uint deviceId, out JoyInfoEx result)
    {
        result = new JoyInfoEx
        {
            size = (uint)Marshal.SizeOf(typeof(JoyInfoEx)),
            flags = JoyReturnAll
        };

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        return joyGetPosEx(deviceId, ref result) == JoyErrorNoError;
#else
        return false;
#endif
    }

    private readonly struct DeviceCandidate
    {
        public readonly uint id;
        public readonly JoyCaps capabilities;
        public readonly JoyInfoEx state;

        public DeviceCandidate(uint id, JoyCaps capabilities, JoyInfoEx state)
        {
            this.id = id;
            this.capabilities = capabilities;
            this.state = state;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort manufacturerId;
        public ushort productId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string productName;
        public uint xMin;
        public uint xMax;
        public uint yMin;
        public uint yMax;
        public uint zMin;
        public uint zMax;
        public uint buttonCount;
        public uint periodMin;
        public uint periodMax;
        public uint rMin;
        public uint rMax;
        public uint uMin;
        public uint uMax;
        public uint vMin;
        public uint vMax;
        public uint capabilities;
        public uint maxAxisCount;
        public uint axisCount;
        public uint maxButtonCount;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string registryKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string oemDriver;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint size;
        public uint flags;
        public uint x;
        public uint y;
        public uint z;
        public uint r;
        public uint u;
        public uint v;
        public uint buttons;
        public uint buttonNumber;
        public uint pov;
        public uint reserved1;
        public uint reserved2;
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [DllImport("winmm.dll", EntryPoint = "joyGetNumDevs")]
    private static extern uint joyGetNumDevs();

    [DllImport("winmm.dll", EntryPoint = "joyGetDevCapsW", CharSet = CharSet.Unicode)]
    private static extern uint joyGetDevCaps(UIntPtr joystickId, ref JoyCaps capabilities, uint size);

    [DllImport("winmm.dll", EntryPoint = "joyGetPosEx")]
    private static extern uint joyGetPosEx(uint joystickId, ref JoyInfoEx info);
#endif
}

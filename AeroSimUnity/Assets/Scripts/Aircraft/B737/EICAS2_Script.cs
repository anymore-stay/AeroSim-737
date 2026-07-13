using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class EICAS2_Script : MonoBehaviour
{
    [Header("Bridge")]
    [Tooltip("JSBSim 数据桥。留空时会自动使用 JsbsimBridge.Instance。")]
    [SerializeField] private JsbsimBridge bridge;
    private JsbsimBridge subscribedBridge;

    [Tooltip("开启后即使 OnStateUpdated 事件没有触发，也会按固定间隔主动读取 JSBSim 数据刷新仪表。")]
    [SerializeField] private bool pollBridgeInUpdate = true;

    [Tooltip("主动轮询刷新间隔，单位秒。0.05 表示约 20Hz。")]
    [SerializeField, Min(0.01f)] private float pollInterval = 0.05f;
    private float nextPollTime;
    private bool refreshPending;
    private int n2LeftDisplay = int.MinValue;
    private int n2RightDisplay = int.MinValue;
    private int fuelFlowLeftDisplay = int.MinValue;
    private int fuelFlowRightDisplay = int.MinValue;
    private int oilPressLeftDisplay = int.MinValue;
    private int oilPressRightDisplay = int.MinValue;
    private int oilTempLeftDisplay = int.MinValue;
    private int oilTempRightDisplay = int.MinValue;
    private int oilQtyLeftDisplay = int.MinValue;
    private int oilQtyRightDisplay = int.MinValue;
    private int vibrationLeftDisplay = int.MinValue;
    private int vibrationRightDisplay = int.MinValue;

    [Header("Gauges")]
    [SerializeField] private EicasGauge n2LeftGauge;
    [SerializeField] private EicasGauge n2RightGauge;

    [Header("Texts")]
    [SerializeField] private Text n2LeftText;
    [SerializeField] private Text n2RightText;
    [SerializeField] private Text fuelFlowLeftText;
    [SerializeField] private Text fuelFlowRightText;
    [SerializeField] private Text oilPressLeftText;
    [SerializeField] private Text oilPressRightText;
    [SerializeField] private Text oilTempLeftText;
    [SerializeField] private Text oilTempRightText;
    [SerializeField] private Text oilQtyLeftText;
    [SerializeField] private Text oilQtyRightText;
    [SerializeField] private Text vibrationLeftText;
    [SerializeField] private Text vibrationRightText;

    [Header("JSBSim Keys - Engine")]
    [SerializeField] private string engine1N2Key = "propulsion_engine_n2";
    [SerializeField] private string engine2N2Key = "propulsion_engine_1_n2";
    [SerializeField] private string engine1FuelFlowPpsKey = "propulsion_engine_fuel_flow_rate_pps";
    [SerializeField] private string engine2FuelFlowPpsKey = "propulsion_engine_1_fuel_flow_rate_pps";

    [Header("JSBSim Keys - Oil / Vibration")]
    [Tooltip("左发油压 key。当前 JSBSim catalog 没有时留空，显示默认值。")]
    [SerializeField] private string engine1OilPressureKey = "";
    [Tooltip("右发油压 key。当前 JSBSim catalog 没有时留空，显示默认值。")]
    [SerializeField] private string engine2OilPressureKey = "";
    [SerializeField] private string engine1OilTempKey = "";
    [SerializeField] private string engine2OilTempKey = "";
    [SerializeField] private string engine1OilQtyKey = "";
    [SerializeField] private string engine2OilQtyKey = "";
    [SerializeField] private string engine1VibrationKey = "";
    [SerializeField] private string engine2VibrationKey = "";

    [Header("Fallback Values")]
    [SerializeField] private float defaultOilPressure = 78f;
    [SerializeField] private float defaultOilTemp = 17f;
    [SerializeField] private float defaultOilQty = 17f;
    [SerializeField] private float defaultVibration = 0f;

    private void Awake()
    {
        AutoBind();
    }

    private void OnEnable()
    {
        AttachBridge();
        Refresh();
    }

    private void OnDisable()
    {
        if (subscribedBridge != null)
        {
            subscribedBridge.OnStateUpdated -= RequestRefresh;
            subscribedBridge = null;
        }
    }

    private void Update()
    {
        if (bridge == null)
        {
            AttachBridge();
        }

        if (bridge != null
            && Time.unscaledTime >= nextPollTime
            && (pollBridgeInUpdate || refreshPending))
        {
            nextPollTime = Time.unscaledTime + pollInterval;
            refreshPending = false;
            Refresh();
        }
    }

    private void AttachBridge()
    {
        JsbsimBridge nextBridge = bridge != null ? bridge : JsbsimBridge.Instance;
        if (nextBridge == null)
        {
            nextBridge = FindObjectOfType<JsbsimBridge>();
        }

        if (nextBridge == null || nextBridge == subscribedBridge)
        {
            return;
        }

        if (subscribedBridge != null)
        {
            subscribedBridge.OnStateUpdated -= RequestRefresh;
        }

        bridge = nextBridge;
        subscribedBridge = nextBridge;
        subscribedBridge.OnStateUpdated += RequestRefresh;
        refreshPending = true;
    }

    private void RequestRefresh()
    {
        refreshPending = true;
    }

    private void Refresh()
    {
        if (bridge == null)
        {
            return;
        }

        float n2Left = Read(engine1N2Key, 0f);
        float n2Right = Read(engine2N2Key, 0f);
        SetGauge(n2LeftGauge, n2Left);
        SetGauge(n2RightGauge, n2Right);
        SetNumericText(n2LeftText, n2Left, 1, ref n2LeftDisplay);
        SetNumericText(n2RightText, n2Right, 1, ref n2RightDisplay);

        SetNumericText(fuelFlowLeftText, Read(engine1FuelFlowPpsKey, 0f) * 3.6f, 2, ref fuelFlowLeftDisplay);
        SetNumericText(fuelFlowRightText, Read(engine2FuelFlowPpsKey, 0f) * 3.6f, 2, ref fuelFlowRightDisplay);
        SetNumericText(oilPressLeftText, Read(engine1OilPressureKey, defaultOilPressure), 0, ref oilPressLeftDisplay);
        SetNumericText(oilPressRightText, Read(engine2OilPressureKey, defaultOilPressure), 0, ref oilPressRightDisplay);
        SetNumericText(oilTempLeftText, Read(engine1OilTempKey, defaultOilTemp), 0, ref oilTempLeftDisplay);
        SetNumericText(oilTempRightText, Read(engine2OilTempKey, defaultOilTemp), 0, ref oilTempRightDisplay);
        SetNumericText(oilQtyLeftText, Read(engine1OilQtyKey, defaultOilQty), 0, ref oilQtyLeftDisplay);
        SetNumericText(oilQtyRightText, Read(engine2OilQtyKey, defaultOilQty), 0, ref oilQtyRightDisplay);
        SetNumericText(vibrationLeftText, Read(engine1VibrationKey, defaultVibration), 1, ref vibrationLeftDisplay);
        SetNumericText(vibrationRightText, Read(engine2VibrationKey, defaultVibration), 1, ref vibrationRightDisplay);
    }

    private float Read(string key, float fallback)
    {
        if (bridge != null && !string.IsNullOrWhiteSpace(key) && bridge.TryGetValue(key, out float value))
        {
            return value;
        }

        return fallback;
    }

    private static void SetGauge(EicasGauge gauge, float value)
    {
        if (gauge != null)
        {
            gauge.SetValue(value);
        }
    }

    private static void SetNumericText(Text text, float value, int decimals, ref int cachedValue)
    {
        if (text == null)
        {
            return;
        }

        int scale = decimals == 0 ? 1 : decimals == 1 ? 10 : 100;
        int displayedValue = Mathf.RoundToInt(value * scale);
        if (displayedValue == cachedValue)
        {
            return;
        }

        cachedValue = displayedValue;
        string format = decimals == 0 ? "0" : decimals == 1 ? "0.0" : "0.00";
        text.text = (displayedValue / (float)scale).ToString(format, CultureInfo.InvariantCulture);
    }

    private void AutoBind()
    {
        if (n2LeftGauge == null) n2LeftGauge = FindComponent<EicasGauge>("N2_Left_GaugeFill");
        if (n2RightGauge == null) n2RightGauge = FindComponent<EicasGauge>("N2_Right_GaugeFill");
        if (n2LeftText == null) n2LeftText = FindComponent<Text>("N2_Left_Value");
        if (n2RightText == null) n2RightText = FindComponent<Text>("N2_Right_Value");
        if (fuelFlowLeftText == null) fuelFlowLeftText = FindComponent<Text>("FF_Left_Value");
        if (fuelFlowRightText == null) fuelFlowRightText = FindComponent<Text>("FF_Right_Value");
        if (oilPressLeftText == null) oilPressLeftText = FindComponent<Text>("OilPRESS_Left_Value");
        if (oilPressRightText == null) oilPressRightText = FindComponent<Text>("OilPRESS_Right_Value");
        if (oilTempLeftText == null) oilTempLeftText = FindComponent<Text>("OilTemp_Left_Value");
        if (oilTempRightText == null) oilTempRightText = FindComponent<Text>("OilTemp_Right_Value");
        if (oilQtyLeftText == null) oilQtyLeftText = FindComponent<Text>("OilQty_Left_Value");
        if (oilQtyRightText == null) oilQtyRightText = FindComponent<Text>("OilQty_Right_Value");
        if (vibrationLeftText == null) vibrationLeftText = FindComponent<Text>("Vib_Left_Value");
        if (vibrationRightText == null) vibrationRightText = FindComponent<Text>("Vib_Right_Value");
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        GameObject target = FindObject(objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private GameObject FindObject(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
            {
                return children[i].gameObject;
            }
        }

        return null;
    }
}

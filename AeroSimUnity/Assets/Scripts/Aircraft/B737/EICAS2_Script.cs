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
            subscribedBridge.OnStateUpdated -= Refresh;
            subscribedBridge = null;
        }
    }

    private void Update()
    {
        if (bridge == null)
        {
            AttachBridge();
        }

        if (pollBridgeInUpdate && bridge != null && Time.unscaledTime >= nextPollTime)
        {
            nextPollTime = Time.unscaledTime + pollInterval;
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
            subscribedBridge.OnStateUpdated -= Refresh;
        }

        bridge = nextBridge;
        subscribedBridge = nextBridge;
        subscribedBridge.OnStateUpdated += Refresh;
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
        SetText(n2LeftText, n2Left.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(n2RightText, n2Right.ToString("0.0", CultureInfo.InvariantCulture));

        SetText(fuelFlowLeftText, FormatFuelFlow(Read(engine1FuelFlowPpsKey, 0f)));
        SetText(fuelFlowRightText, FormatFuelFlow(Read(engine2FuelFlowPpsKey, 0f)));
        SetText(oilPressLeftText, Read(engine1OilPressureKey, defaultOilPressure).ToString("0", CultureInfo.InvariantCulture));
        SetText(oilPressRightText, Read(engine2OilPressureKey, defaultOilPressure).ToString("0", CultureInfo.InvariantCulture));
        SetText(oilTempLeftText, Read(engine1OilTempKey, defaultOilTemp).ToString("0", CultureInfo.InvariantCulture));
        SetText(oilTempRightText, Read(engine2OilTempKey, defaultOilTemp).ToString("0", CultureInfo.InvariantCulture));
        SetText(oilQtyLeftText, Read(engine1OilQtyKey, defaultOilQty).ToString("0", CultureInfo.InvariantCulture));
        SetText(oilQtyRightText, Read(engine2OilQtyKey, defaultOilQty).ToString("0", CultureInfo.InvariantCulture));
        SetText(vibrationLeftText, Read(engine1VibrationKey, defaultVibration).ToString("0.0", CultureInfo.InvariantCulture));
        SetText(vibrationRightText, Read(engine2VibrationKey, defaultVibration).ToString("0.0", CultureInfo.InvariantCulture));
    }

    private string FormatFuelFlow(float poundsPerSecond)
    {
        float thousandPoundsPerHour = poundsPerSecond * 3.6f;
        return thousandPoundsPerHour.ToString("0.00", CultureInfo.InvariantCulture);
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

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
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

using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class EICAS1_Script : MonoBehaviour
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

    [Header("Gauges")]
    [Tooltip("左发 N1 仪表。")]
    [SerializeField] private EicasGauge n1LeftGauge;
    [Tooltip("右发 N1 仪表。")]
    [SerializeField] private EicasGauge n1RightGauge;
    [Tooltip("左发 EGT 仪表。")]
    [SerializeField] private EicasGauge egtLeftGauge;
    [Tooltip("右发 EGT 仪表。")]
    [SerializeField] private EicasGauge egtRightGauge;

    [Header("Texts")]
    [SerializeField] private Text tatText;
    [SerializeField] private Text n1LeftText;
    [SerializeField] private Text n1RightText;
    [SerializeField] private Text egtLeftText;
    [SerializeField] private Text egtRightText;
    [SerializeField] private Text fuelFlowLeftText;
    [SerializeField] private Text fuelFlowRightText;
    [SerializeField] private Text fuelQtyText;
    [SerializeField] private Text fuelTotalText;

    [Header("JSBSim Keys - Engine")]
    [Tooltip("左发 N1 key。")]
    [SerializeField] private string engine1N1Key = "propulsion_engine_n1";
    [Tooltip("右发 N1 key。")]
    [SerializeField] private string engine2N1Key = "propulsion_engine_1_n1";
    [Tooltip("左发 EGT key。当前 catalog 没找到真实 EGT 时可留空，脚本会按 N1 临时推导。")]
    [SerializeField] private string engine1EgtKey = "";
    [Tooltip("右发 EGT key。当前 catalog 没找到真实 EGT 时可留空，脚本会按 N1 临时推导。")]
    [SerializeField] private string engine2EgtKey = "";
    [Tooltip("左发燃油流量 key，单位 lbs/sec。")]
    [SerializeField] private string engine1FuelFlowPpsKey = "propulsion_engine_fuel_flow_rate_pps";
    [Tooltip("右发燃油流量 key，单位 lbs/sec。")]
    [SerializeField] private string engine2FuelFlowPpsKey = "propulsion_engine_1_fuel_flow_rate_pps";
    [Tooltip("TAT 摄氏度 key。")]
    [SerializeField] private string tatCelsiusKey = "propulsion_tat_c";

    [Header("JSBSim Keys - Fuel")]
    [SerializeField] private string leftTankLbsKey = "propulsion_tank_contents_lbs";
    [SerializeField] private string centerTankLbsKey = "propulsion_tank_1_contents_lbs";
    [SerializeField] private string rightTankLbsKey = "propulsion_tank_2_contents_lbs";
    [SerializeField] private string totalFuelLbsKey = "propulsion_total_fuel_lbs";

    [Header("Derived Fallbacks")]
    [Tooltip("EGT key 缺失时，是否临时按 N1 推导 EGT。以后有真实 EGT key 后建议关闭。")]
    [SerializeField] private bool deriveEgtFromN1WhenMissing = true;
    [SerializeField] private float egtBaseC = 200f;
    [SerializeField] private float egtPerN1 = 11.8f;

    [Header("Warnings")]
    [SerializeField] private GameObject eng1StartValveWarning;
    [SerializeField] private GameObject eng2StartValveWarning;
    [SerializeField] private GameObject eng1LowOilPressureWarning;
    [SerializeField] private GameObject eng2LowOilPressureWarning;
    [SerializeField] private GameObject eng1FireBackground;
    [SerializeField] private GameObject eng1FireText;
    [SerializeField] private GameObject eng2FireBackground;
    [SerializeField] private GameObject eng2FireText;
    [SerializeField] private GameObject apuFireText;
    [SerializeField] private GameObject stallText;
    [SerializeField] private GameObject speedBrakeArmedText;
    [SerializeField] private GameObject parkingBrakeText;

    [Header("Warning Keys")]
    [Tooltip("Starter/Start Valve 指示 key。没有左右独立字段时两个发动机可共用。")]
    [SerializeField] private string engine1StartValveKey = "propulsion_starter_cmd";
    [SerializeField] private string engine2StartValveKey = "propulsion_starter_cmd";
    [Tooltip("油压 key。当前 catalog 没有时留空，LOW OIL PRESSURE 默认不显示。")]
    [SerializeField] private string engine1OilPressureKey = "";
    [SerializeField] private string engine2OilPressureKey = "";
    [Tooltip("发动机火警 key。当前 catalog 没有时留空，火警默认不显示。")]
    [SerializeField] private string engine1FireKey = "";
    [SerializeField] private string engine2FireKey = "";
    [SerializeField] private string apuFireKey = "";
    [SerializeField] private string stallWarningKey = "systems_stall_warn_norm";
    [SerializeField] private string speedBrakeKey = "fcs_speedbrake_pos_norm";
    [SerializeField] private string leftBrakeKey = "fcs_left_brake_cmd_norm";
    [SerializeField] private string rightBrakeKey = "fcs_right_brake_cmd_norm";

    [Header("Warning Thresholds")]
    [SerializeField] private float activeThreshold = 0.5f;
    [SerializeField] private float speedBrakeArmedThreshold = 0.05f;
    [SerializeField] private float lowOilPressureThreshold = 20f;

    private void Awake()
    {
        AutoBind();
        HideAllWarnings();
    }

    private void OnEnable()
    {
        AttachBridge();
        HideAllWarnings();
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

        float n1Left = Read(engine1N1Key, 0f);
        float n1Right = Read(engine2N1Key, 0f);
        SetGauge(n1LeftGauge, n1Left);
        SetGauge(n1RightGauge, n1Right);
        SetText(n1LeftText, n1Left.ToString("0.0", CultureInfo.InvariantCulture));
        SetText(n1RightText, n1Right.ToString("0.0", CultureInfo.InvariantCulture));

        float egtLeft = ReadEgt(engine1EgtKey, n1Left);
        float egtRight = ReadEgt(engine2EgtKey, n1Right);
        SetGauge(egtLeftGauge, egtLeft);
        SetGauge(egtRightGauge, egtRight);
        SetText(egtLeftText, egtLeft.ToString("0", CultureInfo.InvariantCulture));
        SetText(egtRightText, egtRight.ToString("0", CultureInfo.InvariantCulture));

        SetText(tatText, Mathf.RoundToInt(Read(tatCelsiusKey, 15f)).ToString(CultureInfo.InvariantCulture) + " C");
        SetText(fuelFlowLeftText, FormatFuelFlow(Read(engine1FuelFlowPpsKey, 0f)));
        SetText(fuelFlowRightText, FormatFuelFlow(Read(engine2FuelFlowPpsKey, 0f)));

        float leftFuel = Read(leftTankLbsKey, 0f);
        float centerFuel = Read(centerTankLbsKey, 0f);
        float rightFuel = Read(rightTankLbsKey, 0f);
        float totalFuel = Read(totalFuelLbsKey, leftFuel + centerFuel + rightFuel);

        SetText(fuelQtyText, string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1:0.00} {2:0.00}",
            leftFuel / 1000f, centerFuel / 1000f, rightFuel / 1000f));
        SetText(fuelTotalText, string.Format(CultureInfo.InvariantCulture, "{0:0.0}", totalFuel / 1000f));

        UpdateWarnings();
    }

    private void UpdateWarnings()
    {
        SetActive(eng1StartValveWarning, ReadBool(engine1StartValveKey));
        SetActive(eng2StartValveWarning, ReadBool(engine2StartValveKey));
        SetActive(eng1LowOilPressureWarning, TryRead(engine1OilPressureKey, out float oil1) && oil1 < lowOilPressureThreshold);
        SetActive(eng2LowOilPressureWarning, TryRead(engine2OilPressureKey, out float oil2) && oil2 < lowOilPressureThreshold);

        bool eng1Fire = ReadBool(engine1FireKey);
        bool eng2Fire = ReadBool(engine2FireKey);
        SetActive(eng1FireBackground, eng1Fire);
        SetActive(eng1FireText, eng1Fire);
        SetActive(eng2FireBackground, eng2Fire);
        SetActive(eng2FireText, eng2Fire);
        SetActive(apuFireText, ReadBool(apuFireKey));
        SetActive(stallText, ReadBool(stallWarningKey));
        SetActive(speedBrakeArmedText, Read(speedBrakeKey, 0f) > speedBrakeArmedThreshold);

        bool parkingBrake = Read(leftBrakeKey, 0f) > activeThreshold && Read(rightBrakeKey, 0f) > activeThreshold;
        SetActive(parkingBrakeText, parkingBrake);
    }

    private float ReadEgt(string key, float n1)
    {
        if (TryRead(key, out float egt))
        {
            return egt;
        }

        return deriveEgtFromN1WhenMissing ? egtBaseC + n1 * egtPerN1 : 0f;
    }

    private string FormatFuelFlow(float poundsPerSecond)
    {
        float thousandPoundsPerHour = poundsPerSecond * 3.6f;
        return thousandPoundsPerHour.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private void HideAllWarnings()
    {
        SetActive(eng1StartValveWarning, false);
        SetActive(eng2StartValveWarning, false);
        SetActive(eng1LowOilPressureWarning, false);
        SetActive(eng2LowOilPressureWarning, false);
        SetActive(eng1FireBackground, false);
        SetActive(eng1FireText, false);
        SetActive(eng2FireBackground, false);
        SetActive(eng2FireText, false);
        SetActive(apuFireText, false);
        SetActive(stallText, false);
        SetActive(speedBrakeArmedText, false);
        SetActive(parkingBrakeText, false);
    }

    private float Read(string key, float fallback)
    {
        return TryRead(key, out float value) ? value : fallback;
    }

    private bool ReadBool(string key)
    {
        return TryRead(key, out float value) && value > activeThreshold;
    }

    private bool TryRead(string key, out float value)
    {
        value = 0f;
        return bridge != null && !string.IsNullOrWhiteSpace(key) && bridge.TryGetValue(key, out value);
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
        if (text != null && text.text != value)
        {
            text.text = value;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void AutoBind()
    {
        if (n1LeftGauge == null) n1LeftGauge = FindComponent<EicasGauge>("N1_Left_GaugeFill");
        if (n1RightGauge == null) n1RightGauge = FindComponent<EicasGauge>("N1_Right_GaugeFill");
        if (egtLeftGauge == null) egtLeftGauge = FindComponent<EicasGauge>("EGT_Left_GaugeFill");
        if (egtRightGauge == null) egtRightGauge = FindComponent<EicasGauge>("EGT_Right_GaugeFill");
        if (tatText == null) tatText = FindComponent<Text>("TAT_Value");
        if (n1LeftText == null) n1LeftText = FindComponent<Text>("N1_Left_Value");
        if (n1RightText == null) n1RightText = FindComponent<Text>("N1_Right_Value");
        if (egtLeftText == null) egtLeftText = FindComponent<Text>("EGT_Left_Value");
        if (egtRightText == null) egtRightText = FindComponent<Text>("EGT_Right_Value");
        if (fuelFlowLeftText == null) fuelFlowLeftText = FindComponent<Text>("FF_Left_Value");
        if (fuelFlowRightText == null) fuelFlowRightText = FindComponent<Text>("FF_Right_Value");
        if (fuelQtyText == null) fuelQtyText = FindComponent<Text>("Fuel_Qty_Value");
        if (fuelTotalText == null) fuelTotalText = FindComponent<Text>("Fuel_Total_Value");

        if (eng1StartValveWarning == null) eng1StartValveWarning = FindObject("ENG1_START_VALVE");
        if (eng2StartValveWarning == null) eng2StartValveWarning = FindObject("ENG2_START_VALVE");
        if (eng1LowOilPressureWarning == null) eng1LowOilPressureWarning = FindObject("ENG1_LOW_OIL_PRESSURE");
        if (eng2LowOilPressureWarning == null) eng2LowOilPressureWarning = FindObject("ENG2_LOW_OIL_PRESSURE");
        if (eng1FireBackground == null) eng1FireBackground = FindObject("ENG1_FIRE_Background");
        if (eng1FireText == null) eng1FireText = FindObject("ENG1_FIRE_Text");
        if (eng2FireBackground == null) eng2FireBackground = FindObject("ENG2_FIRE_Background");
        if (eng2FireText == null) eng2FireText = FindObject("ENG2_FIRE_Text");
        if (apuFireText == null) apuFireText = FindObject("APU_FIRE_Text");
        if (stallText == null) stallText = FindObject("STALL_Text");
        if (speedBrakeArmedText == null) speedBrakeArmedText = FindObject("SPEED_BRAKE_ARMED_Text");
        if (parkingBrakeText == null) parkingBrakeText = FindObject("PARKING_BRAKE_Text");
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

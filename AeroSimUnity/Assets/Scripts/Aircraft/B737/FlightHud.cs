using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 飞行 HUD,用 uGUI Canvas(Screen Space - Overlay)在左上角显示飞行数据与控制按键。
/// 独立脚本,运行时自动创建 Canvas/文本,挂在任意 GameObject 上即可,不依赖相机。
///
/// 关键点:如果相机渲染到非默认 Display,Overlay Canvas 默认输出到 Display 1,
/// 两者不一致就会导致"画面在,HUD 却看不见"。所以 Canvas 的 targetDisplay 跟随当前激活相机。
///
/// 按 Tab 键显示 / 隐藏 HUD。Overlay 模式下三个相机视角都显示同一套 HUD。
/// </summary>
public class FlightHud : MonoBehaviour
{
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private FlightInput input;
    [SerializeField] private int fontSize = 12;
    [SerializeField] private Color color = Color.white;
    [Tooltip("显示/隐藏 HUD 的按键。")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("启动时是否显示 HUD。")]
    [SerializeField] private bool visibleOnStart = true;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;

    private Text label;
    private RectTransform panelRt;
    private GameObject panelGo;
    private Canvas canvas;
    private bool hudVisible;
    private float nextRefreshTime;
    private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

    private void Awake()
    {
        if (bridge == null) bridge = GetComponent<JsbsimBridge>();
        if (input == null) input = GetComponent<FlightInput>();
        BuildUi();
        hudVisible = visibleOnStart;
        panelGo.SetActive(hudVisible);
    }

    /// <summary>运行时构建 Canvas + 半透明背景 + 文本。面板高度随文字内容自适应,任何分辨率都完整可见。</summary>
    private void BuildUi()
    {
        // ---- Canvas(屏幕空间叠加,永远盖在画面最上层,与相机无关)----
        var canvasGo = new GameObject("FlightHudCanvas");
        canvasGo.transform.SetParent(null);
        DontDestroyOnLoad(canvasGo);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // 尽量靠前,盖住其他 UI
        // 关键:相机可能渲染到非默认显示器,
        // Overlay Canvas 必须输出到同一个 Display 才看得见,否则 HUD 跑到别的显示器上。
        canvas.targetDisplay = GetActiveCameraDisplay();
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ---- 容器(无背景,纯透明;仅用于 Tab 显隐和定位,左上角)----
        panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(8f, -8f);
        panelRt.sizeDelta = new Vector2(360f, 100f); // 高度在 Update 里随文字调整

        // ---- 文本(铺满容器,带内边距)----
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panelGo.transform, false);
        label = textGo.AddComponent<Text>();
        // 优先用微软雅黑(Windows 含中文字形),回退黑体/宋体/Arial
        label.font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "SimSun", "Arial" }, fontSize);
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        // 无背景框,加黑色描边让文字在亮天空/地面上都清晰可读
        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        var textRt = label.rectTransform;
        // 铺满父面板,四边留 8px 内边距
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 8f);
        textRt.offsetMax = new Vector2(-8f, -8f);
    }

    private void Update()
    {
        if (label == null) return;

        // Tab 显示/隐藏 HUD(隐藏时仍持续监听,可随时切回)
        if (Input.GetKeyDown(toggleKey))
        {
            hudVisible = !hudVisible;
            panelGo.SetActive(hudVisible);
        }
        if (!hudVisible) return; // 隐藏时跳过文字刷新,省开销

        // 切换相机后,确保 HUD 仍输出到当前画面所在的显示器
        if (canvas != null)
        {
            int d = GetActiveCameraDisplay();
            if (canvas.targetDisplay != d) canvas.targetDisplay = d;
        }

        string status = bridge != null && bridge.HasState ? "已连接" : "等待 JSBSim 数据...";
        string ctrl = bridge != null && bridge.ControlConnected ? "控制: 已连接" : "控制: 未连接";

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        sb.Length = 0;
        sb.AppendLine("JSBSim 飞行仿真  [" + status + "]");
        sb.AppendLine(ctrl);
        if (bridge != null && bridge.HasState)
        {
            sb.AppendLine(string.Format("空速  : {0,6:F1} kt", bridge.SpeedKts));
            sb.AppendLine(string.Format("真空速: {0,6:F1} kt", bridge.TrueSpeedKts));
            sb.AppendLine(string.Format("海拔  : {0,6:F0} ft", bridge.AltitudeFt));
            sb.AppendLine(string.Format("离地  : {0,6:F0} ft", bridge.AglFt));
            sb.AppendLine(string.Format("升降率: {0,6:F0} fpm", bridge.VerticalSpeedFps * 60f));
            sb.AppendLine(string.Format("航向  : {0,6:F0} °", NormalizeHeading(bridge.HeadingDeg)));
            sb.AppendLine(string.Format("俯仰  : {0,6:F1} °", bridge.PitchDeg));
            sb.AppendLine(string.Format("滚转  : {0,6:F1} °", bridge.RollDeg));
            sb.AppendLine(string.Format("转速  : {0,6:F0} rpm", bridge.Rpm));
        }
        if (input != null)
        {
            sb.AppendLine("---- 控制 ----");
            sb.AppendLine(string.Format("油门  : {0,5:P0}", input.Throttle));
            sb.AppendLine(string.Format("升降舵: {0,5:F2}", input.Elevator));
            sb.AppendLine(string.Format("副翼  : {0,5:F2}", input.Aileron));
            sb.AppendLine(string.Format("方向舵: {0,5:F2}", input.Rudder));
            sb.AppendLine(string.Format("襟翼  : {0}/{1}  {2,4:P0}", input.FlapStep, input.FlapStepCount, input.Flaps));
            sb.AppendLine(string.Format("扰流板: {0}/{1}  {2,4:P0}", input.SpoilerStep, input.SpoilerStepCount, input.Spoilers));
            sb.AppendLine(string.Format("起落架: {0}", input.GearDown ? "已放下" : "已收起"));
            sb.AppendLine(string.Format("刹车  : {0}", input.Brakes ? "已锁定 (按 B 松)" : "已松开"));
        }
        sb.AppendLine("---- 飞行按键 ----");
        sb.AppendLine("W/S 俯仰   A/D 滚转   Q/E 偏航");
        sb.AppendLine("Shift/Ctrl 油门加减");
        sb.AppendLine("F/V 襟翼增减  R/T 扰流板增减");
        sb.AppendLine("G 起落架收放  B 刹车开关  Esc 暂停");
        sb.AppendLine("---- 相机按键 ----");
        sb.AppendLine("Shift+7 客舱  Shift+8 驾驶舱  Shift+9 第三人称");
        sb.AppendLine("1 操纵杆显示/隐藏（仅驾驶舱）");
        sb.AppendLine("2 打开天气和时间选择");
        sb.AppendLine("右键拖动 转视角   方向键 移动视角");
        sb.AppendLine("滚轮 第三人称缩放");
        sb.AppendLine("---- 起飞提示 ----");
        sb.AppendLine("按住 Shift 推油门 → 滑跑到约150kt → 按 S 抬轮");
        sb.AppendLine("---- 显示 ----");
        sb.AppendLine("Tab 显示 / 隐藏本面板");

        label.text = sb.ToString();

        // 面板高度随文字内容自适应(文字 preferredHeight + 上下内边距),保证完整显示
        if (panelRt != null)
        {
            float h = label.preferredHeight + 16f;
            panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, h);
        }
    }

    private float NormalizeHeading(float deg)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }

    /// <summary>取当前激活相机的 targetDisplay,让 HUD 输出到和画面相同的显示器。</summary>
    private int GetActiveCameraDisplay()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var cams = Camera.allCameras; // 只含 enabled 的相机
            if (cams.Length > 0) cam = cams[0];
        }
        return cam != null ? cam.targetDisplay : 0;
    }
}

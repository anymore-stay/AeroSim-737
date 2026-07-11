using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class B737FmsDisplay : MonoBehaviour
{
    private enum FmsPage
    {
        Index,
        Status,
        RouteMenu,
        Database,
        ArrData
    }

    [Header("Bridge")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private bool pollBridgeInUpdate = true;
    [SerializeField, Min(0.01f)] private float pollInterval = 0.1f;

    [Header("Layout")]
    [SerializeField] private Vector2 displaySize = new Vector2(768f, 1024f);
    [SerializeField] private bool showLiveDataStatus;

    [Header("Navigation")]
    [SerializeField] private FmsPage initialPage = FmsPage.Index;

    private const string GeneratedRootName = "FMS_UI_Generated";
    private const string CyanHex = "#27E6D8";
    private const string WhiteHex = "#F1F5F1";

    private Text titleText;
    private Text pageText;
    private Text leftStatusText;
    private Text rightRouteText;
    private Text rightDatabaseText;
    private Text rightArrDataText;
    private Text bottomMessageText;
    private Text liveStatusText;
    private Font displayFont;
    private float nextPollTime;
    private JsbsimBridge subscribedBridge;
    private FmsPage currentPage = FmsPage.Index;
    private bool isRebuilding;

    private static readonly Color32 BackgroundColor = new Color32(0, 0, 0, 255);
    private static readonly Color32 WhiteTextColor = new Color32(242, 246, 242, 255);
    private static readonly Color32 CyanTextColor = new Color32(39, 230, 216, 255);

    private void Awake()
    {
        currentPage = initialPage;
        if (!ShouldSuppressEditorRebuild())
        {
            RebuildDisplay();
        }
    }

    private void OnValidate()
    {
        displaySize = new Vector2(Mathf.Max(1f, displaySize.x), Mathf.Max(1f, displaySize.y));
        // Prefab 反序列化早期不重建 UI，避免生成游离的 Prefab 根节点。
        if (ShouldSuppressEditorRebuild() || !gameObject.scene.IsValid())
        {
            return;
        }

        if (isActiveAndEnabled)
        {
            RebuildDisplay();
        }
    }

    private void OnEnable()
    {
        if (ShouldSuppressEditorRebuild())
        {
            return;
        }

        RebuildDisplay();

        if (Application.isPlaying)
        {
            AttachBridge();
        }

        Refresh();
    }

    private bool ShouldSuppressEditorRebuild()
    {
#if UNITY_EDITOR
        return B737FmsDisplayRig.SuppressEditorRebuild
            || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene);
#else
        return false;
#endif
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
        if (!Application.isPlaying)
        {
            Refresh();
            return;
        }

        if (bridge == null)
        {
            AttachBridge();
        }

        if (pollBridgeInUpdate && Time.unscaledTime >= nextPollTime)
        {
            nextPollTime = Time.unscaledTime + pollInterval;
            Refresh();
        }
    }

    public void SetDisplaySize(Vector2 size)
    {
        Vector2 nextSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        if (displaySize == nextSize && transform.Find(GeneratedRootName) != null)
        {
            return;
        }

        displaySize = nextSize;
        RebuildDisplay();
    }

    public void ShowIndexPage()
    {
        SetPage(FmsPage.Index);
    }

    public void ShowStatusPage()
    {
        SetPage(FmsPage.Status);
    }

    public void ShowRouteMenuPage()
    {
        SetPage(FmsPage.RouteMenu);
    }

    public void ShowDatabasePage()
    {
        SetPage(FmsPage.Database);
    }

    public void ShowArrDataPage()
    {
        SetPage(FmsPage.ArrData);
    }

    public void ClearOrBack()
    {
        SetPage(FmsPage.Index);
    }

    public void PressLeftLine(int lineIndex)
    {
        if (!IsLeftLineActive(lineIndex))
        {
            return;
        }

        if (currentPage == FmsPage.Index && lineIndex == 1)
        {
            SetPage(FmsPage.Status);
        }
        else if (lineIndex == 1)
        {
            SetPage(FmsPage.Index);
        }
    }

    public void PressRightLine(int lineIndex)
    {
        if (!IsRightLineActive(lineIndex))
        {
            return;
        }

        if (currentPage == FmsPage.Index && lineIndex == 1)
        {
            SetPage(FmsPage.RouteMenu);
        }
        else if (currentPage == FmsPage.Index && lineIndex == 2)
        {
            SetPage(FmsPage.Database);
        }
        else if (currentPage == FmsPage.Index && lineIndex == 5)
        {
            SetPage(FmsPage.ArrData);
        }
        else if (currentPage != FmsPage.Index)
        {
            bottomMessageText.text = ColorizeBracketedMessage("FUNCTION NOT IMPLEMENTED ");
        }
    }

    public void RebuildDisplay()
    {
        if (isRebuilding)
        {
            return;
        }

        isRebuilding = true;
        try
        {
            displayFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Cascadia Mono", "Bahnschrift", "Microsoft YaHei", "Arial" }, 24);

            RectTransform rootTransform = GetComponent<RectTransform>();
            rootTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rootTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rootTransform.pivot = new Vector2(0.5f, 0.5f);
            rootTransform.anchoredPosition = Vector2.zero;
            rootTransform.sizeDelta = displaySize;

            Transform oldRoot = transform.Find(GeneratedRootName);
            if (oldRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(oldRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(oldRoot.gameObject);
                }
            }

            GameObject generatedRoot = new GameObject(GeneratedRootName, typeof(RectTransform));
            generatedRoot.layer = gameObject.layer;
            generatedRoot.transform.SetParent(transform, false);
            RectTransform generatedRect = generatedRoot.GetComponent<RectTransform>();
            generatedRect.anchorMin = Vector2.zero;
            generatedRect.anchorMax = Vector2.one;
            generatedRect.offsetMin = Vector2.zero;
            generatedRect.offsetMax = Vector2.zero;

            AddPanel(generatedRect, "Background", Vector2.zero, displaySize, BackgroundColor);

            titleText = AddCduText(generatedRect, "Title", "INDEX", 0.5f, 0.085f, 0.36f, 0.1f, 44, CyanTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            pageText = AddCduText(generatedRect, "Page", "1/1", 0.78f, 0.085f, 0.22f, 0.1f, 44, CyanTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            leftStatusText = AddCduText(generatedRect, "L1_Status", "<STATUS", 0.26f, 0.22f, 0.4f, 0.11f, 44, WhiteTextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            rightRouteText = AddCduText(generatedRect, "R1_RouteMenu", "ROUTE MENU>", 0.7f, 0.22f, 0.46f, 0.11f, 44, WhiteTextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            rightDatabaseText = AddCduText(generatedRect, "R2_Database", "DATABASE>", 0.74f, 0.345f, 0.38f, 0.11f, 44, WhiteTextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            rightArrDataText = AddCduText(generatedRect, "R5_ArrData", "ARR DATA>", 0.74f, 0.67f, 0.38f, 0.11f, 44, WhiteTextColor, TextAnchor.MiddleRight, FontStyle.Bold);

            liveStatusText = AddCduText(generatedRect, "LiveStatus", "", 0.5f, 0.77f, 0.82f, 0.08f, 34, CyanTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            bottomMessageText = AddCduText(generatedRect, "BottomMessage", "", 0.5f, 0.89f, 0.84f, 0.11f, 42, WhiteTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            Refresh();
        }
        finally
        {
            isRebuilding = false;
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

    private void SetPage(FmsPage page)
    {
        if (currentPage == page)
        {
            return;
        }

        currentPage = page;
        Refresh();
    }

    private bool IsLeftLineActive(int lineIndex)
    {
        if (lineIndex != 1)
        {
            return false;
        }

        return !string.IsNullOrEmpty(leftStatusText != null ? leftStatusText.text : null);
    }

    private bool IsRightLineActive(int lineIndex)
    {
        switch (currentPage)
        {
            case FmsPage.Index:
                return lineIndex == 1 || lineIndex == 2 || lineIndex == 5;
            case FmsPage.Status:
            case FmsPage.RouteMenu:
            case FmsPage.Database:
            case FmsPage.ArrData:
                return lineIndex == 1 || lineIndex == 2 || lineIndex == 5;
            default:
                return false;
        }
    }

    private void Refresh()
    {
        if (bottomMessageText == null)
        {
            return;
        }

        bool hasData = bridge != null && bridge.HasState;
        switch (currentPage)
        {
            case FmsPage.Status:
                RenderStatusPage(hasData);
                break;
            case FmsPage.RouteMenu:
                RenderRouteMenuPage();
                break;
            case FmsPage.Database:
                RenderDatabasePage();
                break;
            case FmsPage.ArrData:
                RenderArrDataPage();
                break;
            default:
                RenderIndexPage(hasData);
                break;
        }
    }

    private void RenderIndexPage(bool hasData)
    {
        titleText.text = "INDEX";
        pageText.text = "1/1";
        leftStatusText.text = "<STATUS";
        rightRouteText.text = "ROUTE MENU>";
        rightDatabaseText.text = "DATABASE>";
        rightArrDataText.text = "ARR DATA>";

        if (showLiveDataStatus && hasData)
        {
            float speed = bridge.SpeedKts;
            float heading = bridge.HeadingDeg;
            float altitude = bridge.AltitudeFt;
            liveStatusText.text = string.Format(CultureInfo.InvariantCulture, "HDG {0:000}   IAS {1:0}KT   ALT {2:0}FT", Normalize360(heading), speed, altitude);
            bottomMessageText.text = ColorizeBracketedMessage("JSBSIM DATA ACTIVE ");
            return;
        }

        liveStatusText.text = string.Empty;
        bottomMessageText.text = ColorizeBracketedMessage("NAV DATA OUT OF DATE ");
    }

    private void RenderStatusPage(bool hasData)
    {
        titleText.text = "STATUS";
        pageText.text = "1/1";
        leftStatusText.text = "<INDEX";
        rightRouteText.text = hasData ? "JSBSIM OK>" : "NO JSBSIM>";
        rightDatabaseText.text = "AIRCRAFT>";
        rightArrDataText.text = string.Empty;
        liveStatusText.text = hasData ? "B737 DATA LINK ACTIVE" : "B737 DATA LINK STANDBY";
        bottomMessageText.text = hasData ? ColorizeBracketedMessage("FMS STATUS NORMAL ") : ColorizeBracketedMessage("CONNECT JSBSIM ");
    }

    private void RenderRouteMenuPage()
    {
        titleText.text = "ROUTE MENU";
        pageText.text = "1/1";
        leftStatusText.text = "<INDEX";
        rightRouteText.text = "RTE 1>";
        rightDatabaseText.text = "RTE 2>";
        rightArrDataText.text = string.Empty;
        liveStatusText.text = "ORIGIN ----   DEST ----";
        bottomMessageText.text = ColorizeBracketedMessage("ROUTE NOT ENTERED ");
    }

    private void RenderDatabasePage()
    {
        titleText.text = "DATABASE";
        pageText.text = "1/1";
        leftStatusText.text = "<INDEX";
        rightRouteText.text = "NAV DATA>";
        rightDatabaseText.text = "UPDATE>";
        rightArrDataText.text = string.Empty;
        liveStatusText.text = "AIRAC CYCLE ----";
        bottomMessageText.text = ColorizeBracketedMessage("NAV DATA OUT OF DATE ");
    }

    private void RenderArrDataPage()
    {
        titleText.text = "ARR DATA";
        pageText.text = "1/1";
        leftStatusText.text = "<INDEX";
        rightRouteText.text = "ARRIVAL>";
        rightDatabaseText.text = "APPROACH>";
        rightArrDataText.text = "RUNWAY>";
        liveStatusText.text = "DEST ----";
        bottomMessageText.text = ColorizeBracketedMessage("ARR DATA NOT SET ");
    }

    private static string ColorizeBracketedMessage(string message)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "<color={0}>[</color><color={1}>{2}</color><color={0}>]</color>",
            CyanHex,
            WhiteHex,
            message);
    }

    private static float Normalize360(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    private Vector2 PositionFromScreen(float normalizedX, float normalizedY)
    {
        return new Vector2(
            (normalizedX - 0.5f) * displaySize.x,
            (0.5f - normalizedY) * displaySize.y);
    }

    private Vector2 SizeFromScreen(float normalizedWidth, float normalizedHeight)
    {
        return new Vector2(
            Mathf.Max(1f, normalizedWidth * displaySize.x),
            Mathf.Max(1f, normalizedHeight * displaySize.y));
    }

    private Image AddPanel(RectTransform parent, string objectName, Vector2 position, Vector2 size, Color color)
    {
        GameObject panelGo = new GameObject(objectName, typeof(RectTransform));
        panelGo.layer = gameObject.layer;
        panelGo.transform.SetParent(parent, false);
        RectTransform rt = panelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image image = panelGo.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text AddCduText(RectTransform parent, string objectName, string text, float normalizedX, float normalizedY, float normalizedWidth, float normalizedHeight, int fontSize, Color color, TextAnchor alignment, FontStyle style)
    {
        return AddText(
            parent,
            objectName,
            text,
            PositionFromScreen(normalizedX, normalizedY),
            SizeFromScreen(normalizedWidth, normalizedHeight),
            fontSize,
            color,
            alignment,
            style);
    }

    private Text AddText(RectTransform parent, string objectName, string text, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment, FontStyle style)
    {
        GameObject textGo = new GameObject(objectName, typeof(RectTransform));
        textGo.layer = gameObject.layer;
        textGo.transform.SetParent(parent, false);
        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Text textComponent = textGo.AddComponent<Text>();
        textComponent.font = displayFont;
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.alignment = alignment;
        textComponent.fontStyle = style;
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        textComponent.supportRichText = true;
        textComponent.raycastTarget = false;

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1f, -1f);

        return textComponent;
    }
}

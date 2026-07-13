using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class FlightMapOverlay : MonoBehaviour
{
    private static FlightMapOverlay activeInstance;

    [Serializable]
    public class Waypoint
    {
        public string ident = "WPT";
        public double latitudeDeg;
        public double longitudeDeg;
    }

    private struct GeoPoint
    {
        public double LatitudeDeg;
        public double LongitudeDeg;

        public GeoPoint(double latitudeDeg, double longitudeDeg)
        {
            LatitudeDeg = latitudeDeg;
            LongitudeDeg = longitudeDeg;
        }
    }

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;
    [SerializeField] private bool visibleOnStart;
    [SerializeField] private bool allowMouseWheelRange = true;

    [Header("Data")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private Waypoint[] route;
    [SerializeField, Min(0.5f)] private float minimumRangeNm = 1.5f;
    [SerializeField, Min(10f)] private float defaultRangeNm = 40f;
    [SerializeField, Min(20f)] private float maximumRangeNm = 240f;
    [SerializeField] private bool autoFitRoute = true;
    [SerializeField, Range(0.5f, 0.95f)] private float routeFitFill = 0.72f;
    [SerializeField] private bool keepAircraftCentered;

    [Header("Track")]
    [SerializeField] private bool recordTrack = true;
    [SerializeField, Min(2)] private int maxTrackPoints = 900;
    [SerializeField, Min(0.01f)] private float trackMinDistanceNm = 0.05f;

    [Header("Window")]
    [SerializeField, Min(260f)] private float mapSize = 520f;
    [SerializeField, Min(260f)] private float minMapSize = 320f;
    [SerializeField, Min(420f)] private float maxMapSize = 920f;
    [SerializeField] private Vector2 screenOffset = new Vector2(-24f, -24f);
    [SerializeField] private int fontSize = 15;
    [SerializeField] private int labelFontSize = 13;
    [SerializeField] private int sortingOrder = 32755;

    [Header("Fallback Texture")]
    [SerializeField] private Texture2D mapBackgroundTexture;
    [SerializeField, Min(0.1f)] private float backgroundTextureRangeNm = 180f;
    [SerializeField] private bool generateFallbackMapTexture = true;

    [Header("Cesium Scene Basemap")]
    [SerializeField] private bool useCesiumSceneBasemap = true;
    [SerializeField, Range(256, 2048)] private int cesiumMapTextureSize = 1024;
    [SerializeField, Min(300f)] private float cesiumMinimumCameraHeightMeters = 1200f;
    [SerializeField, Min(1000f)] private float cesiumFarPaddingMeters = 12000f;
    [SerializeField] private LayerMask cesiumSceneLayerMask = ~0;
    [SerializeField] private Color cesiumImageTint = new Color(0.82f, 0.90f, 0.82f, 1f);
    [SerializeField] private Color cesiumMapColorWash = new Color(0.58f, 0.72f, 0.58f, 0.22f);
    [SerializeField] private Color cesiumMapClearColor = new Color(0.60f, 0.68f, 0.62f, 1f);
    [SerializeField] private string cesiumAttribution = "Cesium 3D Tiles";

    [Header("Online Tile Basemap")]
    [SerializeField] private bool useOnlineTileBasemap;
    [SerializeField] private string tileUrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    [SerializeField] private string tileUserAgent = "AeroSim-737-Unity/0.1 (https://github.com/anymore-stay/AeroSim-737)";
    [SerializeField] private string tileAttribution = "Map data (c) OpenStreetMap contributors";
    [SerializeField, Range(1, 19)] private int minTileZoom = 3;
    [SerializeField, Range(1, 19)] private int maxTileZoom = 15;
    [SerializeField, Min(128)] private int tileSizePx = 256;
    [SerializeField, Min(4)] private int maxVisibleTiles = 64;
    [SerializeField] private bool cacheTilesOnDisk = true;
    [SerializeField, Min(1)] private int tileCacheDays = 14;

    private const double EarthRadiusNm = 3440.065;
    private const double EarthRadiusMeters = 6378137.0;
    private const float FeetPerSecondToKnots = 0.5924838f;
    private const float HeaderHeight = 38f;
    private const float FooterHeight = 82f;
    private const float BorderResizeThickness = 12f;

    private readonly List<GeoPoint> trackPoints = new List<GeoPoint>(512);
    private readonly Vector2[] emptyRoutePoints = Array.Empty<Vector2>();
    private readonly System.Text.StringBuilder infoBuilder = new System.Text.StringBuilder(384);
    private readonly Dictionary<string, RawImage> tileImages = new Dictionary<string, RawImage>(64);
    private readonly Dictionary<string, Texture2D> tileTextures = new Dictionary<string, Texture2D>(128);
    private readonly HashSet<string> loadingTiles = new HashSet<string>();
    private readonly HashSet<string> visibleTileKeys = new HashSet<string>();

    private Canvas canvas;
    private RectTransform canvasRt;
    private RectTransform panelRt;
    private RectTransform dragHandleRt;
    private RectTransform mapPanHandleRt;
    private RectTransform resizeLeftRt;
    private RectTransform resizeRightRt;
    private RectTransform resizeTopRt;
    private RectTransform resizeBottomRt;
    private RectTransform mapViewportRt;
    private RectTransform tileRootRt;
    private RectTransform cesiumMapRt;
    private RectTransform cesiumColorWashRt;
    private RectTransform mapLabelRootRt;
    private RectTransform mapBackgroundRt;
    private RawImage mapBackgroundImage;
    private RawImage cesiumMapImage;
    private Image cesiumColorWashImage;
    private FlightMapGraphic mapGraphic;
    private Text headerText;
    private RectTransform statusPanelRt;
    private Text statusText;
    private Text infoText;
    private Text attributionText;
    private Font mapFont;
    private Texture2D generatedFallbackTexture;
    private Texture2D tilePlaceholderTexture;
    private RenderTexture cesiumMapTexture;
    private Camera cesiumMapCamera;
    private CesiumGeoreference cesiumGeoreference;
    private CesiumCameraManager cesiumCameraManager;
    private bool mapVisible;
    private bool cesiumSceneBasemapAvailable;
    private bool manualRangeOverride;
    private float manualRangeNm;
    private float currentRangeNm;
    private int currentTileZoom = -1;
    private float currentTileDisplayScale = 1f;
    private double currentCenterLatDeg;
    private double currentCenterLonDeg;
    private bool manualCenterOverride;
    private double manualCenterLatDeg;
    private double manualCenterLonDeg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeOverlay()
    {
        if (activeInstance != null || FindObjectOfType<FlightMapOverlay>() != null)
        {
            return;
        }

        GameObject go = new GameObject("FlightMapOverlay");
        go.AddComponent<FlightMapOverlay>();
    }

    private void Reset()
    {
        EnsureDefaultRoute();
    }

    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        activeInstance = this;
        DontDestroyOnLoad(gameObject);
        EnsureDefaultRoute();
        BuildUi();
        mapVisible = visibleOnStart;
        canvas.gameObject.SetActive(mapVisible);
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }

        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }

        if (generatedFallbackTexture != null)
        {
            Destroy(generatedFallbackTexture);
        }

        if (tilePlaceholderTexture != null)
        {
            Destroy(tilePlaceholderTexture);
        }

        ReleaseCesiumSceneBasemap();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            mapVisible = !mapVisible;
            canvas.gameObject.SetActive(mapVisible);
        }

        if (!mapVisible)
        {
            return;
        }

        if (bridge == null)
        {
            bridge = JsbsimBridge.Instance != null ? JsbsimBridge.Instance : FindObjectOfType<JsbsimBridge>();
        }

        if (canvas != null)
        {
            int display = GetActiveCameraDisplay();
            if (canvas.targetDisplay != display)
            {
                canvas.targetDisplay = display;
            }
        }

        HandleRangeInput();
        RenderMap();
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        mapSize = Mathf.Clamp(mapSize, minMapSize, maxMapSize);
        mapFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Consolas", "Bahnschrift", "Microsoft YaHei", "Arial" },
            fontSize);

        GameObject canvasGo = new GameObject("FlightMapOverlayCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(null);
        DontDestroyOnLoad(canvasGo);
        canvasRt = canvasGo.GetComponent<RectTransform>();
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvas.targetDisplay = GetActiveCameraDisplay();
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new GameObject("FlightMapPanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = screenOffset;

        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.58f);
        panelBg.raycastTarget = false;

        Image dragHandle = CreateHandle("DragHandle", panelRt, FlightMapWindowHandle.Mode.Drag);
        dragHandleRt = dragHandle.rectTransform;

        headerText = CreateText("Header", panelRt, 12f, 8f, mapSize - 24f, 26f, fontSize + 2, new Color32(220, 224, 226, 255), TextAnchor.MiddleLeft);
        headerText.text = "MAP  TERRAIN / ROUTE / TRACK";

        GameObject viewportGo = new GameObject("MapViewport", typeof(RectTransform));
        viewportGo.transform.SetParent(panelRt, false);
        mapViewportRt = viewportGo.GetComponent<RectTransform>();
        viewportGo.AddComponent<RectMask2D>();

        mapBackgroundImage = CreateMapBackground(mapViewportRt);
        cesiumMapImage = CreateCesiumSceneBackground(mapViewportRt);
        cesiumColorWashImage = CreateCesiumColorWash(mapViewportRt);

        GameObject tileRootGo = new GameObject("TileBasemap", typeof(RectTransform));
        tileRootGo.transform.SetParent(mapViewportRt, false);
        tileRootRt = tileRootGo.GetComponent<RectTransform>();

        GameObject mapGo = new GameObject("MapGraphic", typeof(RectTransform));
        mapGo.transform.SetParent(mapViewportRt, false);
        RectTransform mapRt = mapGo.GetComponent<RectTransform>();
        mapGraphic = mapGo.AddComponent<FlightMapGraphic>();
        mapGraphic.raycastTarget = false;

        GameObject labelRootGo = new GameObject("MapLabels", typeof(RectTransform));
        labelRootGo.transform.SetParent(mapViewportRt, false);
        mapLabelRootRt = labelRootGo.GetComponent<RectTransform>();

        attributionText = CreateText("Attribution", mapViewportRt, 8f, mapSize - 18f, mapSize - 16f, 16f, 10, new Color32(205, 209, 211, 230), TextAnchor.LowerLeft);
        attributionText.text = GetBasemapAttribution();

        GameObject statusPanelGo = new GameObject("MapDataPanel", typeof(RectTransform));
        statusPanelGo.transform.SetParent(mapViewportRt, false);
        statusPanelRt = statusPanelGo.GetComponent<RectTransform>();
        Image statusPanelImage = statusPanelGo.AddComponent<Image>();
        statusPanelImage.color = new Color(0.02f, 0.06f, 0.08f, 0.62f);
        statusPanelImage.raycastTarget = false;

        statusText = CreateText("MapData", statusPanelRt, 10f, 7f, 230f, 62f, fontSize - 2, new Color32(230, 241, 235, 255), TextAnchor.UpperLeft);

        infoText = CreateText("Info", panelRt, 12f, mapSize + 43f, mapSize - 48f, 34f, fontSize - 1, new Color32(224, 238, 232, 255), TextAnchor.UpperLeft);

        Image panHandle = CreateHandle("MapPanHandle", mapViewportRt, FlightMapWindowHandle.Mode.Pan);
        mapPanHandleRt = panHandle.rectTransform;

        resizeLeftRt = CreateHandle("ResizeBorderLeft", panelRt, FlightMapWindowHandle.Mode.ResizeLeft).rectTransform;
        resizeRightRt = CreateHandle("ResizeBorderRight", panelRt, FlightMapWindowHandle.Mode.ResizeRight).rectTransform;
        resizeTopRt = CreateHandle("ResizeBorderTop", panelRt, FlightMapWindowHandle.Mode.ResizeTop).rectTransform;
        resizeBottomRt = CreateHandle("ResizeBorderBottom", panelRt, FlightMapWindowHandle.Mode.ResizeBottom).rectTransform;

        ApplyWindowLayout();
    }

    private Image CreateHandle(string name, RectTransform parent, FlightMapWindowHandle.Mode mode)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.005f);
        image.raycastTarget = true;
        FlightMapWindowHandle handle = go.AddComponent<FlightMapWindowHandle>();
        handle.Setup(this, mode);
        return image;
    }

    private RawImage CreateMapBackground(RectTransform parent)
    {
        Texture2D texture = mapBackgroundTexture != null
            ? mapBackgroundTexture
            : Resources.Load<Texture2D>("FlightMapBackground");
        if (texture == null && generateFallbackMapTexture)
        {
            generatedFallbackTexture = CreateFallbackMapTexture(768);
            texture = generatedFallbackTexture;
        }

        if (texture == null)
        {
            return null;
        }

        GameObject go = new GameObject("MapBackground", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        mapBackgroundRt = go.GetComponent<RectTransform>();

        RawImage image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        return image;
    }

    private RawImage CreateCesiumSceneBackground(RectTransform parent)
    {
        GameObject go = new GameObject("CesiumSceneBasemap", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        cesiumMapRt = go.GetComponent<RectTransform>();

        RawImage image = go.AddComponent<RawImage>();
        image.color = cesiumImageTint;
        image.raycastTarget = false;
        image.gameObject.SetActive(useCesiumSceneBasemap);
        return image;
    }

    private Image CreateCesiumColorWash(RectTransform parent)
    {
        GameObject go = new GameObject("CesiumMapColorWash", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        cesiumColorWashRt = go.GetComponent<RectTransform>();

        Image image = go.AddComponent<Image>();
        image.color = cesiumMapColorWash;
        image.raycastTarget = false;
        image.gameObject.SetActive(useCesiumSceneBasemap);
        return image;
    }

    private void ApplyWindowLayout()
    {
        mapSize = Mathf.Clamp(mapSize, minMapSize, maxMapSize);
        float panelHeight = mapSize + FooterHeight;
        panelRt.sizeDelta = new Vector2(mapSize, panelHeight);

        SetTopLeft(dragHandleRt, 0f, 0f, mapSize, HeaderHeight);
        SetTopLeft(headerText.rectTransform, 12f, 8f, mapSize - 24f, 26f);
        SetTopLeft(mapViewportRt, 0f, HeaderHeight, mapSize, mapSize);

        if (mapBackgroundRt != null)
        {
            SetTopLeft(mapBackgroundRt, 0f, 0f, mapSize, mapSize);
        }

        if (cesiumMapRt != null)
        {
            SetTopLeft(cesiumMapRt, 0f, 0f, mapSize, mapSize);
        }

        if (cesiumColorWashRt != null)
        {
            SetTopLeft(cesiumColorWashRt, 0f, 0f, mapSize, mapSize);
        }

        if (tileRootRt != null)
        {
            SetTopLeft(tileRootRt, 0f, 0f, mapSize, mapSize);
        }

        if (mapGraphic != null)
        {
            SetCentered(mapGraphic.rectTransform, mapSize, mapSize);
            mapGraphic.Configure(mapSize);
            mapGraphic.SetBackgroundVisible(mapBackgroundImage == null && !useCesiumSceneBasemap && !ShouldUseOnlineTileBasemap());
        }

        SetTopLeft(mapLabelRootRt, 0f, 0f, mapSize, mapSize);
        SetTopLeft(attributionText.rectTransform, 8f, mapSize - 18f, mapSize - 16f, 16f);
        SetTopLeft(mapPanHandleRt, 0f, 0f, mapSize, mapSize);
        if (statusPanelRt != null)
        {
            float dataPanelWidth = Mathf.Clamp(mapSize * 0.42f, 220f, 310f);
            float dataPanelHeight = 72f;
            SetTopLeft(statusPanelRt, mapSize - dataPanelWidth - 10f, 10f, dataPanelWidth, dataPanelHeight);
            if (statusText != null)
            {
                SetTopLeft(statusText.rectTransform, 10f, 7f, dataPanelWidth - 20f, dataPanelHeight - 12f);
            }
        }

        SetTopLeft(infoText.rectTransform, 12f, mapSize + 43f, mapSize - 48f, 36f);
        SetTopLeft(resizeLeftRt, 0f, 0f, BorderResizeThickness, panelHeight);
        SetTopLeft(resizeRightRt, mapSize - BorderResizeThickness, 0f, BorderResizeThickness, panelHeight);
        SetTopLeft(resizeTopRt, 0f, 0f, mapSize, BorderResizeThickness);
        SetTopLeft(resizeBottomRt, 0f, panelHeight - BorderResizeThickness, mapSize, BorderResizeThickness);
    }

    private void RenderMap()
    {
        bool hasAircraft = TryReadAircraft(out double aircraftLat, out double aircraftLon, out float headingDeg);
        double centerLat = manualCenterOverride ? manualCenterLatDeg : GetDisplayCenterLat(hasAircraft, aircraftLat);
        double centerLon = manualCenterOverride ? manualCenterLonDeg : GetDisplayCenterLon(hasAircraft, aircraftLon);
        currentCenterLatDeg = centerLat;
        currentCenterLonDeg = centerLon;

        float rangeNm = CalculateRangeNm(hasAircraft, aircraftLat, aircraftLon, centerLat, centerLon);
        currentRangeNm = rangeNm;
        PrepareMercatorProjection(centerLat, rangeNm);

        if (hasAircraft)
        {
            UpdateTrack(aircraftLat, aircraftLon);
        }

        int activeLeg = FindActiveLeg(hasAircraft, aircraftLat, aircraftLon);
        Vector2[] routePoints = BuildRoutePoints(centerLat, centerLon, rangeNm);
        Vector2[] projectedTrack = BuildTrackPoints(centerLat, centerLon, rangeNm);
        Vector2 aircraftPoint = hasAircraft
            ? Project(aircraftLat, aircraftLon, centerLat, centerLon, rangeNm)
            : new Vector2(mapSize * 0.5f, mapSize * 0.5f);

        UpdateFallbackBackground(centerLat, centerLon, rangeNm);
        UpdateCesiumSceneBasemap(centerLat, centerLon, rangeNm);
        UpdateTileBasemap(centerLat, centerLon, rangeNm);

        mapGraphic.SetState(new FlightMapRenderState
        {
            RoutePoints = routePoints ?? emptyRoutePoints,
            TrackPoints = projectedTrack ?? emptyRoutePoints,
            AircraftPoint = aircraftPoint,
            AircraftHeadingDeg = headingDeg,
            HasAircraft = hasAircraft,
            ActiveLegIndex = activeLeg,
            RangeNm = rangeNm
        });

        UpdateInfo(hasAircraft, aircraftLat, aircraftLon, headingDeg, rangeNm, activeLeg);
    }

    private void HandleRangeInput()
    {
        if (!allowMouseWheelRange || mapViewportRt == null)
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(mapViewportRt, Input.mousePosition, null))
        {
            return;
        }

        float baseRange = manualRangeOverride ? manualRangeNm : Mathf.Max(minimumRangeNm, currentRangeNm > 0f ? currentRangeNm : defaultRangeNm);
        manualRangeNm = Mathf.Clamp(baseRange * Mathf.Pow(0.86f, scroll), minimumRangeNm, maximumRangeNm);
        manualRangeOverride = true;
    }

    private Vector2[] BuildRoutePoints(double centerLat, double centerLon, float rangeNm)
    {
        if (route == null || route.Length == 0)
        {
            return emptyRoutePoints;
        }

        Vector2[] points = new Vector2[route.Length];
        for (int i = 0; i < route.Length; i++)
        {
            points[i] = Project(route[i].latitudeDeg, route[i].longitudeDeg, centerLat, centerLon, rangeNm);
        }

        return points;
    }

    private Vector2[] BuildTrackPoints(double centerLat, double centerLon, float rangeNm)
    {
        if (!recordTrack || trackPoints.Count < 2)
        {
            return emptyRoutePoints;
        }

        Vector2[] points = new Vector2[trackPoints.Count];
        for (int i = 0; i < trackPoints.Count; i++)
        {
            GeoPoint point = trackPoints[i];
            points[i] = Project(point.LatitudeDeg, point.LongitudeDeg, centerLat, centerLon, rangeNm);
        }

        return points;
    }

    private Vector2 Project(double latitudeDeg, double longitudeDeg, double centerLat, double centerLon, float rangeNm)
    {
        if (ShouldUseOnlineTileBasemap() && currentTileZoom >= 0)
        {
            LatLonToWorldPixel(centerLat, centerLon, currentTileZoom, out double centerX, out double centerY);
            LatLonToWorldPixel(latitudeDeg, longitudeDeg, currentTileZoom, out double pointX, out double pointY);
            return new Vector2(
                mapSize * 0.5f + (float)((pointX - centerX) * currentTileDisplayScale),
                mapSize * 0.5f + (float)((pointY - centerY) * currentTileDisplayScale));
        }

        LatLonToOffsetNm(centerLat, centerLon, latitudeDeg, longitudeDeg, out double northNm, out double eastNm);
        float pixelsPerNm = (mapSize * 0.5f) / Mathf.Max(minimumRangeNm, rangeNm);
        return new Vector2(
            mapSize * 0.5f + (float)eastNm * pixelsPerNm,
            mapSize * 0.5f - (float)northNm * pixelsPerNm);
    }

    private float CalculateRangeNm(bool hasAircraft, double aircraftLat, double aircraftLon, double centerLat, double centerLon)
    {
        if (manualRangeOverride)
        {
            return Mathf.Clamp(manualRangeNm, minimumRangeNm, maximumRangeNm);
        }

        float range = defaultRangeNm;
        if (!autoFitRoute)
        {
            return Mathf.Clamp(range, minimumRangeNm, maximumRangeNm);
        }

        double maxDistance = 0.0;

        if (route != null)
        {
            for (int i = 0; i < route.Length; i++)
            {
                CalculateBearingDistance(centerLat, centerLon, route[i].latitudeDeg, route[i].longitudeDeg, out _, out double distanceNm);
                maxDistance = Math.Max(maxDistance, distanceNm);
            }
        }

        if (hasAircraft)
        {
            CalculateBearingDistance(centerLat, centerLon, aircraftLat, aircraftLon, out _, out double aircraftDistanceNm);
            maxDistance = Math.Max(maxDistance, aircraftDistanceNm);
        }

        if (recordTrack && trackPoints.Count > 0)
        {
            int step = Mathf.Max(1, trackPoints.Count / 80);
            for (int i = 0; i < trackPoints.Count; i += step)
            {
                GeoPoint point = trackPoints[i];
                CalculateBearingDistance(centerLat, centerLon, point.LatitudeDeg, point.LongitudeDeg, out _, out double distanceNm);
                maxDistance = Math.Max(maxDistance, distanceNm);
            }
        }

        if (maxDistance > 0.1)
        {
            range = Mathf.Max(defaultRangeNm, (float)(maxDistance / Mathf.Max(0.1f, routeFitFill)));
        }

        return Mathf.Clamp(range, minimumRangeNm, maximumRangeNm);
    }

    private int FindActiveLeg(bool hasAircraft, double aircraftLat, double aircraftLon)
    {
        if (!hasAircraft || route == null || route.Length < 2)
        {
            return -1;
        }

        int nearestIndex = 0;
        double nearestDistance = double.MaxValue;
        for (int i = 0; i < route.Length; i++)
        {
            CalculateBearingDistance(aircraftLat, aircraftLon, route[i].latitudeDeg, route[i].longitudeDeg, out _, out double distanceNm);
            if (distanceNm < nearestDistance)
            {
                nearestDistance = distanceNm;
                nearestIndex = i;
            }
        }

        if (nearestIndex >= route.Length - 1)
        {
            return route.Length - 2;
        }

        return nearestIndex;
    }

    private void UpdateTrack(double aircraftLat, double aircraftLon)
    {
        if (!recordTrack)
        {
            return;
        }

        if (trackPoints.Count > 0)
        {
            GeoPoint last = trackPoints[trackPoints.Count - 1];
            CalculateBearingDistance(last.LatitudeDeg, last.LongitudeDeg, aircraftLat, aircraftLon, out _, out double distanceNm);
            if (distanceNm < trackMinDistanceNm)
            {
                return;
            }
        }

        trackPoints.Add(new GeoPoint(aircraftLat, aircraftLon));
        while (trackPoints.Count > maxTrackPoints)
        {
            trackPoints.RemoveAt(0);
        }
    }

    private void UpdateFallbackBackground(double centerLat, double centerLon, float rangeNm)
    {
        if (mapBackgroundImage == null)
        {
            return;
        }

        double routeCenterLat = GetRouteCenterLat();
        double routeCenterLon = GetRouteCenterLon();
        LatLonToOffsetNm(routeCenterLat, routeCenterLon, centerLat, centerLon, out double northNm, out double eastNm);

        float visibleSize = Mathf.Clamp(rangeNm * 2f / backgroundTextureRangeNm, 0.08f, 1.6f);
        float u = 0.5f + (float)(eastNm / backgroundTextureRangeNm) - visibleSize * 0.5f;
        float v = 0.5f + (float)(northNm / backgroundTextureRangeNm) - visibleSize * 0.5f;
        mapBackgroundImage.uvRect = new Rect(u, v, visibleSize, visibleSize);
    }

    private void UpdateInfo(bool hasAircraft, double lat, double lon, float headingDeg, float rangeNm, int activeLeg)
    {
        float iasKts = bridge != null ? bridge.SpeedKts : 0f;
        float trueKts = bridge != null ? bridge.TrueSpeedKts : 0f;
        float altitudeFt = bridge != null ? bridge.AltitudeFt : 0f;
        float aglFt = bridge != null ? bridge.AglFt : 0f;
        float groundSpeedKts = ReadGroundSpeedKts(iasKts);
        string legText = "--";
        string nextText = "--";
        string sourceText = GetBasemapStatusLabel();

        infoBuilder.Length = 0;
        if (hasAircraft)
        {
            infoBuilder.AppendFormat(CultureInfo.InvariantCulture, "POS {0:F5}, {1:F5}   MAP RNG {2:0}NM   {3}",
                lat,
                lon,
                rangeNm,
                sourceText);
        }
        else
        {
            infoBuilder.AppendFormat(CultureInfo.InvariantCulture, "WAITING JSBSIM POSITION   MAP RNG {0:0}NM   {1}", rangeNm, sourceText);
        }

        if (route != null && route.Length > 1 && activeLeg >= 0 && activeLeg + 1 < route.Length)
        {
            Waypoint from = route[activeLeg];
            Waypoint to = route[activeLeg + 1];
            double distanceToNext = 0.0;
            double bearingToNext = 0.0;
            if (hasAircraft)
            {
                CalculateBearingDistance(lat, lon, to.latitudeDeg, to.longitudeDeg, out bearingToNext, out distanceToNext);
            }
            else
            {
                CalculateBearingDistance(from.latitudeDeg, from.longitudeDeg, to.latitudeDeg, to.longitudeDeg, out bearingToNext, out distanceToNext);
            }

            legText = SafeIdent(from, activeLeg) + ">" + SafeIdent(to, activeLeg + 1);
            nextText = string.Format(CultureInfo.InvariantCulture, "{0:0.0}NM {1:000}", distanceToNext, bearingToNext);
        }

        if (statusText != null)
        {
            if (hasAircraft)
            {
                statusText.text = string.Format(CultureInfo.InvariantCulture,
                    "HDG {0:000}   IAS {1:0} KT\nGS  {2:0} KT  ALT {3:0} FT\nLEG {4}   NEXT {5}",
                    Normalize360(headingDeg),
                    iasKts,
                    groundSpeedKts,
                    altitudeFt,
                    legText,
                    nextText);
            }
            else
            {
                statusText.text = string.Format(CultureInfo.InvariantCulture,
                    "JSBSIM WAIT\nRNG {0:0} NM\nROUTE {1} WPTS",
                    rangeNm,
                    route != null ? route.Length : 0);
            }
        }

        infoText.text = infoBuilder.ToString();
    }

    private float ReadGroundSpeedKts(float fallbackKts)
    {
        if (bridge == null)
        {
            return fallbackKts;
        }

        if (bridge.TryGetValue("velocities_vg_fps", out float groundSpeedFps))
        {
            return Mathf.Max(0f, groundSpeedFps * FeetPerSecondToKnots);
        }

        if (bridge.TryGetValue("velocities_ned_velocity_mag_fps", out groundSpeedFps))
        {
            return Mathf.Max(0f, groundSpeedFps * FeetPerSecondToKnots);
        }

        return fallbackKts;
    }

    private double CalculateRemainingRouteDistanceNm(bool hasAircraft, double aircraftLat, double aircraftLon, int activeLeg)
    {
        if (route == null || route.Length < 2 || activeLeg < 0)
        {
            return 0.0;
        }

        double total = 0.0;
        int nextIndex = Mathf.Clamp(activeLeg + 1, 1, route.Length - 1);
        if (hasAircraft)
        {
            CalculateBearingDistance(aircraftLat, aircraftLon, route[nextIndex].latitudeDeg, route[nextIndex].longitudeDeg, out _, out double firstDistance);
            total += firstDistance;
        }

        for (int i = nextIndex; i < route.Length - 1; i++)
        {
            CalculateBearingDistance(route[i].latitudeDeg, route[i].longitudeDeg, route[i + 1].latitudeDeg, route[i + 1].longitudeDeg, out _, out double legDistance);
            total += legDistance;
        }

        return total;
    }

    private bool TryReadAircraft(out double latDeg, out double lonDeg, out float headingDeg)
    {
        latDeg = 0.0;
        lonDeg = 0.0;
        headingDeg = 0f;

        if (bridge == null || !bridge.HasState)
        {
            return false;
        }

        bool hasLat = bridge.TryGetValue("lat_deg", out float lat);
        bool hasLon = bridge.TryGetValue("lon_deg", out float lon);
        latDeg = lat;
        lonDeg = lon;
        headingDeg = Normalize360(bridge.HeadingDeg);
        return hasLat && hasLon;
    }

    private void EnsureDefaultRoute()
    {
        if (route != null && route.Length > 0)
        {
            return;
        }

        route = new[]
        {
            new Waypoint { ident = "DEP", latitudeDeg = 33.5200, longitudeDeg = 120.3000 },
            new Waypoint { ident = "WPT1", latitudeDeg = 33.5550, longitudeDeg = 120.4300 },
            new Waypoint { ident = "WPT2", latitudeDeg = 33.6400, longitudeDeg = 120.6100 },
            new Waypoint { ident = "ARR", latitudeDeg = 33.7200, longitudeDeg = 120.8200 }
        };
    }

    private void PrepareMercatorProjection(double centerLat, float rangeNm)
    {
        if (!ShouldUseOnlineTileBasemap())
        {
            currentTileZoom = -1;
            currentTileDisplayScale = 1f;
            return;
        }

        currentTileZoom = CalculateTileZoom(centerLat, rangeNm);
        currentTileDisplayScale = CalculateTileDisplayScale(currentTileZoom, centerLat, rangeNm);
    }

    private int CalculateTileZoom(double centerLat, float rangeNm)
    {
        double metersPerPixel = Math.Max(1.0, rangeNm * 2.0 * 1852.0 / Math.Max(1.0, mapSize));
        double cosLat = Math.Max(0.1, Math.Cos(centerLat * Math.PI / 180.0));
        double equatorResolution = 2.0 * Math.PI * EarthRadiusMeters / tileSizePx;
        double zoom = Math.Log(cosLat * equatorResolution / metersPerPixel, 2.0);
        return Mathf.Clamp(Mathf.RoundToInt((float)zoom), minTileZoom, maxTileZoom);
    }

    private float CalculateTileDisplayScale(int zoom, double centerLat, float rangeNm)
    {
        double worldSize = GetWorldSizePixels(zoom);
        double cosLat = Math.Max(0.1, Math.Cos(centerLat * Math.PI / 180.0));
        double worldPixelsPerNm = worldSize / (360.0 * 60.0 * cosLat);
        double desiredPixelsPerNm = (mapSize * 0.5) / Math.Max(minimumRangeNm, rangeNm);
        return Mathf.Clamp((float)(desiredPixelsPerNm / Math.Max(0.0001, worldPixelsPerNm)), 0.05f, 8f);
    }

    private void UpdateCesiumSceneBasemap(double centerLat, double centerLon, float rangeNm)
    {
        cesiumSceneBasemapAvailable = useCesiumSceneBasemap && EnsureCesiumSceneBasemap();

        if (cesiumMapImage != null)
        {
            cesiumMapImage.gameObject.SetActive(cesiumSceneBasemapAvailable);
            cesiumMapImage.texture = cesiumSceneBasemapAvailable ? cesiumMapTexture : null;
            cesiumMapImage.color = cesiumImageTint;
        }

        if (cesiumColorWashImage != null)
        {
            cesiumColorWashImage.gameObject.SetActive(cesiumSceneBasemapAvailable);
            cesiumColorWashImage.color = cesiumMapColorWash;
        }

        if (!cesiumSceneBasemapAvailable)
        {
            return;
        }

        Vector3 center = LongitudeLatitudeHeightToWorld(centerLon, centerLat, 0.0);
        Vector3 up = cesiumGeoreference.transform.TransformDirection(Vector3.up).normalized;
        Vector3 north = cesiumGeoreference.transform.TransformDirection(Vector3.forward).normalized;
        float halfRangeMeters = Mathf.Max(minimumRangeNm, rangeNm) * 1852f;
        float cameraHeight = Mathf.Max(cesiumMinimumCameraHeightMeters, halfRangeMeters * 2.2f);

        cesiumMapCamera.transform.position = center + up * cameraHeight;
        cesiumMapCamera.transform.rotation = Quaternion.LookRotation(-up, north);
        cesiumMapCamera.orthographicSize = halfRangeMeters;
        cesiumMapCamera.nearClipPlane = 0.1f;
        cesiumMapCamera.farClipPlane = cameraHeight + Mathf.Max(cesiumFarPaddingMeters, halfRangeMeters * 1.5f);
        cesiumMapCamera.backgroundColor = cesiumMapClearColor;
        cesiumMapCamera.cullingMask = cesiumSceneLayerMask;
        cesiumMapCamera.Render();

        if (attributionText != null)
        {
            attributionText.text = GetBasemapAttribution();
        }
    }

    private bool EnsureCesiumSceneBasemap()
    {
        if (!useCesiumSceneBasemap)
        {
            return false;
        }

        if (cesiumGeoreference == null)
        {
            cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
        }

        if (cesiumGeoreference == null)
        {
            return false;
        }

        cesiumGeoreference.Initialize();

        int textureSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(cesiumMapTextureSize, 256, 2048));
        if (cesiumMapTexture == null || cesiumMapTexture.width != textureSize || cesiumMapTexture.height != textureSize)
        {
            if (cesiumMapTexture != null)
            {
                cesiumMapTexture.Release();
                Destroy(cesiumMapTexture);
            }

            cesiumMapTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "FlightMap_CesiumSceneBasemap",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            cesiumMapTexture.Create();
        }

        if (cesiumMapCamera == null)
        {
            GameObject cameraGo = new GameObject("FlightMapCesiumSceneCamera");
            DontDestroyOnLoad(cameraGo);
            cesiumMapCamera = cameraGo.AddComponent<Camera>();
            cesiumMapCamera.enabled = false;
            cesiumMapCamera.orthographic = true;
            cesiumMapCamera.clearFlags = CameraClearFlags.SolidColor;
            cesiumMapCamera.allowHDR = false;
            cesiumMapCamera.allowMSAA = true;
            cesiumMapCamera.useOcclusionCulling = false;
        }

        cesiumMapCamera.targetTexture = cesiumMapTexture;
        RegisterCesiumMapCamera();
        return true;
    }

    private void RegisterCesiumMapCamera()
    {
        if (cesiumMapCamera == null || cesiumGeoreference == null)
        {
            return;
        }

        if (cesiumCameraManager == null)
        {
            cesiumCameraManager = CesiumCameraManager.GetOrCreate(cesiumGeoreference.gameObject);
        }

        if (cesiumCameraManager != null && !cesiumCameraManager.additionalCameras.Contains(cesiumMapCamera))
        {
            cesiumCameraManager.additionalCameras.Add(cesiumMapCamera);
        }
    }

    private Vector3 LongitudeLatitudeHeightToWorld(double longitudeDeg, double latitudeDeg, double heightMeters)
    {
        double3 ecef = cesiumGeoreference.ellipsoid.LongitudeLatitudeHeightToCenteredFixed(
            new double3(longitudeDeg, latitudeDeg, heightMeters));
        double3 unity = cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
        Vector3 local = new Vector3((float)unity.x, (float)unity.y, (float)unity.z);
        return cesiumGeoreference.transform.TransformPoint(local);
    }

    private void ReleaseCesiumSceneBasemap()
    {
        if (cesiumCameraManager != null && cesiumMapCamera != null)
        {
            cesiumCameraManager.additionalCameras.Remove(cesiumMapCamera);
        }

        if (cesiumMapCamera != null)
        {
            Destroy(cesiumMapCamera.gameObject);
            cesiumMapCamera = null;
        }

        if (cesiumMapTexture != null)
        {
            cesiumMapTexture.Release();
            Destroy(cesiumMapTexture);
            cesiumMapTexture = null;
        }
    }

    private bool ShouldUseOnlineTileBasemap()
    {
        return useOnlineTileBasemap && !useCesiumSceneBasemap;
    }

    private string GetBasemapStatusLabel()
    {
        if (useCesiumSceneBasemap)
        {
            return cesiumSceneBasemapAvailable ? "CESIUM 3D TERRAIN" : "CESIUM LOADING";
        }

        return ShouldUseOnlineTileBasemap() ? "ONLINE TILE" : "LOCAL TERRAIN";
    }

    private string GetBasemapAttribution()
    {
        if (useCesiumSceneBasemap)
        {
            return cesiumAttribution;
        }

        return ShouldUseOnlineTileBasemap() ? tileAttribution : "";
    }

    private void UpdateTileBasemap(double centerLat, double centerLon, float rangeNm)
    {
        if (!ShouldUseOnlineTileBasemap() || tileRootRt == null || string.IsNullOrWhiteSpace(tileUrlTemplate))
        {
            if (tileRootRt != null)
            {
                tileRootRt.gameObject.SetActive(false);
            }

            if (attributionText != null)
            {
                attributionText.text = GetBasemapAttribution();
            }

            return;
        }

        tileRootRt.gameObject.SetActive(true);
        if (attributionText != null)
        {
            attributionText.text = tileAttribution;
        }

        int zoom = currentTileZoom;
        double centerX;
        double centerY;
        LatLonToWorldPixel(centerLat, centerLon, zoom, out centerX, out centerY);
        float scale = currentTileDisplayScale;
        double halfWorldPixels = (mapSize * 0.5) / Math.Max(0.001, scale);
        int minX = Mathf.FloorToInt((float)((centerX - halfWorldPixels) / tileSizePx));
        int maxX = Mathf.FloorToInt((float)((centerX + halfWorldPixels) / tileSizePx));
        int minY = Mathf.FloorToInt((float)((centerY - halfWorldPixels) / tileSizePx));
        int maxY = Mathf.FloorToInt((float)((centerY + halfWorldPixels) / tileSizePx));

        while ((maxX - minX + 1) * (maxY - minY + 1) > maxVisibleTiles && zoom > minTileZoom)
        {
            zoom--;
            currentTileZoom = zoom;
            currentTileDisplayScale = CalculateTileDisplayScale(zoom, centerLat, rangeNm);
            LatLonToWorldPixel(centerLat, centerLon, zoom, out centerX, out centerY);
            scale = currentTileDisplayScale;
            halfWorldPixels = (mapSize * 0.5) / Math.Max(0.001, scale);
            minX = Mathf.FloorToInt((float)((centerX - halfWorldPixels) / tileSizePx));
            maxX = Mathf.FloorToInt((float)((centerX + halfWorldPixels) / tileSizePx));
            minY = Mathf.FloorToInt((float)((centerY - halfWorldPixels) / tileSizePx));
            maxY = Mathf.FloorToInt((float)((centerY + halfWorldPixels) / tileSizePx));
        }

        int tileCount = 1 << zoom;
        minY = Mathf.Clamp(minY, 0, tileCount - 1);
        maxY = Mathf.Clamp(maxY, 0, tileCount - 1);
        visibleTileKeys.Clear();

        for (int ty = minY; ty <= maxY; ty++)
        {
            for (int tx = minX; tx <= maxX; tx++)
            {
                int wrappedX = PositiveModulo(tx, tileCount);
                string key = BuildTileKey(zoom, wrappedX, ty);
                visibleTileKeys.Add(key);

                RawImage image = GetOrCreateTileImage(key);
                image.gameObject.SetActive(true);
                double tileWorldX = tx * tileSizePx;
                double tileWorldY = ty * tileSizePx;
                float localX = mapSize * 0.5f + (float)((tileWorldX - centerX) * scale);
                float localY = mapSize * 0.5f + (float)((tileWorldY - centerY) * scale);
                SetTopLeft(image.rectTransform, localX, localY, tileSizePx * scale, tileSizePx * scale);

                if (tileTextures.TryGetValue(key, out Texture2D texture) && texture != null)
                {
                    image.texture = texture;
                    image.color = Color.white;
                }
                else
                {
                    image.texture = GetTilePlaceholderTexture();
                    image.color = new Color(1f, 1f, 1f, 0.08f);
                    if (!loadingTiles.Contains(key))
                    {
                        StartCoroutine(LoadTileCoroutine(zoom, wrappedX, ty, key));
                    }
                }
            }
        }

        foreach (KeyValuePair<string, RawImage> pair in tileImages)
        {
            if (!visibleTileKeys.Contains(pair.Key) && pair.Value != null)
            {
                pair.Value.gameObject.SetActive(false);
            }
        }
    }

    private RawImage GetOrCreateTileImage(string key)
    {
        if (tileImages.TryGetValue(key, out RawImage existing) && existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("Tile_" + key, typeof(RectTransform));
        go.transform.SetParent(tileRootRt, false);
        RawImage image = go.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = GetTilePlaceholderTexture();
        tileImages[key] = image;
        return image;
    }

    private IEnumerator LoadTileCoroutine(int zoom, int x, int y, string key)
    {
        loadingTiles.Add(key);
        Texture2D cached = TryLoadTileFromDisk(key);
        if (cached != null)
        {
            tileTextures[key] = cached;
            loadingTiles.Remove(key);
            yield break;
        }

        string url = tileUrlTemplate
            .Replace("{z}", zoom.ToString(CultureInfo.InvariantCulture))
            .Replace("{x}", x.ToString(CultureInfo.InvariantCulture))
            .Replace("{y}", y.ToString(CultureInfo.InvariantCulture));

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, false))
        {
            request.timeout = 10;
            if (!string.IsNullOrWhiteSpace(tileUserAgent))
            {
                request.SetRequestHeader("User-Agent", tileUserAgent);
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                tileTextures[key] = texture;
                SaveTileToDisk(key, request.downloadHandler.data);
            }
            else
            {
                Debug.LogWarning("[FlightMap] Tile request failed: " + url + " " + request.error);
            }
        }

        loadingTiles.Remove(key);
    }

    private Texture2D TryLoadTileFromDisk(string key)
    {
        if (!cacheTilesOnDisk)
        {
            return null;
        }

        string path = GetTileCachePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        DateTime lastWrite = File.GetLastWriteTimeUtc(path);
        if ((DateTime.UtcNow - lastWrite).TotalDays > tileCacheDays)
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FlightMap] Failed to read cached tile: " + e.Message);
            return null;
        }
    }

    private void SaveTileToDisk(string key, byte[] bytes)
    {
        if (!cacheTilesOnDisk || bytes == null || bytes.Length == 0)
        {
            return;
        }

        try
        {
            string path = GetTileCachePath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FlightMap] Failed to cache tile: " + e.Message);
        }
    }

    private string GetTileCachePath(string key)
    {
        return Path.Combine(Application.persistentDataPath, "FlightMapTiles", key + ".png");
    }

    private Texture2D GetTilePlaceholderTexture()
    {
        if (tilePlaceholderTexture != null)
        {
            return tilePlaceholderTexture;
        }

        tilePlaceholderTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "FlightMap_TilePlaceholder",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        Color32 c = new Color32(210, 226, 210, 255);
        tilePlaceholderTexture.SetPixels32(new[] { c, c, c, c });
        tilePlaceholderTexture.Apply(false, true);
        return tilePlaceholderTexture;
    }

    private Texture2D CreateFallbackMapTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated_Flight_Map_Background",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32 water = new Color32(194, 226, 232, 255);
        Color32 shallowWater = new Color32(178, 217, 226, 255);
        Color32 land = new Color32(218, 227, 194, 255);
        Color32 field = new Color32(204, 219, 178, 255);
        Color32 city = new Color32(194, 199, 190, 255);
        Color32 road = new Color32(236, 221, 172, 255);
        Color32 highway = new Color32(154, 200, 216, 255);
        Color32 river = new Color32(171, 218, 231, 255);
        Color32 coast = new Color32(152, 181, 168, 255);
        Color32 runway = new Color32(118, 126, 124, 255);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)(size - 1);
                float coastLine = 0.52f + 0.08f * Mathf.Sin(ny * 9f) + 0.035f * Mathf.Sin(ny * 27f);
                bool isLand = nx > coastLine;
                Color32 c = isLand ? land : water;

                if (!isLand && Mathf.Abs(nx - coastLine) < 0.035f)
                {
                    c = shallowWater;
                }

                if (Mathf.Abs(nx - coastLine) < 0.008f)
                {
                    c = coast;
                }

                if (isLand)
                {
                    float cityMask = Mathf.Sin((nx + 0.17f) * 35f) * Mathf.Sin((ny + 0.29f) * 31f);
                    if (cityMask > 0.72f)
                    {
                        c = city;
                    }

                    float fieldMask = Mathf.Sin(nx * 58f) * Mathf.Sin(ny * 46f);
                    if (fieldMask > 0.55f)
                    {
                        c = field;
                    }

                    float riverCenter = 0.72f + 0.035f * Mathf.Sin(ny * 14f);
                    if (Mathf.Abs(nx - riverCenter) < 0.005f + 0.003f * Mathf.Sin(ny * 33f))
                    {
                        c = river;
                    }

                    if (Mathf.Abs(Mathf.Sin((nx * 0.7f + ny) * 34f)) < 0.018f ||
                        Mathf.Abs(Mathf.Sin((nx - ny * 0.42f) * 28f)) < 0.016f)
                    {
                        c = road;
                    }

                    if (Mathf.Abs((nx - 0.2f) - (ny - 0.5f) * 0.17f) < 0.006f ||
                        Mathf.Abs((nx - 0.93f) + (ny - 0.45f) * 0.08f) < 0.006f)
                    {
                        c = highway;
                    }

                    if (IsRotatedRect(nx, ny, 0.64f, 0.55f, 0.035f, 0.19f, -18f) ||
                        IsRotatedRect(nx, ny, 0.81f, 0.33f, 0.028f, 0.14f, 12f))
                    {
                        c = runway;
                    }
                }

                pixels[y * size + x] = c;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static bool IsRotatedRect(float x, float y, float cx, float cy, float halfWidth, float halfHeight, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float dx = x - cx;
        float dy = y - cy;
        float localX = dx * Mathf.Cos(rad) - dy * Mathf.Sin(rad);
        float localY = dx * Mathf.Sin(rad) + dy * Mathf.Cos(rad);
        return Mathf.Abs(localX) <= halfWidth && Mathf.Abs(localY) <= halfHeight;
    }

    internal RectTransform CanvasRectTransform => canvasRt;
    internal RectTransform PanelRectTransform => panelRt;
    internal double CurrentCenterLatitudeDeg => manualCenterOverride ? manualCenterLatDeg : currentCenterLatDeg;
    internal double CurrentCenterLongitudeDeg => manualCenterOverride ? manualCenterLonDeg : currentCenterLonDeg;

    internal bool TryScreenToCanvasPoint(PointerEventData eventData, out Vector2 point)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, eventData.position, eventData.pressEventCamera, out point);
    }

    internal void SetPanelAnchoredPosition(Vector2 anchoredPosition)
    {
        panelRt.anchoredPosition = anchoredPosition;
    }

    internal void PanMapFromDragDelta(double startCenterLat, double startCenterLon, Vector2 delta)
    {
        float range = Mathf.Max(minimumRangeNm, currentRangeNm > 0f ? currentRangeNm : defaultRangeNm);
        double eastNm = -delta.x * range * 2.0 / Math.Max(1.0, mapSize);
        double northNm = delta.y * range * 2.0 / Math.Max(1.0, mapSize);
        OffsetLatLon(startCenterLat, startCenterLon, northNm, eastNm, out manualCenterLatDeg, out manualCenterLonDeg);
        manualCenterOverride = true;
    }

    internal void ResizeFromDragDelta(FlightMapWindowHandle.Mode mode, Vector2 startAnchoredPosition, float startMapSize, Vector2 delta)
    {
        Vector2 topLeft = new Vector2(startAnchoredPosition.x - startMapSize, startAnchoredPosition.y);
        float requestedSize = startMapSize;
        Vector2 nextTopLeft = topLeft;

        switch (mode)
        {
            case FlightMapWindowHandle.Mode.ResizeLeft:
                requestedSize = startMapSize - delta.x;
                nextTopLeft.x = topLeft.x + (startMapSize - Mathf.Clamp(requestedSize, minMapSize, maxMapSize));
                break;
            case FlightMapWindowHandle.Mode.ResizeRight:
                requestedSize = startMapSize + delta.x;
                break;
            case FlightMapWindowHandle.Mode.ResizeTop:
                requestedSize = startMapSize + delta.y;
                nextTopLeft.y = topLeft.y + (Mathf.Clamp(requestedSize, minMapSize, maxMapSize) - startMapSize);
                break;
            case FlightMapWindowHandle.Mode.ResizeBottom:
                requestedSize = startMapSize - delta.y;
                break;
        }

        mapSize = Mathf.Clamp(requestedSize, minMapSize, maxMapSize);
        panelRt.anchoredPosition = new Vector2(nextTopLeft.x + mapSize, nextTopLeft.y);
        ApplyWindowLayout();
    }

    private Text CreateText(string name, RectTransform parent, float x, float y, float w, float h, int size, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = mapFont;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        SetTopLeft(text.rectTransform, x, y, w, h);
        return text;
    }

    private double GetRouteCenterLat()
    {
        if (route == null || route.Length == 0)
        {
            return currentCenterLatDeg;
        }

        double total = 0.0;
        for (int i = 0; i < route.Length; i++)
        {
            total += route[i].latitudeDeg;
        }

        return total / route.Length;
    }

    private double GetDisplayCenterLat(bool hasAircraft, double aircraftLat)
    {
        if (keepAircraftCentered && hasAircraft)
        {
            return aircraftLat;
        }

        if (route != null && route.Length > 0)
        {
            double minLat = route[0].latitudeDeg;
            double maxLat = route[0].latitudeDeg;
            for (int i = 1; i < route.Length; i++)
            {
                minLat = Math.Min(minLat, route[i].latitudeDeg);
                maxLat = Math.Max(maxLat, route[i].latitudeDeg);
            }

            if (hasAircraft)
            {
                minLat = Math.Min(minLat, aircraftLat);
                maxLat = Math.Max(maxLat, aircraftLat);
            }

            return (minLat + maxLat) * 0.5;
        }

        return hasAircraft ? aircraftLat : currentCenterLatDeg;
    }

    private double GetDisplayCenterLon(bool hasAircraft, double aircraftLon)
    {
        if (keepAircraftCentered && hasAircraft)
        {
            return aircraftLon;
        }

        if (route != null && route.Length > 0)
        {
            double minLon = route[0].longitudeDeg;
            double maxLon = route[0].longitudeDeg;
            for (int i = 1; i < route.Length; i++)
            {
                minLon = Math.Min(minLon, route[i].longitudeDeg);
                maxLon = Math.Max(maxLon, route[i].longitudeDeg);
            }

            if (hasAircraft)
            {
                minLon = Math.Min(minLon, aircraftLon);
                maxLon = Math.Max(maxLon, aircraftLon);
            }

            return (minLon + maxLon) * 0.5;
        }

        return hasAircraft ? aircraftLon : currentCenterLonDeg;
    }

    private double GetRouteCenterLon()
    {
        if (route == null || route.Length == 0)
        {
            return currentCenterLonDeg;
        }

        double total = 0.0;
        for (int i = 0; i < route.Length; i++)
        {
            total += route[i].longitudeDeg;
        }

        return total / route.Length;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    private static void LatLonToOffsetNm(double originLatDeg, double originLonDeg, double targetLatDeg, double targetLonDeg, out double northNm, out double eastNm)
    {
        double latRad = originLatDeg * Math.PI / 180.0;
        northNm = (targetLatDeg - originLatDeg) * 60.0;
        eastNm = (targetLonDeg - originLonDeg) * 60.0 * Math.Cos(latRad);
    }

    private static void OffsetLatLon(double originLatDeg, double originLonDeg, double northNm, double eastNm, out double latitudeDeg, out double longitudeDeg)
    {
        double latRad = originLatDeg * Math.PI / 180.0;
        latitudeDeg = originLatDeg + northNm / 60.0;
        longitudeDeg = originLonDeg + eastNm / Math.Max(0.000001, 60.0 * Math.Cos(latRad));
    }

    private static void CalculateBearingDistance(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg, out double bearingDeg, out double distanceNm)
    {
        double lat1 = lat1Deg * Math.PI / 180.0;
        double lat2 = lat2Deg * Math.PI / 180.0;
        double dLat = (lat2Deg - lat1Deg) * Math.PI / 180.0;
        double dLon = (lon2Deg - lon1Deg) * Math.PI / 180.0;

        double y = Math.Sin(dLon) * Math.Cos(lat2);
        double x = Math.Cos(lat1) * Math.Sin(lat2) -
                   Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        bearingDeg = Normalize360((float)(Math.Atan2(y, x) * 180.0 / Math.PI));

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(lat1) * Math.Cos(lat2) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        distanceNm = Math.Max(0.0, EarthRadiusNm * c);
    }

    private static void LatLonToWorldPixel(double latDeg, double lonDeg, int zoom, out double x, out double y)
    {
        double worldSize = GetWorldSizePixels(zoom);
        double sinLat = Math.Sin(Mathf.Clamp((float)latDeg, -85.05112878f, 85.05112878f) * Math.PI / 180.0);
        x = (lonDeg + 180.0) / 360.0 * worldSize;
        y = (0.5 - Math.Log((1.0 + sinLat) / (1.0 - sinLat)) / (4.0 * Math.PI)) * worldSize;
    }

    private static double GetWorldSizePixels(int zoom)
    {
        return 256.0 * (1 << zoom);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static string BuildTileKey(int zoom, int x, int y)
    {
        return zoom.ToString(CultureInfo.InvariantCulture) + "_" +
               x.ToString(CultureInfo.InvariantCulture) + "_" +
               y.ToString(CultureInfo.InvariantCulture);
    }

    private static string SafeIdent(Waypoint waypoint, int index)
    {
        return waypoint != null && !string.IsNullOrWhiteSpace(waypoint.ident)
            ? waypoint.ident
            : "WPT" + (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void SetCentered(RectTransform rt, float w, float h)
    {
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    private static float Normalize360(float deg)
    {
        deg %= 360f;
        if (deg < 0f)
        {
            deg += 360f;
        }

        return deg;
    }

    private static int GetActiveCameraDisplay()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Camera[] cams = Camera.allCameras;
            if (cams.Length > 0)
            {
                cam = cams[0];
            }
        }

        return cam != null ? cam.targetDisplay : 0;
    }
}

public sealed class FlightMapWindowHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public enum Mode
    {
        Drag,
        Pan,
        ResizeLeft,
        ResizeRight,
        ResizeTop,
        ResizeBottom
    }

    private FlightMapOverlay owner;
    private Mode mode;
    private Vector2 startPointer;
    private Vector2 startAnchoredPosition;
    private float startMapSize;
    private double startCenterLat;
    private double startCenterLon;

    public void Setup(FlightMapOverlay overlay, Mode handleMode)
    {
        owner = overlay;
        mode = handleMode;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner == null || !owner.TryScreenToCanvasPoint(eventData, out startPointer))
        {
            return;
        }

        startAnchoredPosition = owner.PanelRectTransform.anchoredPosition;
        startMapSize = owner.PanelRectTransform.sizeDelta.x;
        startCenterLat = owner.CurrentCenterLatitudeDeg;
        startCenterLon = owner.CurrentCenterLongitudeDeg;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner == null || !owner.TryScreenToCanvasPoint(eventData, out Vector2 currentPointer))
        {
            return;
        }

        Vector2 delta = currentPointer - startPointer;
        if (mode == Mode.Drag)
        {
            owner.SetPanelAnchoredPosition(startAnchoredPosition + delta);
        }
        else if (mode == Mode.Pan)
        {
            owner.PanMapFromDragDelta(startCenterLat, startCenterLon, delta);
        }
        else
        {
            owner.ResizeFromDragDelta(mode, startAnchoredPosition, startMapSize, delta);
        }
    }
}

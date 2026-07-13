using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boeing 737 ND/EHSI 控制层。读取 JSBSim 状态并驱动 530x530 RenderTexture UI。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class B737NavigationDisplay : MonoBehaviour
{
    [Serializable]
    public class NavigationTarget
    {
        public string ident = "DUNOT";
        public B737NavigationDisplaySymbolType type = B737NavigationDisplaySymbolType.ActiveWaypoint;
        public bool show = true;
        public bool useLatLon;
        public double latitudeDeg;
        public double longitudeDeg;
        public float relativeBearingDeg;
        public float distanceNm = 12.8f;
        public string frequencyText = "";
        public bool useAsNextWaypoint;
    }

    private struct RuntimeData
    {
        public bool hasBridgeData;
        public bool hasLatLon;
        public double latDeg;
        public double lonDeg;
        public float headingMagDeg;
        public float trackMagDeg;
        public float headingBugMagDeg;
        public float courseMagDeg;
        public float groundSpeedKts;
        public float trueAirSpeedKts;
        public float windFromMagDeg;
        public float windSpeedKts;
    }

    [Header("Bridge")]
    [Tooltip("JSBSim 数据桥。留空时会自动使用 JsbsimBridge.Instance。")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private bool pollBridgeInUpdate = true;
    [SerializeField, Min(0.01f)] private float pollInterval = 0.05f;

    [Header("Canvas Geometry")]
    [SerializeField] private float canvasSize = 530f;
    [SerializeField] private Vector2 aircraftApex = new Vector2(260f, 388f);
    [SerializeField] private float arcRadius = 306f;
    [SerializeField] private float visibleArcDeg = 90f;
    [SerializeField] private float displayRangeNm = 40f;

    [Header("Top-left Coordinates")]
    [SerializeField] private Vector2 gsPosition = new Vector2(36f, 43f);
    [SerializeField] private Vector2 trackPosition = new Vector2(192f, 48f);
    [SerializeField] private Vector2 nextWaypointPosition = new Vector2(373f, 36f);
    [SerializeField] private Vector2 vorPosition = new Vector2(454f, 36f);
    [SerializeField] private Vector2 activeFeaturesPosition = new Vector2(35f, 307f);

    [Header("Navigation")]
    [Tooltip("本项目当前 JSBSim 输出为真航向；如需磁航向，在这里填本地磁差，东偏为正。")]
    [SerializeField] private float magneticVariationDeg;
    [SerializeField] private float selectedHeadingBugMagDeg = 70f;
    [SerializeField] private float selectedCourseMagDeg = 70f;
    [SerializeField] private bool courseFollowsNextWaypoint = true;
    [SerializeField] private NavigationTarget[] navigationTargets;

    [Header("JSBSim Keys")]
    [SerializeField] private string groundSpeedFpsKey = "velocities_vg_fps";
    [SerializeField] private string groundSpeedFallbackFpsKey = "velocities_ned_velocity_mag_fps";
    [SerializeField] private string groundTrackRadKey = "flight_path_psi_gt_rad";
    [SerializeField] private string windNorthFpsKey = "atmosphere_total_wind_north_fps";
    [SerializeField] private string windEastFpsKey = "atmosphere_total_wind_east_fps";
    [SerializeField] private string windMagFpsKey = "atmosphere_wind_mag_fps";
    [SerializeField] private string headingBugKey = "";
    [SerializeField] private string courseKey = "";

    [Header("Animation")]
    [SerializeField, Min(0f)] private float animationResponse = 12f;
    [SerializeField, Min(0.01f)] private float displayRefreshInterval = 1f / 30f;

    private const float FeetPerSecondToKnots = 0.5924838f;
    private const double EarthRadiusNm = 3440.065;

    private readonly List<B737NavigationDisplaySymbolSnapshot> symbolBuffer = new List<B737NavigationDisplaySymbolSnapshot>(16);
    private B737NavigationDisplaySymbolSnapshot[] symbolSnapshot = Array.Empty<B737NavigationDisplaySymbolSnapshot>();
    private RuntimeData targetData;
    private RuntimeData displayData;
    private bool dataInitialized;
    private float nextPollTime;
    private float nextDisplayRefreshTime;
    private float lastDisplayRefreshTime;
    private bool stateRefreshPending;
    private JsbsimBridge subscribedBridge;

    private RectTransform generatedRoot;
    private B737NavigationDisplayGraphic graphic;
    private Text gsText;
    private Text trackLabelText;
    private Text trackValueText;
    private Text magLabelText;
    private Text nextWaypointText;
    private Text vorText;
    private Text activeFeaturesText;
    private Text currentLocationText;
    private Text range20Text;
    private Text[] compassTexts;
    private Text[] symbolTexts;
    private Font ndFont;
    private int gsDisplay = int.MinValue;
    private int tasDisplay = int.MinValue;
    private int windDirectionDisplay = int.MinValue;
    private int windSpeedDisplay = int.MinValue;
    private int trackDisplay = int.MinValue;
    private string nextIdentDisplay;
    private int nextEtaMinuteDisplay = int.MinValue;
    private int nextDistanceDisplay = int.MinValue;
    private string vorIdentDisplay;
    private string vorFrequencyDisplay;
    private int vorDistanceDisplay = int.MinValue;
    private int[] compassLabelValues;

    public float HeadingBugMagDeg
    {
        get => selectedHeadingBugMagDeg;
        set => selectedHeadingBugMagDeg = Normalize360(value);
    }

    public float CourseMagDeg
    {
        get => selectedCourseMagDeg;
        set => selectedCourseMagDeg = Normalize360(value);
    }

    private void Reset()
    {
        EnsureDefaultTargets();
    }

    private void Awake()
    {
        RebuildDisplay();
    }

    private void OnEnable()
    {
        AttachBridge();
        RefreshFromBridge();
        nextDisplayRefreshTime = 0f;
        lastDisplayRefreshTime = 0f;
        RenderDisplay();
    }

    private void OnDisable()
    {
        if (subscribedBridge != null)
        {
            subscribedBridge.OnStateUpdated -= RequestStateRefresh;
            subscribedBridge = null;
        }
    }

    private void Update()
    {
        if (bridge == null)
        {
            AttachBridge();
        }

        bool pollDue = pollBridgeInUpdate && Time.unscaledTime >= nextPollTime;
        if (pollDue)
        {
            nextPollTime = Time.unscaledTime + pollInterval;
        }

        if (Time.unscaledTime < nextDisplayRefreshTime)
        {
            return;
        }

        float displayDeltaTime = lastDisplayRefreshTime > 0f
            ? Time.unscaledTime - lastDisplayRefreshTime
            : Time.unscaledDeltaTime;
        lastDisplayRefreshTime = Time.unscaledTime;
        nextDisplayRefreshTime = Time.unscaledTime + displayRefreshInterval;

        if (pollDue || stateRefreshPending)
        {
            stateRefreshPending = false;
            RefreshFromBridge();
        }

        AnimateData(displayDeltaTime);
        RenderDisplay();
    }

    public void SetHeadingBug(float headingMagDeg)
    {
        selectedHeadingBugMagDeg = Normalize360(headingMagDeg);
    }

    public void SetCourse(float courseMagDeg)
    {
        selectedCourseMagDeg = Normalize360(courseMagDeg);
    }

    public void RebuildDisplay()
    {
        EnsureDefaultTargets();
        BuildUi();
        ApplyDemoData();
        displayData = targetData;
        dataInitialized = true;
        RenderDisplay();
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
            subscribedBridge.OnStateUpdated -= RequestStateRefresh;
        }

        bridge = nextBridge;
        subscribedBridge = nextBridge;
        subscribedBridge.OnStateUpdated += RequestStateRefresh;
        stateRefreshPending = true;
    }

    private void RequestStateRefresh()
    {
        stateRefreshPending = true;
    }

    private void RefreshFromBridge()
    {
        if (bridge == null || !bridge.HasState)
        {
            ApplyDemoData();
            return;
        }

        float headingTrue = Normalize360(bridge.HeadingDeg);
        float headingMag = Normalize360(headingTrue - magneticVariationDeg);

        targetData.hasBridgeData = true;
        targetData.headingMagDeg = headingMag;
        targetData.trackMagDeg = ReadGroundTrackMag(headingMag);
        targetData.headingBugMagDeg = ReadOptionalHeading(headingBugKey, selectedHeadingBugMagDeg);
        targetData.groundSpeedKts = ReadGroundSpeedKts();
        targetData.trueAirSpeedKts = Mathf.Max(0f, bridge.TrueSpeedKts);
        bool hasLat = TryRead("lat_deg", out float lat);
        bool hasLon = TryRead("lon_deg", out float lon);
        targetData.hasLatLon = hasLat && hasLon;
        targetData.latDeg = lat;
        targetData.lonDeg = lon;

        ReadWind(out targetData.windFromMagDeg, out targetData.windSpeedKts);

        if (courseFollowsNextWaypoint && TryResolveNextWaypoint(targetData, out float nextBearing, out _))
        {
            targetData.courseMagDeg = nextBearing;
        }
        else
        {
            targetData.courseMagDeg = ReadOptionalHeading(courseKey, selectedCourseMagDeg);
        }
    }

    private void ApplyDemoData()
    {
        targetData.hasBridgeData = false;
        targetData.hasLatLon = false;
        targetData.headingMagDeg = 70f;
        targetData.trackMagDeg = 70f;
        targetData.headingBugMagDeg = selectedHeadingBugMagDeg;
        targetData.courseMagDeg = selectedCourseMagDeg;
        targetData.groundSpeedKts = 268f;
        targetData.trueAirSpeedKts = 264f;
        targetData.windFromMagDeg = 326f;
        targetData.windSpeedKts = 31f;
    }

    private void AnimateData(float deltaTime)
    {
        if (!dataInitialized)
        {
            displayData = targetData;
            dataInitialized = true;
            return;
        }

        float t = animationResponse <= 0f ? 1f : 1f - Mathf.Exp(-animationResponse * deltaTime);
        displayData.hasBridgeData = targetData.hasBridgeData;
        displayData.hasLatLon = targetData.hasLatLon;
        displayData.latDeg = targetData.latDeg;
        displayData.lonDeg = targetData.lonDeg;
        displayData.headingMagDeg = Mathf.LerpAngle(displayData.headingMagDeg, targetData.headingMagDeg, t);
        displayData.trackMagDeg = Mathf.LerpAngle(displayData.trackMagDeg, targetData.trackMagDeg, t);
        displayData.headingBugMagDeg = Mathf.LerpAngle(displayData.headingBugMagDeg, targetData.headingBugMagDeg, t);
        displayData.courseMagDeg = Mathf.LerpAngle(displayData.courseMagDeg, targetData.courseMagDeg, t);
        displayData.windFromMagDeg = Mathf.LerpAngle(displayData.windFromMagDeg, targetData.windFromMagDeg, t);
        displayData.groundSpeedKts = Mathf.Lerp(displayData.groundSpeedKts, targetData.groundSpeedKts, t);
        displayData.trueAirSpeedKts = Mathf.Lerp(displayData.trueAirSpeedKts, targetData.trueAirSpeedKts, t);
        displayData.windSpeedKts = Mathf.Lerp(displayData.windSpeedKts, targetData.windSpeedKts, t);
    }

    private void RenderDisplay()
    {
        BuildSymbolSnapshots(displayData);
        UpdateTexts();
        UpdateCompassLabels();
        UpdateSymbolLabels();

        if (graphic != null)
        {
            CopySymbolSnapshot();
            graphic.ConfigureGeometry(canvasSize, aircraftApex, arcRadius, visibleArcDeg);
            graphic.SetState(new B737NavigationDisplayState
            {
                HeadingMagDeg = displayData.headingMagDeg,
                TrackMagDeg = displayData.trackMagDeg,
                HeadingBugMagDeg = displayData.headingBugMagDeg,
                CourseMagDeg = displayData.courseMagDeg,
                WindFromMagDeg = displayData.windFromMagDeg,
                WindSpeedKts = displayData.windSpeedKts,
                DisplayRangeNm = displayRangeNm,
                Symbols = symbolSnapshot
            });
        }
    }

    private void CopySymbolSnapshot()
    {
        if (symbolSnapshot.Length != symbolBuffer.Count)
        {
            symbolSnapshot = new B737NavigationDisplaySymbolSnapshot[symbolBuffer.Count];
        }

        symbolBuffer.CopyTo(symbolSnapshot);
    }

    private void UpdateTexts()
    {
        if (gsText != null)
        {
            int nextGs = Mathf.RoundToInt(displayData.groundSpeedKts);
            int nextTas = Mathf.RoundToInt(displayData.trueAirSpeedKts);
            int nextWindDirection = Mathf.RoundToInt(Normalize360(displayData.windFromMagDeg));
            int nextWindSpeed = Mathf.RoundToInt(displayData.windSpeedKts);
            if (nextGs != gsDisplay
                || nextTas != tasDisplay
                || nextWindDirection != windDirectionDisplay
                || nextWindSpeed != windSpeedDisplay)
            {
                gsDisplay = nextGs;
                tasDisplay = nextTas;
                windDirectionDisplay = nextWindDirection;
                windSpeedDisplay = nextWindSpeed;
                gsText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "<color=#a9b0b7>GS</color>{0:000}  <color=#a9b0b7>TAS</color>{1:000}\n{2:000}\u00b0/{3:00}",
                    nextGs,
                    nextTas,
                    nextWindDirection,
                    nextWindSpeed);
            }
        }

        int nextTrack = Mathf.RoundToInt(Normalize360(displayData.trackMagDeg));
        if (trackValueText != null && nextTrack != trackDisplay)
        {
            trackDisplay = nextTrack;
            trackValueText.text = nextTrack.ToString("000", CultureInfo.InvariantCulture);
        }

        NavigationTarget next = FindNextWaypoint();
        if (nextWaypointText != null)
        {
            string ident = next != null ? next.ident : "NO WPT";
            float distance = 0f;
            TryResolveNextWaypoint(displayData, out _, out distance);
            int etaMinute = next != null ? CalculateZuluEtaMinute(distance, displayData.groundSpeedKts) : -1;
            int distanceTenths = Mathf.RoundToInt(distance * 10f);
            if (ident != nextIdentDisplay
                || etaMinute != nextEtaMinuteDisplay
                || distanceTenths != nextDistanceDisplay)
            {
                nextIdentDisplay = ident;
                nextEtaMinuteDisplay = etaMinute;
                nextDistanceDisplay = distanceTenths;
                nextWaypointText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "<color=#e24dba>{0}</color>\n<color=#d8dde0>{1}</color>\n<color=#d8dde0>{2:0.0}NM</color>",
                    ident,
                    FormatZuluEta(etaMinute),
                    distanceTenths * 0.1f);
            }
        }

        NavigationTarget vor = FindTunedVor();
        if (vorText != null)
        {
            float distance = 111f;
            if (vor != null && TryResolveTarget(vor, displayData, out _, out float resolvedDistance))
            {
                distance = resolvedDistance;
            }

            string ident = vor != null ? vor.ident : "VOR 1";
            string freq = vor != null && !string.IsNullOrWhiteSpace(vor.frequencyText) ? vor.frequencyText : "114.20";
            int distanceRounded = Mathf.RoundToInt(distance);
            if (ident != vorIdentDisplay
                || freq != vorFrequencyDisplay
                || distanceRounded != vorDistanceDisplay)
            {
                vorIdentDisplay = ident;
                vorFrequencyDisplay = freq;
                vorDistanceDisplay = distanceRounded;
                vorText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "<color=#7fd65f>{0}</color>\n<color=#7fd65f>{1}</color>\n<color=#7fd65f>DME {2:0}</color>",
                    ident,
                    freq,
                    distanceRounded);
            }
        }

        SetText(activeFeaturesText, "<color=#4fa8e8>ARPT\nWPT\nSTA\nWXR\n-2A</color>");
        SetText(currentLocationText, "0.0");
        SetText(range20Text, "20");
    }

    private void UpdateCompassLabels()
    {
        if (compassTexts == null)
        {
            return;
        }

        if (compassLabelValues == null || compassLabelValues.Length != compassTexts.Length)
        {
            compassLabelValues = new int[compassTexts.Length];
            for (int i = 0; i < compassLabelValues.Length; i++)
            {
                compassLabelValues[i] = int.MinValue;
            }
        }

        float halfArc = visibleArcDeg * 0.5f;
        float first = Mathf.Floor((displayData.headingMagDeg - halfArc) / 30f) * 30f;
        int index = 0;

        for (float heading = first; heading <= displayData.headingMagDeg + halfArc + 30f && index < compassTexts.Length; heading += 30f)
        {
            float normalizedHeading = Normalize360(heading);
            float rel = Mathf.DeltaAngle(displayData.headingMagDeg, normalizedHeading);
            if (Mathf.Abs(rel) > halfArc + 0.5f)
            {
                continue;
            }

            Vector2 p = PointFromBearing(rel, arcRadius - 34f);
            int labelIndex = index++;
            Text label = compassTexts[labelIndex];
            if (!label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(true);
            }
            int labelValue = GetCompassLabelValue(normalizedHeading);
            if (compassLabelValues[labelIndex] != labelValue)
            {
                compassLabelValues[labelIndex] = labelValue;
                label.text = labelValue.ToString(CultureInfo.InvariantCulture);
            }
            SetTopLeft(label.rectTransform, p.x - 15f, p.y - 10f, 30f, 20f);
        }

        for (; index < compassTexts.Length; index++)
        {
            if (compassTexts[index].gameObject.activeSelf)
            {
                compassTexts[index].gameObject.SetActive(false);
            }
        }
    }

    private void UpdateSymbolLabels()
    {
        if (symbolTexts == null)
        {
            return;
        }

        int index = 0;
        for (int i = 0; i < symbolBuffer.Count && index < symbolTexts.Length; i++)
        {
            B737NavigationDisplaySymbolSnapshot symbol = symbolBuffer[i];
            if (!TryProject(symbol.BearingMagDeg, symbol.DistanceNm, out Vector2 p))
            {
                continue;
            }

            Text label = symbolTexts[index++];
            if (!label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(true);
            }
            SetText(label, symbol.Ident);
            Color symbolColor = ColorForSymbol(symbol.Type);
            if (label.color != symbolColor)
            {
                label.color = symbolColor;
            }
            SetTopLeft(label.rectTransform, p.x + 9f, p.y - 7f, 70f, 20f);
        }

        for (; index < symbolTexts.Length; index++)
        {
            if (symbolTexts[index].gameObject.activeSelf)
            {
                symbolTexts[index].gameObject.SetActive(false);
            }
        }
    }

    private void BuildSymbolSnapshots(RuntimeData data)
    {
        symbolBuffer.Clear();
        if (navigationTargets == null)
        {
            return;
        }

        for (int i = 0; i < navigationTargets.Length; i++)
        {
            NavigationTarget target = navigationTargets[i];
            if (target == null || !target.show || string.IsNullOrWhiteSpace(target.ident))
            {
                continue;
            }

            if (!TryResolveTarget(target, data, out float bearingMag, out float distanceNm))
            {
                continue;
            }

            B737NavigationDisplaySymbolType type = target.type;
            bool active = target.useAsNextWaypoint || type == B737NavigationDisplaySymbolType.ActiveWaypoint;
            if (active)
            {
                type = B737NavigationDisplaySymbolType.ActiveWaypoint;
            }

            symbolBuffer.Add(new B737NavigationDisplaySymbolSnapshot
            {
                Ident = target.ident,
                Type = type,
                BearingMagDeg = bearingMag,
                DistanceNm = distanceNm,
                IsActive = active
            });
        }
    }

    private bool TryResolveNextWaypoint(RuntimeData data, out float bearingMag, out float distanceNm)
    {
        NavigationTarget next = FindNextWaypoint();
        if (next != null)
        {
            return TryResolveTarget(next, data, out bearingMag, out distanceNm);
        }

        bearingMag = Normalize360(data.headingMagDeg);
        distanceNm = 0f;
        return false;
    }

    private bool TryResolveTarget(NavigationTarget target, RuntimeData data, out float bearingMag, out float distanceNm)
    {
        bearingMag = 0f;
        distanceNm = 0f;

        if (target == null)
        {
            return false;
        }

        if (target.useLatLon && data.hasLatLon)
        {
            CalculateBearingDistance(data.latDeg, data.lonDeg, target.latitudeDeg, target.longitudeDeg, out bearingMag, out distanceNm);
            bearingMag = Normalize360(bearingMag - magneticVariationDeg);
            return true;
        }

        bearingMag = Normalize360(data.headingMagDeg + target.relativeBearingDeg);
        distanceNm = Mathf.Max(0f, target.distanceNm);
        return true;
    }

    private float ReadGroundSpeedKts()
    {
        if (TryRead(groundSpeedFpsKey, out float fps))
        {
            return Mathf.Max(0f, fps * FeetPerSecondToKnots);
        }

        if (TryRead(groundSpeedFallbackFpsKey, out fps))
        {
            return Mathf.Max(0f, fps * FeetPerSecondToKnots);
        }

        if (TryRead("velocities_v_north_fps", out float north) && TryRead("velocities_v_east_fps", out float east))
        {
            return Mathf.Sqrt(north * north + east * east) * FeetPerSecondToKnots;
        }

        return bridge != null ? Mathf.Max(0f, bridge.SpeedKts) : 0f;
    }

    private float ReadGroundTrackMag(float fallbackHeadingMag)
    {
        if (TryRead(groundTrackRadKey, out float trackRad))
        {
            return Normalize360(trackRad * Mathf.Rad2Deg - magneticVariationDeg);
        }

        if (TryRead("velocities_v_north_fps", out float north) && TryRead("velocities_v_east_fps", out float east) &&
            Mathf.Abs(north) + Mathf.Abs(east) > 0.01f)
        {
            return Normalize360(Mathf.Atan2(east, north) * Mathf.Rad2Deg - magneticVariationDeg);
        }

        return fallbackHeadingMag;
    }

    private void ReadWind(out float windFromMagDeg, out float windSpeedKts)
    {
        bool hasNorth = TryRead(windNorthFpsKey, out float north);
        bool hasEast = TryRead(windEastFpsKey, out float east);

        if (!hasNorth || !hasEast)
        {
            TryRead("atmosphere_wind_north_fps", out north);
            TryRead("atmosphere_wind_east_fps", out east);
        }

        float componentSpeed = Mathf.Sqrt(north * north + east * east) * FeetPerSecondToKnots;
        windSpeedKts = componentSpeed;
        if (TryRead(windMagFpsKey, out float magFps) && magFps > 0.1f)
        {
            windSpeedKts = magFps * FeetPerSecondToKnots;
        }

        if (componentSpeed > 0.5f)
        {
            float windToTrue = Mathf.Atan2(east, north) * Mathf.Rad2Deg;
            windFromMagDeg = Normalize360(windToTrue + 180f - magneticVariationDeg);
        }
        else if (TryRead("ic_vw_dir_deg", out float dirDeg))
        {
            windFromMagDeg = Normalize360(dirDeg - magneticVariationDeg);
        }
        else
        {
            windFromMagDeg = targetData.headingMagDeg;
        }
    }

    private float ReadOptionalHeading(string key, float fallback)
    {
        return TryRead(key, out float value) ? Normalize360(value) : Normalize360(fallback);
    }

    private bool TryRead(string key, out float value)
    {
        value = 0f;
        return bridge != null && !string.IsNullOrWhiteSpace(key) && bridge.TryGetValue(key, out value);
    }

    private void BuildUi()
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(canvasSize, canvasSize);

        Transform existing = transform.Find("ND_UI_Generated");
        if (existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        GameObject rootGo = new GameObject("ND_UI_Generated", typeof(RectTransform));
        rootGo.layer = gameObject.layer;
        rootGo.transform.SetParent(transform, false);
        generatedRoot = rootGo.GetComponent<RectTransform>();
        generatedRoot.anchorMin = new Vector2(0.5f, 0.5f);
        generatedRoot.anchorMax = new Vector2(0.5f, 0.5f);
        generatedRoot.pivot = new Vector2(0.5f, 0.5f);
        generatedRoot.anchoredPosition = Vector2.zero;
        generatedRoot.sizeDelta = new Vector2(canvasSize, canvasSize);

        graphic = rootGo.AddComponent<B737NavigationDisplayGraphic>();
        graphic.raycastTarget = false;
        graphic.ConfigureGeometry(canvasSize, aircraftApex, arcRadius, visibleArcDeg);

        ndFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Consolas", "Bahnschrift", "Microsoft YaHei", "Arial" }, 18);

        CreateBox("GS_Box", gsPosition.x - 8f, gsPosition.y - 10f, 140f, 74f);
        CreateBox("TRK_Box", trackPosition.x + 24f, trackPosition.y - 11f, 58f, 29f);
        CreateBox("WPT_Box", nextWaypointPosition.x - 9f, nextWaypointPosition.y - 10f, 86f, 73f);
        CreateBox("VOR_Box", vorPosition.x - 8f, vorPosition.y - 10f, 70f, 74f);

        gsText = CreateText("GS_TAS_Wind", gsPosition.x, gsPosition.y, 150f, 70f, 18, new Color32(224, 229, 231, 255), TextAnchor.UpperLeft);
        trackLabelText = CreateText("TRK_Label", trackPosition.x, trackPosition.y, 38f, 22f, 17, new Color32(112, 216, 94, 255), TextAnchor.UpperLeft);
        trackValueText = CreateText("TRK_Value", trackPosition.x + 31f, trackPosition.y - 5f, 54f, 28f, 22, Color.white, TextAnchor.MiddleCenter);
        magLabelText = CreateText("MAG_Label", trackPosition.x + 92f, trackPosition.y, 48f, 22f, 17, new Color32(112, 216, 94, 255), TextAnchor.UpperLeft);
        nextWaypointText = CreateText("Next_Waypoint", nextWaypointPosition.x, nextWaypointPosition.y, 88f, 72f, 15, Color.white, TextAnchor.UpperLeft);
        vorText = CreateText("VOR1_Info", vorPosition.x, vorPosition.y, 75f, 72f, 14, new Color32(128, 214, 96, 255), TextAnchor.UpperLeft);
        activeFeaturesText = CreateText("Active_Features", activeFeaturesPosition.x, activeFeaturesPosition.y, 58f, 96f, 15, new Color32(79, 168, 232, 255), TextAnchor.UpperLeft);
        currentLocationText = CreateText("Current_Location", aircraftApex.x - 12f, aircraftApex.y + 52f, 36f, 18f, 12, Color.white, TextAnchor.UpperCenter);
        range20Text = CreateText("Range_20", aircraftApex.x - 35f, aircraftApex.y - arcRadius * 0.5f - 10f, 30f, 18f, 15, new Color32(210, 225, 212, 255), TextAnchor.MiddleCenter);

        trackLabelText.text = "TRK";
        magLabelText.text = "MAG";

        compassTexts = new Text[8];
        compassLabelValues = new int[compassTexts.Length];
        for (int i = 0; i < compassTexts.Length; i++)
        {
            compassTexts[i] = CreateText("Compass_Label_" + i, 0f, 0f, 32f, 20f, 18, Color.white, TextAnchor.MiddleCenter);
            compassLabelValues[i] = int.MinValue;
            compassTexts[i].gameObject.SetActive(false);
        }

        symbolTexts = new Text[16];
        for (int i = 0; i < symbolTexts.Length; i++)
        {
            symbolTexts[i] = CreateText("Symbol_Label_" + i, 0f, 0f, 80f, 20f, 13, Color.white, TextAnchor.UpperLeft);
            symbolTexts[i].gameObject.SetActive(false);
        }
    }

    private Text CreateText(string name, float x, float y, float w, float h, int fontSize, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(generatedRoot, false);
        Text text = go.AddComponent<Text>();
        text.font = ndFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1f, -1f);

        SetTopLeft(text.rectTransform, x, y, w, h);
        return text;
    }

    private void CreateBox(string name, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(generatedRoot, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.62f);
        image.raycastTarget = false;
        SetTopLeft(image.rectTransform, x, y, w, h);
    }

    private void EnsureDefaultTargets()
    {
        if (navigationTargets != null && navigationTargets.Length > 0)
        {
            return;
        }

        navigationTargets = new[]
        {
            new NavigationTarget
            {
                ident = "DUNOT",
                type = B737NavigationDisplaySymbolType.ActiveWaypoint,
                relativeBearingDeg = 0f,
                distanceNm = 12.8f,
                useAsNextWaypoint = true
            },
            new NavigationTarget
            {
                ident = "DECEL",
                type = B737NavigationDisplaySymbolType.Waypoint,
                relativeBearingDeg = 0f,
                distanceNm = 20f
            },
            new NavigationTarget
            {
                ident = "EXXES",
                type = B737NavigationDisplaySymbolType.Waypoint,
                relativeBearingDeg = 34f,
                distanceNm = 25f
            },
            new NavigationTarget
            {
                ident = "EBCI",
                type = B737NavigationDisplaySymbolType.Airport,
                relativeBearingDeg = -38f,
                distanceNm = 24f
            },
            new NavigationTarget
            {
                ident = "CRI",
                type = B737NavigationDisplaySymbolType.Vor,
                relativeBearingDeg = -13f,
                distanceNm = 17f,
                frequencyText = "114.20"
            },
            new NavigationTarget
            {
                ident = "VOR 1",
                type = B737NavigationDisplaySymbolType.Vor,
                relativeBearingDeg = 28f,
                distanceNm = 31f,
                frequencyText = "114.20"
            }
        };
    }

    private NavigationTarget FindNextWaypoint()
    {
        if (navigationTargets == null)
        {
            return null;
        }

        for (int i = 0; i < navigationTargets.Length; i++)
        {
            NavigationTarget target = navigationTargets[i];
            if (target != null && target.show && target.useAsNextWaypoint)
            {
                return target;
            }
        }

        for (int i = 0; i < navigationTargets.Length; i++)
        {
            NavigationTarget target = navigationTargets[i];
            if (target != null && target.show &&
                (target.type == B737NavigationDisplaySymbolType.ActiveWaypoint ||
                 target.type == B737NavigationDisplaySymbolType.Waypoint))
            {
                return target;
            }
        }

        return null;
    }

    private NavigationTarget FindFirstTarget(B737NavigationDisplaySymbolType type)
    {
        if (navigationTargets == null)
        {
            return null;
        }

        for (int i = 0; i < navigationTargets.Length; i++)
        {
            NavigationTarget target = navigationTargets[i];
            if (target != null && target.show && target.type == type)
            {
                return target;
            }
        }

        return null;
    }

    private NavigationTarget FindTunedVor()
    {
        if (navigationTargets == null)
        {
            return null;
        }

        for (int i = 0; i < navigationTargets.Length; i++)
        {
            NavigationTarget target = navigationTargets[i];
            if (target != null && target.show && target.type == B737NavigationDisplaySymbolType.Vor &&
                !string.IsNullOrWhiteSpace(target.ident) &&
                target.ident.Equals("VOR 1", StringComparison.OrdinalIgnoreCase))
            {
                return target;
            }
        }

        return FindFirstTarget(B737NavigationDisplaySymbolType.Vor);
    }

    private bool TryProject(float bearingMagDeg, float distanceNm, out Vector2 pos)
    {
        float range = Mathf.Max(5f, displayRangeNm);
        float rel = Mathf.DeltaAngle(displayData.headingMagDeg, bearingMagDeg);
        float radius = arcRadius * Mathf.Clamp(distanceNm / range, 0f, 1.25f);
        pos = PointFromBearing(rel, radius);
        return pos.x >= -20f && pos.x <= canvasSize + 20f && pos.y >= 74f && pos.y <= canvasSize + 20f;
    }

    private Vector2 PointFromBearing(float relativeBearingDeg, float radius)
    {
        float rad = relativeBearingDeg * Mathf.Deg2Rad;
        return aircraftApex + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * radius;
    }

    private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        Vector2 topLeft = new Vector2(0f, 1f);
        Vector2 position = new Vector2(x, -y);
        Vector2 size = new Vector2(w, h);
        if (rt.anchorMin != topLeft) rt.anchorMin = topLeft;
        if (rt.anchorMax != topLeft) rt.anchorMax = topLeft;
        if (rt.pivot != topLeft) rt.pivot = topLeft;
        if ((rt.anchoredPosition - position).sqrMagnitude > 0.0001f) rt.anchoredPosition = position;
        if (rt.sizeDelta != size) rt.sizeDelta = size;
    }

    private static void SetText(Text text, string value)
    {
        if (text != null && text.text != value)
        {
            text.text = value;
        }
    }

    private static Color ColorForSymbol(B737NavigationDisplaySymbolType type)
    {
        switch (type)
        {
            case B737NavigationDisplaySymbolType.ActiveWaypoint:
                return new Color32(226, 77, 186, 255);
            case B737NavigationDisplaySymbolType.Airport:
            case B737NavigationDisplaySymbolType.Vor:
                return new Color32(79, 168, 232, 255);
            default:
                return new Color32(119, 215, 93, 255);
        }
    }

    private static int GetCompassLabelValue(float headingMagDeg)
    {
        int value = Mathf.RoundToInt(Normalize360(headingMagDeg) / 10f);
        if (value == 0)
        {
            value = 36;
        }

        return value;
    }

    private static int CalculateZuluEtaMinute(float distanceNm, float groundSpeedKts)
    {
        if (groundSpeedKts < 1f || distanceNm <= 0f)
        {
            return -1;
        }

        DateTime eta = DateTime.UtcNow.AddHours(distanceNm / groundSpeedKts);
        return eta.Hour * 60 + eta.Minute;
    }

    private static string FormatZuluEta(int minuteOfDay)
    {
        return minuteOfDay < 0
            ? "----.-z"
            : string.Format(CultureInfo.InvariantCulture, "{0:00}{1:00}.0z", minuteOfDay / 60, minuteOfDay % 60);
    }

    private static void CalculateBearingDistance(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg, out float bearingDeg, out float distanceNm)
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
        distanceNm = Mathf.Max(0f, (float)(EarthRadiusNm * c));
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
}

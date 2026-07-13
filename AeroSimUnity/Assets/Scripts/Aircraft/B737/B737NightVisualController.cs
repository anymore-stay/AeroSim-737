using System;
using System.Collections.Generic;
using CesiumForUnity;
using UniStorm;
using UnityEngine;

/// <summary>
/// 统一压低夜间全局环境和地表亮度，同时保留飞机灯光、仪表和星空可见。
/// </summary>
[DefaultExecutionOrder(320)]
public class B737NightVisualController : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int CesiumBaseColorFactorId = Shader.PropertyToID("_baseColorFactor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    private static readonly int NightSkyTintId = Shader.PropertyToID("_NightSkyTint");

    [Header("引用")]
    [SerializeField] private UniStormSystem uniStormSystem;
    [SerializeField] private Renderer starsRenderer;
    [SerializeField] private Transform[] worldDarkeningRoots = Array.Empty<Transform>();
    [SerializeField] private Transform[] airportDarkeningRoots = Array.Empty<Transform>();
    [SerializeField] private Transform[] excludedRoots = Array.Empty<Transform>();

    [Header("夜晚时间")]
    [SerializeField, Range(0f, 24f)] private float eveningTransitionStartHour = 17f;
    [SerializeField, Range(0f, 24f)] private float nightStartHour = 19f;
    [SerializeField, Range(0f, 24f)] private float surfaceDarkeningStartHour = 19f;
    [SerializeField, Range(0f, 24f)] private float surfaceFullDarkHour = 21.5f;
    [SerializeField, Range(0f, 24f)] private float nightEndHour = 5.5f;
    [SerializeField, Min(0f)] private float transitionHours = 0.75f;

    [Header("目标图初始参数")]
    [SerializeField, Range(0f, 1f)] private float ambientIntensity = 0.16f;
    [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 0.075f;
    [SerializeField] private Color ambientSkyColor = new Color(0.045f, 0.052f, 0.065f, 1f);
    [SerializeField] private Color ambientEquatorColor = new Color(0.035f, 0.04f, 0.05f, 1f);
    [SerializeField] private Color ambientGroundColor = new Color(0.024f, 0.026f, 0.032f, 1f);
    [SerializeField] private Color skyTintColor = new Color(0.018f, 0.022f, 0.032f, 1f);

    [Header("UniStorm 夜间光")]
    [SerializeField, Range(0f, 1f)] private float maximumMoonLightIntensity = 0.05f;
    [SerializeField, Range(0f, 2f)] private float maximumMoonAtmosphericFogIntensity = 0f;
    [SerializeField, Range(0f, 2f)] private float maximumSunAtmosphericFogIntensity = 0f;
    [SerializeField, Range(0f, 3f)] private float starBrightnessMultiplier = 1.35f;

    [Header("世界地表压暗")]
    [SerializeField, Range(0f, 1f)] private float worldSurfaceBrightness = 0.24f;
    [SerializeField, Min(0.1f)] private float rendererRefreshInterval = 2f;
    [SerializeField, Min(0.05f)] private float nightRendererScanInterval = 1f;
    [SerializeField, Min(1)] private int maximumNewRenderersPerScan = 256;
    [SerializeField, Min(1)] private int maximumNewAirportRenderersPerScan = 1024;
    [SerializeField, Min(1)] private int maximumNewCesiumRenderersPerScan = 512;
    [SerializeField] private Cesium3DTileset[] cesiumTilesets = Array.Empty<Cesium3DTileset>();
    [SerializeField] private string[] excludedNameContains =
    {
        "B737",
        "UniStorm",
        "Light",
        "Lamp",
        "RunwayLight",
        "TaxiLight",
        "PAPI",
        "Beacon",
        "Strobe",
        "NAV",
        "EICAS",
        "EICAS1_Canvas",
        "EICAS2_Canvas",
        "EICAS1_Plane",
        "EICAS2_Plane",
        "Clock",
        "Clock_Canvas",
        "Clock1_Plane",
        "Clock2_Plane",
        "灯"
    };
    [SerializeField] private string[] airportNameContains =
    {
        "Airport",
        "beijing-daxing",
        "6986",
        "JC-",
        "Runway_Physics_Surface",
        "航站楼",
        "塔台",
        "停车场",
        "高速路"
    };

    private readonly List<Renderer> cachedWorldRenderers = new List<Renderer>();
    private readonly Dictionary<int, Renderer> cachedWorldRendererById = new Dictionary<int, Renderer>();
    private readonly List<int> worldRendererIdsToRemove = new List<int>();
    private readonly List<Transform> worldRendererScanStack = new List<Transform>();
    private readonly List<Renderer> cachedAirportRenderers = new List<Renderer>();
    private readonly Dictionary<int, Renderer> cachedAirportRendererById = new Dictionary<int, Renderer>();
    private readonly List<int> airportRendererIdsToRemove = new List<int>();
    private readonly List<Transform> resolvedAirportRootBuffer = new List<Transform>();
    private readonly List<Transform> discoveredAirportRoots = new List<Transform>();
    private readonly List<Renderer> cachedCesiumRenderers = new List<Renderer>();
    private readonly Dictionary<int, Renderer> cachedCesiumRendererById = new Dictionary<int, Renderer>();
    private readonly List<int> cesiumRendererIdsToRemove = new List<int>();
    private readonly List<Transform> cesiumRendererScanStack = new List<Transform>();
    private readonly HashSet<Cesium3DTileset> subscribedCesiumTilesets = new HashSet<Cesium3DTileset>();
    private MaterialPropertyBlock propertyBlock;
    private float nextRendererRefreshTime;
    private float nextCesiumRendererRefreshTime;
    private Color lastSourceStarColor;
    private Color lastAppliedStarColor;
    private bool hasLastAppliedStarColor;
    private float lastAppliedWorldBrightness = -1f;
    private bool hasScannedSceneAirportRoots;
    private bool hasOriginalRenderSettings;
    private float originalAmbientIntensity;
    private float originalReflectionIntensity;
    private Color originalAmbientSkyColor;
    private Color originalAmbientEquatorColor;
    private Color originalAmbientGroundColor;
    private Material cachedSkyboxMaterial;
    private Color originalSkyTintColor;
    private Color originalNightSkyTintColor;
    private bool hasOriginalSkyTintColor;
    private bool hasOriginalNightSkyTintColor;
    private bool isApplyingNightEnvironment;

    private void Awake()
    {
        EnsurePropertyBlock();
        ResolveReferences();
        CaptureOriginalRenderSettings();
        SubscribeToCesiumTilesets();
        ApplyCesiumTilesetNaturalLighting(GetCurrentNightBlend());
        RebuildWorldRendererCache();
    }

    private void OnEnable()
    {
        EnsurePropertyBlock();
        ResolveReferences();
        CaptureOriginalRenderSettings();
        SubscribeToCesiumTilesets();
        ApplyCesiumTilesetNaturalLighting(GetCurrentNightBlend());
        RebuildWorldRendererCache();
    }

    private void OnDisable()
    {
        RestoreCesiumTilesets();
        RestoreCachedWorldRenderers();
        RestoreRenderSettings();
        isApplyingNightEnvironment = false;
    }

    private void LateUpdate()
    {
        ResolveReferences();

        float hour = GetCurrentHour();
        float nightBlend = CalculateNightVisualBlend(
            hour,
            eveningTransitionStartHour,
            nightStartHour,
            nightEndHour,
            transitionHours);

        ApplyEnvironment(nightBlend);
        ApplyUniStormNightLight(nightBlend);

        float surfaceBlend = CalculateNightSurfaceBlend(
            hour,
            surfaceDarkeningStartHour,
            surfaceFullDarkHour,
            nightEndHour,
            transitionHours);
        float brightness = CalculateWorldBrightness(surfaceBlend);
        ApplyCesiumTilesetNaturalLighting(nightBlend);

        if (ShouldScanWorldRenderers(
                Time.unscaledTime,
                nextRendererRefreshTime))
        {
            ScanForNewAirportRenderers(maximumNewAirportRenderersPerScan);
            ScanForNewWorldRenderers(brightness);
            nextRendererRefreshTime = Time.unscaledTime + GetNextWorldRendererScanInterval(
                nightBlend,
                nightRendererScanInterval,
                rendererRefreshInterval);
        }

        if (!Mathf.Approximately(lastAppliedWorldBrightness, brightness))
        {
            ApplyWorldBrightnessTo(cachedWorldRenderers, brightness);
            lastAppliedWorldBrightness = brightness;
        }
    }

    private void ResolveReferences()
    {
        if (uniStormSystem == null)
        {
            uniStormSystem = GetComponent<UniStormSystem>();
            if (uniStormSystem == null)
            {
                uniStormSystem = FindFirstObjectByType<UniStormSystem>();
            }
        }

        if (starsRenderer == null)
        {
            GameObject starsObject = GameObject.Find("UniStorm Stars");
            starsRenderer = starsObject != null ? starsObject.GetComponent<Renderer>() : null;
        }

        if (cesiumTilesets == null || cesiumTilesets.Length == 0)
        {
            cesiumTilesets = FindObjectsByType<Cesium3DTileset>(FindObjectsSortMode.None);
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private float GetCurrentHour()
    {
        if (uniStormSystem == null)
        {
            return DateTime.Now.Hour + DateTime.Now.Minute / 60f;
        }

        return Mathf.Repeat(uniStormSystem.Hour + uniStormSystem.Minute / 60f, 24f);
    }

    private float GetCurrentWorldBrightness()
    {
        float surfaceBlend = GetCurrentSurfaceBlend();
        return CalculateWorldBrightness(surfaceBlend);
    }

    private float GetCurrentNightBlend()
    {
        return CalculateNightVisualBlend(
            GetCurrentHour(),
            eveningTransitionStartHour,
            nightStartHour,
            nightEndHour,
            transitionHours);
    }

    private float GetCurrentSurfaceBlend()
    {
        return CalculateNightSurfaceBlend(
            GetCurrentHour(),
            surfaceDarkeningStartHour,
            surfaceFullDarkHour,
            nightEndHour,
            transitionHours);
    }

    private float CalculateWorldBrightness(float nightBlend)
    {
        return CalculateSurfaceBrightness(nightBlend, worldSurfaceBrightness);
    }

    private void ApplyEnvironment(float nightBlend)
    {
        if (nightBlend <= 0f)
        {
            if (isApplyingNightEnvironment)
            {
                RestoreRenderSettings();
                isApplyingNightEnvironment = false;
            }

            return;
        }

        CaptureOriginalRenderSettings();
        isApplyingNightEnvironment = true;
        RenderSettings.ambientIntensity = CalculateNightEnvironmentValue(originalAmbientIntensity, ambientIntensity, nightBlend);
        RenderSettings.reflectionIntensity = CalculateNightEnvironmentValue(originalReflectionIntensity, reflectionIntensity, nightBlend);
        RenderSettings.ambientSkyColor = CalculateNightEnvironmentColor(originalAmbientSkyColor, ambientSkyColor, nightBlend);
        RenderSettings.ambientEquatorColor = CalculateNightEnvironmentColor(originalAmbientEquatorColor, ambientEquatorColor, nightBlend);
        RenderSettings.ambientGroundColor = CalculateNightEnvironmentColor(originalAmbientGroundColor, ambientGroundColor, nightBlend);

        Material skybox = RenderSettings.skybox;
        if (skybox == null)
        {
            return;
        }

        CaptureOriginalSkyboxColors(skybox);
        if (skybox.HasProperty(SkyTintId))
        {
            skybox.SetColor(SkyTintId, CalculateNightEnvironmentColor(originalSkyTintColor, skyTintColor, nightBlend));
        }
        if (skybox.HasProperty(NightSkyTintId))
        {
            skybox.SetColor(NightSkyTintId, CalculateNightEnvironmentColor(originalNightSkyTintColor, skyTintColor, nightBlend));
        }
    }

    private void CaptureOriginalRenderSettings()
    {
        if (hasOriginalRenderSettings)
        {
            return;
        }

        hasOriginalRenderSettings = true;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        originalAmbientSkyColor = RenderSettings.ambientSkyColor;
        originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        originalAmbientGroundColor = RenderSettings.ambientGroundColor;
        CaptureOriginalSkyboxColors(RenderSettings.skybox);
    }

    private void CaptureOriginalSkyboxColors(Material skybox)
    {
        if (skybox == null || cachedSkyboxMaterial == skybox)
        {
            return;
        }

        cachedSkyboxMaterial = skybox;
        hasOriginalSkyTintColor = skybox.HasProperty(SkyTintId);
        hasOriginalNightSkyTintColor = skybox.HasProperty(NightSkyTintId);
        originalSkyTintColor = hasOriginalSkyTintColor ? skybox.GetColor(SkyTintId) : Color.white;
        originalNightSkyTintColor = hasOriginalNightSkyTintColor ? skybox.GetColor(NightSkyTintId) : Color.white;
    }

    private void RestoreRenderSettings()
    {
        if (!hasOriginalRenderSettings)
        {
            return;
        }

        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
        RenderSettings.ambientSkyColor = originalAmbientSkyColor;
        RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
        RenderSettings.ambientGroundColor = originalAmbientGroundColor;

        Material skybox = cachedSkyboxMaterial;
        if (skybox == null)
        {
            return;
        }

        if (hasOriginalSkyTintColor && skybox.HasProperty(SkyTintId))
        {
            skybox.SetColor(SkyTintId, originalSkyTintColor);
        }
        if (hasOriginalNightSkyTintColor && skybox.HasProperty(NightSkyTintId))
        {
            skybox.SetColor(NightSkyTintId, originalNightSkyTintColor);
        }
    }

    private void ApplyUniStormNightLight(float nightBlend)
    {
        if (nightBlend <= 0f || uniStormSystem == null)
        {
            return;
        }

        if (uniStormSystem.m_MoonLight != null)
        {
            uniStormSystem.m_MoonLight.intensity = ClampNightLightIntensity(
                uniStormSystem.m_MoonLight.intensity,
                maximumMoonLightIntensity,
                nightBlend);
        }

        if (uniStormSystem.m_UniStormAtmosphericFog != null)
        {
            uniStormSystem.m_UniStormAtmosphericFog.MoonIntensity = ClampNightLightIntensity(
                uniStormSystem.m_UniStormAtmosphericFog.MoonIntensity,
                maximumMoonAtmosphericFogIntensity,
                nightBlend);
            uniStormSystem.m_UniStormAtmosphericFog.SunIntensity = ClampNightLightIntensity(
                uniStormSystem.m_UniStormAtmosphericFog.SunIntensity,
                maximumSunAtmosphericFogIntensity,
                nightBlend);
        }

        if (starsRenderer != null)
        {
            Material starsMaterial = starsRenderer.material;
            Color sourceColor = ResolveStarSourceColor(
                starsMaterial.color,
                lastSourceStarColor,
                lastAppliedStarColor,
                hasLastAppliedStarColor);
            Color appliedColor = CalculateStarColor(sourceColor, starBrightnessMultiplier, nightBlend);
            starsMaterial.color = appliedColor;
            lastSourceStarColor = sourceColor;
            lastAppliedStarColor = appliedColor;
            hasLastAppliedStarColor = true;
        }
    }

    private void RebuildWorldRendererCache()
    {
        RestoreCachedWorldRenderers();
        cachedWorldRenderers.Clear();
        cachedWorldRendererById.Clear();
        cachedAirportRenderers.Clear();
        cachedAirportRendererById.Clear();
        lastAppliedWorldBrightness = -1f;

        ScanForNewAirportRenderers(int.MaxValue);
        ScanForNewWorldRenderers(1f, int.MaxValue);
    }

    private void ScanForNewWorldRenderers(float worldBrightness)
    {
        ScanForNewWorldRenderers(worldBrightness, maximumNewRenderersPerScan);
    }

    private void ScanForNewWorldRenderers(float worldBrightness, int maxNewRenderers)
    {
        if (worldDarkeningRoots == null)
        {
            return;
        }

        RemoveMissingWorldRenderers();

        int newlyRegisteredCount = 0;
        for (int i = 0; i < worldDarkeningRoots.Length; i++)
        {
            Transform root = worldDarkeningRoots[i];
            if (root == null)
            {
                continue;
            }

            newlyRegisteredCount += ScanRootForNewWorldRenderers(
                root,
                worldBrightness,
                maxNewRenderers - newlyRegisteredCount);
            if (newlyRegisteredCount >= maxNewRenderers)
            {
                return;
            }
        }
    }

    private int ScanRootForNewWorldRenderers(
        Transform root,
        float worldBrightness,
        int remainingBudget)
    {
        if (root == null || remainingBudget <= 0)
        {
            return 0;
        }

        int newlyRegisteredCount = 0;
        worldRendererScanStack.Clear();
        worldRendererScanStack.Add(root);

        while (worldRendererScanStack.Count > 0 && newlyRegisteredCount < remainingBudget)
        {
            int lastIndex = worldRendererScanStack.Count - 1;
            Transform current = worldRendererScanStack[lastIndex];
            worldRendererScanStack.RemoveAt(lastIndex);

            if (current == null
                || current.GetComponent<Cesium3DTileset>() != null
                || IsAirportDarkeningRootOrChild(current))
            {
                continue;
            }

            if (current.TryGetComponent(out Renderer renderer) && TryRegisterWorldRenderer(renderer))
            {
                ApplyWorldBrightnessTo(renderer, worldBrightness);
                newlyRegisteredCount++;
                if (newlyRegisteredCount >= remainingBudget)
                {
                    break;
                }
            }
            for (int i = 0; i < current.childCount; i++)
            {
                worldRendererScanStack.Add(current.GetChild(i));
            }
        }

        return newlyRegisteredCount;
    }

    private void ScanForNewAirportRenderers(int maxNewRenderers)
    {
        RemoveMissingAirportRenderers();
        ScanAirportRootsForNewRenderers(maxNewRenderers);
    }

    private int ScanAirportRootsForNewRenderers(int remainingBudget)
    {
        if (remainingBudget <= 0)
        {
            return 0;
        }

        int newlyRegisteredCount = 0;
        Transform[] roots = ResolveAirportDarkeningRoots();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null)
            {
                continue;
            }

            newlyRegisteredCount += ScanAirportRootForNewRenderers(
                root,
                remainingBudget - newlyRegisteredCount);
            if (newlyRegisteredCount >= remainingBudget)
            {
                return newlyRegisteredCount;
            }
        }

        return newlyRegisteredCount;
    }

    private int ScanAirportRootForNewRenderers(
        Transform root,
        int remainingBudget)
    {
        if (root == null || remainingBudget <= 0)
        {
            return 0;
        }

        int newlyRegisteredCount = 0;
        worldRendererScanStack.Clear();
        worldRendererScanStack.Add(root);

        while (worldRendererScanStack.Count > 0 && newlyRegisteredCount < remainingBudget)
        {
            int lastIndex = worldRendererScanStack.Count - 1;
            Transform current = worldRendererScanStack[lastIndex];
            worldRendererScanStack.RemoveAt(lastIndex);

            if (current == null || current.GetComponent<Cesium3DTileset>() != null)
            {
                continue;
            }

            if (current.TryGetComponent(out Renderer renderer) && TryRegisterAirportRenderer(renderer, true))
            {
                ApplyNaturalLightingTo(renderer);
                newlyRegisteredCount++;
                if (newlyRegisteredCount >= remainingBudget)
                {
                    break;
                }
            }

            for (int i = 0; i < current.childCount; i++)
            {
                worldRendererScanStack.Add(current.GetChild(i));
            }
        }

        return newlyRegisteredCount;
    }

    private void ApplyCesiumTilesetNaturalLighting(float nightBlend)
    {
        if (cesiumTilesets == null)
        {
            return;
        }

        SubscribeToCesiumTilesets();
        RemoveMissingCesiumRenderers();

        if (ShouldScanWorldRenderers(Time.unscaledTime, nextCesiumRendererRefreshTime))
        {
            ScanForNewCesiumRenderers();
            nextCesiumRendererRefreshTime = Time.unscaledTime + GetNextWorldRendererScanInterval(
                nightBlend,
                nightRendererScanInterval,
                rendererRefreshInterval);
        }
    }

    private void SubscribeToCesiumTilesets()
    {
        if (cesiumTilesets == null)
        {
            return;
        }

        for (int i = 0; i < cesiumTilesets.Length; i++)
        {
            Cesium3DTileset tileset = cesiumTilesets[i];
            if (tileset == null || subscribedCesiumTilesets.Contains(tileset))
            {
                continue;
            }

            tileset.OnTileGameObjectCreated += HandleCesiumTileGameObjectCreated;
            subscribedCesiumTilesets.Add(tileset);
        }
    }

    private void HandleCesiumTileGameObjectCreated(GameObject tileObject)
    {
        if (tileObject == null)
        {
            return;
        }

        RegisterCesiumRenderers(tileObject.transform, int.MaxValue);
    }

    private void RestoreCesiumTilesets()
    {
        foreach (Cesium3DTileset tileset in subscribedCesiumTilesets)
        {
            if (tileset == null)
            {
                continue;
            }

            tileset.OnTileGameObjectCreated -= HandleCesiumTileGameObjectCreated;
        }

        subscribedCesiumTilesets.Clear();
        ApplyNaturalLightingTo(cachedCesiumRenderers);
        cachedCesiumRenderers.Clear();
        cachedCesiumRendererById.Clear();
    }

    private void ScanForNewCesiumRenderers()
    {
        if (cesiumTilesets == null)
        {
            return;
        }

        int newlyRegisteredCount = 0;
        for (int i = 0; i < cesiumTilesets.Length; i++)
        {
            Cesium3DTileset tileset = cesiumTilesets[i];
            if (tileset == null)
            {
                continue;
            }

            newlyRegisteredCount += RegisterCesiumRenderers(
                tileset.transform,
                maximumNewCesiumRenderersPerScan - newlyRegisteredCount);
            if (newlyRegisteredCount >= maximumNewCesiumRenderersPerScan)
            {
                return;
            }
        }
    }

    private int RegisterCesiumRenderers(Transform root, int remainingBudget)
    {
        if (root == null || remainingBudget <= 0)
        {
            return 0;
        }

        int newlyRegisteredCount = 0;
        cesiumRendererScanStack.Clear();
        cesiumRendererScanStack.Add(root);

        while (cesiumRendererScanStack.Count > 0 && newlyRegisteredCount < remainingBudget)
        {
            int lastIndex = cesiumRendererScanStack.Count - 1;
            Transform current = cesiumRendererScanStack[lastIndex];
            cesiumRendererScanStack.RemoveAt(lastIndex);

            if (current == null)
            {
                continue;
            }

            if (current.TryGetComponent(out Renderer renderer) && TryRegisterCesiumRenderer(renderer))
            {
                ApplyNaturalLightingTo(renderer);
                newlyRegisteredCount++;
                if (newlyRegisteredCount >= remainingBudget)
                {
                    break;
                }
            }

            for (int i = 0; i < current.childCount; i++)
            {
                cesiumRendererScanStack.Add(current.GetChild(i));
            }
        }

        return newlyRegisteredCount;
    }

    private bool TryRegisterCesiumRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        int id = renderer.GetInstanceID();
        if (cachedCesiumRendererById.ContainsKey(id))
        {
            return false;
        }

        cachedCesiumRendererById.Add(id, renderer);
        cachedCesiumRenderers.Add(renderer);
        return true;
    }

    private void RemoveMissingCesiumRenderers()
    {
        cesiumRendererIdsToRemove.Clear();
        foreach (KeyValuePair<int, Renderer> pair in cachedCesiumRendererById)
        {
            if (pair.Value == null)
            {
                cesiumRendererIdsToRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < cesiumRendererIdsToRemove.Count; i++)
        {
            cachedCesiumRendererById.Remove(cesiumRendererIdsToRemove[i]);
        }

        if (cesiumRendererIdsToRemove.Count > 0)
        {
            cachedCesiumRenderers.RemoveAll(renderer => renderer == null);
        }
    }

    private void RemoveMissingWorldRenderers()
    {
        worldRendererIdsToRemove.Clear();
        foreach (KeyValuePair<int, Renderer> pair in cachedWorldRendererById)
        {
            if (pair.Value == null)
            {
                worldRendererIdsToRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < worldRendererIdsToRemove.Count; i++)
        {
            cachedWorldRendererById.Remove(worldRendererIdsToRemove[i]);
        }

        if (worldRendererIdsToRemove.Count > 0)
        {
            cachedWorldRenderers.RemoveAll(renderer => renderer == null);
        }
    }

    private void RemoveMissingAirportRenderers()
    {
        airportRendererIdsToRemove.Clear();
        foreach (KeyValuePair<int, Renderer> pair in cachedAirportRendererById)
        {
            if (pair.Value == null)
            {
                airportRendererIdsToRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < airportRendererIdsToRemove.Count; i++)
        {
            cachedAirportRendererById.Remove(airportRendererIdsToRemove[i]);
        }

        if (airportRendererIdsToRemove.Count > 0)
        {
            cachedAirportRenderers.RemoveAll(renderer => renderer == null);
        }
    }

    private bool TryRegisterWorldRenderer(Renderer renderer)
    {
        if (renderer == null
            || ShouldExcludeRenderer(renderer)
            || IsAirportRenderer(renderer)
            || IsUnderAirportDarkeningRoot(renderer.transform)
            || IsUnderCesiumTileset(renderer.transform))
        {
            return false;
        }

        int id = renderer.GetInstanceID();
        if (cachedWorldRendererById.ContainsKey(id))
        {
            return false;
        }

        cachedWorldRendererById.Add(id, renderer);
        cachedWorldRenderers.Add(renderer);
        return true;
    }

    private bool TryRegisterAirportRenderer(Renderer renderer)
    {
        return TryRegisterAirportRenderer(renderer, false);
    }

    private bool TryRegisterAirportRenderer(Renderer renderer, bool forceAirportRoot)
    {
        if (renderer == null
            || ShouldExcludeRenderer(renderer)
            || (!forceAirportRoot && !IsAirportRenderer(renderer))
            || IsUnderCesiumTileset(renderer.transform))
        {
            return false;
        }

        int id = renderer.GetInstanceID();
        if (cachedAirportRendererById.ContainsKey(id))
        {
            return false;
        }

        cachedAirportRendererById.Add(id, renderer);
        cachedAirportRenderers.Add(renderer);
        return true;
    }

    private bool IsAirportDarkeningRootOrChild(Transform transform)
    {
        return IsUnderAirportDarkeningRoot(transform);
    }

    private bool IsUnderAirportDarkeningRoot(Transform transform)
    {
        if (airportDarkeningRoots == null)
        {
            return false;
        }

        Transform current = transform;
        while (current != null)
        {
            for (int i = 0; i < airportDarkeningRoots.Length; i++)
            {
                Transform root = airportDarkeningRoots[i];
                if (root != null && current == root)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }

    private Transform[] ResolveAirportDarkeningRoots()
    {
        resolvedAirportRootBuffer.Clear();
        AddValidAirportRoots(airportDarkeningRoots, resolvedAirportRootBuffer);

        if (resolvedAirportRootBuffer.Count == 0)
        {
            DiscoverAirportRootsFromScene();
            AddValidAirportRoots(discoveredAirportRoots, resolvedAirportRootBuffer);
        }

        return resolvedAirportRootBuffer.ToArray();
    }

    private void AddValidAirportRoots(IReadOnlyList<Transform> roots, List<Transform> target)
    {
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            if (root == null || !HasRendererInChildren(root) || target.Contains(root))
            {
                continue;
            }

            target.Add(root);
        }
    }

    private void DiscoverAirportRootsFromScene()
    {
        if (hasScannedSceneAirportRoots)
        {
            return;
        }

        hasScannedSceneAirportRoots = true;
        discoveredAirportRoots.Clear();

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate == null
                || !candidate.gameObject.scene.IsValid()
                || !ContainsAirportToken(candidate.name)
                || HasAirportTokenAncestor(candidate)
                || !HasRendererInChildren(candidate))
            {
                continue;
            }

            discoveredAirportRoots.Add(candidate);
        }
    }

    private bool HasAirportTokenAncestor(Transform transform)
    {
        Transform current = transform.parent;
        while (current != null)
        {
            if (ContainsAirportToken(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool HasRendererInChildren(Transform root)
    {
        return root != null && root.GetComponentInChildren<Renderer>(true) != null;
    }

    private bool IsUnderCesiumTileset(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.GetComponent<Cesium3DTileset>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void RestoreCachedWorldRenderers()
    {
        ApplyWorldBrightnessTo(cachedWorldRenderers, 1f);
        ApplyWorldBrightnessTo(cachedAirportRenderers, 1f);
    }

    private bool ShouldExcludeRenderer(Renderer renderer)
    {
        Transform rendererTransform = renderer.transform;

        if (excludedRoots != null)
        {
            for (int i = 0; i < excludedRoots.Length; i++)
            {
                Transform excludedRoot = excludedRoots[i];
                if (excludedRoot != null && rendererTransform.IsChildOf(excludedRoot))
                {
                    return true;
                }
            }
        }

        if (excludedNameContains == null)
        {
            return false;
        }

        string objectPath = BuildTransformPath(rendererTransform);
        if (ContainsExcludedToken(objectPath))
        {
            return true;
        }

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && ContainsExcludedToken(material.name))
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsExcludedToken(string value)
    {
        for (int i = 0; i < excludedNameContains.Length; i++)
        {
            string token = excludedNameContains[i];
            if (!string.IsNullOrEmpty(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAirportRenderer(Renderer renderer)
    {
        if (renderer == null || airportNameContains == null)
        {
            return false;
        }

        if (ContainsAirportToken(BuildTransformPath(renderer.transform)))
        {
            return true;
        }

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && ContainsAirportToken(material.name))
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsAirportToken(string value)
    {
        if (airportNameContains == null)
        {
            return false;
        }

        for (int i = 0; i < airportNameContains.Length; i++)
        {
            string token = airportNameContains[i];
            if (!string.IsNullOrEmpty(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyWorldBrightnessTo(List<Renderer> renderers, float brightness)
    {
        EnsurePropertyBlock();

        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            ApplyWorldBrightnessTo(renderer, brightness);
        }
    }

    private void ApplyNaturalLightingTo(List<Renderer> renderers)
    {
        ApplyWorldBrightnessTo(renderers, 1f);
    }

    private void ApplyNaturalLightingTo(Renderer renderer)
    {
        ApplyWorldBrightnessTo(renderer, 1f);
    }

    private void ApplyWorldBrightnessTo(Renderer renderer, float brightness)
    {
        if (renderer == null)
        {
            return;
        }

        Material[] materials = renderer.sharedMaterials;
        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            Material material = materials[materialIndex];
            if (material == null)
            {
                continue;
            }

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock, materialIndex);
            ApplyMaterialColor(propertyBlock, material, CesiumBaseColorFactorId, brightness);
            ApplyMaterialColor(propertyBlock, material, BaseColorId, brightness);
            ApplyMaterialColor(propertyBlock, material, ColorId, brightness);
            ClearMaterialEmissionOverride(propertyBlock, material);
            renderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }

    private static void ApplyMaterialColor(MaterialPropertyBlock block, Material material, int propertyId, float brightness)
    {
        if (!material.HasProperty(propertyId))
        {
            return;
        }

        block.SetColor(propertyId, CalculateDimmedColor(material.GetColor(propertyId), brightness, 1f));
    }

    private static void ClearMaterialEmissionOverride(MaterialPropertyBlock block, Material material)
    {
        if (material.HasProperty(EmissionColorId))
        {
            block.SetColor(EmissionColorId, Color.black);
        }
    }

    public static Color CalculateCesiumRuntimeMaterialColor(Color originalColor, float brightness)
    {
        return originalColor;
    }

    public static float CalculateNightBlend(float hour, float nightStartHour, float nightEndHour, float transitionHours)
    {
        hour = Mathf.Repeat(hour, 24f);
        nightStartHour = Mathf.Repeat(nightStartHour, 24f);
        nightEndHour = Mathf.Repeat(nightEndHour, 24f);

        bool isNight = IsHourInNightRange(hour, nightStartHour, nightEndHour);
        if (!isNight)
        {
            return 0f;
        }

        if (transitionHours <= 0f)
        {
            return 1f;
        }

        float hoursSinceStart = DeltaHours(nightStartHour, hour);
        float hoursUntilEnd = DeltaHours(hour, nightEndHour);
        float fadeIn = Mathf.Clamp01(hoursSinceStart / transitionHours);
        float fadeOut = Mathf.Clamp01(hoursUntilEnd / transitionHours);
        return Mathf.Min(fadeIn, fadeOut);
    }

    public static float CalculateNightVisualBlend(
        float hour,
        float eveningTransitionStartHour,
        float fullNightStartHour,
        float nightEndHour,
        float morningTransitionHours)
    {
        hour = Mathf.Repeat(hour, 24f);
        eveningTransitionStartHour = Mathf.Repeat(eveningTransitionStartHour, 24f);
        fullNightStartHour = Mathf.Repeat(fullNightStartHour, 24f);
        nightEndHour = Mathf.Repeat(nightEndHour, 24f);

        if (IsHourInNightRange(hour, fullNightStartHour, nightEndHour))
        {
            return 1f;
        }

        float eveningDuration = DeltaHours(eveningTransitionStartHour, fullNightStartHour);
        if (eveningDuration > 0f && IsHourInForwardRange(hour, eveningTransitionStartHour, fullNightStartHour))
        {
            return Mathf.Clamp01(DeltaHours(eveningTransitionStartHour, hour) / eveningDuration);
        }

        if (morningTransitionHours > 0f)
        {
            float morningTransitionEndHour = Mathf.Repeat(nightEndHour + morningTransitionHours, 24f);
            if (IsHourInForwardRange(hour, nightEndHour, morningTransitionEndHour))
            {
                return 1f - Mathf.Clamp01(DeltaHours(nightEndHour, hour) / morningTransitionHours);
            }
        }

        return 0f;
    }

    public static float CalculateNightSurfaceBlend(
        float hour,
        float surfaceDarkeningStartHour,
        float surfaceFullDarkHour,
        float nightEndHour,
        float morningTransitionHours)
    {
        hour = Mathf.Repeat(hour, 24f);
        surfaceDarkeningStartHour = Mathf.Repeat(surfaceDarkeningStartHour, 24f);
        surfaceFullDarkHour = Mathf.Repeat(surfaceFullDarkHour, 24f);
        nightEndHour = Mathf.Repeat(nightEndHour, 24f);

        if (IsHourInNightRange(hour, surfaceFullDarkHour, nightEndHour))
        {
            return 1f;
        }

        float darkeningDuration = DeltaHours(surfaceDarkeningStartHour, surfaceFullDarkHour);
        if (darkeningDuration > 0f && IsHourInForwardRange(hour, surfaceDarkeningStartHour, surfaceFullDarkHour))
        {
            return Mathf.Clamp01(DeltaHours(surfaceDarkeningStartHour, hour) / darkeningDuration);
        }

        if (morningTransitionHours > 0f)
        {
            float morningTransitionEndHour = Mathf.Repeat(nightEndHour + morningTransitionHours, 24f);
            if (IsHourInForwardRange(hour, nightEndHour, morningTransitionEndHour))
            {
                return 1f - Mathf.Clamp01(DeltaHours(nightEndHour, hour) / morningTransitionHours);
            }
        }

        return 0f;
    }

    public static Color CalculateDimmedColor(Color originalColor, float brightness, float blend)
    {
        Color target = originalColor * Mathf.Clamp01(brightness);
        target.a = originalColor.a;
        return Color.Lerp(originalColor, target, Mathf.Clamp01(blend));
    }

    public static float CalculateNightEnvironmentValue(float originalValue, float targetValue, float blend)
    {
        return Mathf.Lerp(originalValue, targetValue, Mathf.Clamp01(blend));
    }

    public static Color CalculateNightEnvironmentColor(Color originalColor, Color targetColor, float blend)
    {
        return Color.Lerp(originalColor, targetColor, Mathf.Clamp01(blend));
    }

    public static float CalculateSurfaceBrightness(float nightBlend, float targetBrightness)
    {
        return Mathf.Lerp(1f, Mathf.Clamp01(targetBrightness), Mathf.Clamp01(nightBlend));
    }

    public static float ClampNightLightIntensity(float currentIntensity, float maximumIntensity, float blend)
    {
        float target = Mathf.Min(Mathf.Max(0f, currentIntensity), Mathf.Max(0f, maximumIntensity));
        return Mathf.Lerp(currentIntensity, target, Mathf.Clamp01(blend));
    }

    public static bool ShouldScanWorldRenderers(
        float currentTime,
        float nextRefreshTime)
    {
        return currentTime >= nextRefreshTime;
    }

    public static float GetNextWorldRendererScanInterval(
        float nightBlend,
        float nightScanInterval,
        float dayScanInterval)
    {
        return nightBlend > 0f
            ? Mathf.Max(0.05f, nightScanInterval)
            : Mathf.Max(0.1f, dayScanInterval);
    }

    public static bool ShouldUseCesiumRuntimeMaterial(float nightBlend)
    {
        return false;
    }

    public static bool ShouldApplyBrightnessChange(float previousBrightness, float nextBrightness)
    {
        return previousBrightness < 0f || Mathf.Abs(previousBrightness - nextBrightness) >= 0.005f;
    }

    public static Color CalculateStarColor(Color currentColor, float brightnessMultiplier, float blend)
    {
        float multiplier = Mathf.Lerp(1f, Mathf.Max(0f, brightnessMultiplier), Mathf.Clamp01(blend));
        Color result = currentColor * multiplier;
        result.a = Mathf.Clamp01(currentColor.a * multiplier);
        return result;
    }

    public static Color ResolveStarSourceColor(
        Color currentColor,
        Color previousSourceColor,
        Color previousAppliedColor,
        bool hasPreviousAppliedColor)
    {
        if (hasPreviousAppliedColor && AreColorsApproximatelyEqual(currentColor, previousAppliedColor))
        {
            return previousSourceColor;
        }

        return currentColor;
    }

    private static bool IsHourInNightRange(float hour, float nightStartHour, float nightEndHour)
    {
        if (Mathf.Approximately(nightStartHour, nightEndHour))
        {
            return true;
        }

        if (nightStartHour < nightEndHour)
        {
            return hour >= nightStartHour && hour < nightEndHour;
        }

        return hour >= nightStartHour || hour < nightEndHour;
    }

    private static bool IsHourInForwardRange(float hour, float startHour, float endHour)
    {
        if (Mathf.Approximately(startHour, endHour))
        {
            return false;
        }

        float duration = DeltaHours(startHour, endHour);
        float elapsed = DeltaHours(startHour, hour);
        return elapsed >= 0f && elapsed < duration;
    }

    private static float DeltaHours(float fromHour, float toHour)
    {
        return Mathf.Repeat(toHour - fromHour, 24f);
    }

    private static bool AreColorsApproximatelyEqual(Color first, Color second)
    {
        const float tolerance = 0.0001f;
        return Mathf.Abs(first.r - second.r) <= tolerance
            && Mathf.Abs(first.g - second.g) <= tolerance
            && Mathf.Abs(first.b - second.b) <= tolerance
            && Mathf.Abs(first.a - second.a) <= tolerance;
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}

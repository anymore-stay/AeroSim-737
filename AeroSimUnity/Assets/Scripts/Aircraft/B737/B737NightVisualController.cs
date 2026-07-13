using System;
using System.Collections.Generic;
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
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    private static readonly int NightSkyTintId = Shader.PropertyToID("_NightSkyTint");

    [Header("引用")]
    [SerializeField] private UniStormSystem uniStormSystem;
    [SerializeField] private Renderer starsRenderer;
    [SerializeField] private Transform[] worldDarkeningRoots = Array.Empty<Transform>();
    [SerializeField] private Transform[] excludedRoots = Array.Empty<Transform>();

    [Header("夜晚时间")]
    [SerializeField, Range(0f, 24f)] private float nightStartHour = 19f;
    [SerializeField, Range(0f, 24f)] private float nightEndHour = 5.5f;
    [SerializeField, Min(0f)] private float transitionHours = 0.75f;

    [Header("目标图初始参数")]
    [SerializeField, Range(0f, 1f)] private float ambientIntensity = 0.015f;
    [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 0.02f;
    [SerializeField] private Color ambientSkyColor = new Color(0.004f, 0.005f, 0.007f, 1f);
    [SerializeField] private Color ambientEquatorColor = new Color(0.003f, 0.0035f, 0.0045f, 1f);
    [SerializeField] private Color ambientGroundColor = new Color(0.0015f, 0.0015f, 0.0015f, 1f);
    [SerializeField] private Color skyTintColor = new Color(0.002f, 0.0025f, 0.004f, 1f);

    [Header("UniStorm 夜间光")]
    [SerializeField, Range(0f, 1f)] private float maximumMoonLightIntensity = 0.03f;
    [SerializeField, Range(0f, 2f)] private float maximumMoonAtmosphericFogIntensity = 0f;
    [SerializeField, Range(0f, 2f)] private float maximumSunAtmosphericFogIntensity = 0f;
    [SerializeField, Range(0f, 3f)] private float starBrightnessMultiplier = 1.35f;

    [Header("世界地表压暗")]
    [SerializeField, Range(0f, 1f)] private float worldSurfaceBrightness = 0.08f;
    [SerializeField, Min(0.1f)] private float rendererRefreshInterval = 2f;
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

    private readonly List<Renderer> cachedWorldRenderers = new List<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private float nextRendererRefreshTime;
    private Color lastSourceStarColor;
    private Color lastAppliedStarColor;
    private bool hasLastAppliedStarColor;

    private void Awake()
    {
        EnsurePropertyBlock();
        ResolveReferences();
        RefreshWorldRenderers();
    }

    private void OnEnable()
    {
        EnsurePropertyBlock();
        ResolveReferences();
        RefreshWorldRenderers();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        float hour = GetCurrentHour();
        float nightBlend = CalculateNightBlend(hour, nightStartHour, nightEndHour, transitionHours);

        ApplyEnvironment(nightBlend);
        ApplyUniStormNightLight(nightBlend);

        if (Time.unscaledTime >= nextRendererRefreshTime)
        {
            RefreshWorldRenderers();
            nextRendererRefreshTime = Time.unscaledTime + rendererRefreshInterval;
        }

        ApplyWorldDarkening(nightBlend);
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

    private void ApplyEnvironment(float nightBlend)
    {
        if (nightBlend <= 0f)
        {
            return;
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(RenderSettings.ambientIntensity, ambientIntensity, nightBlend);
        RenderSettings.reflectionIntensity = Mathf.Lerp(RenderSettings.reflectionIntensity, reflectionIntensity, nightBlend);
        RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, ambientSkyColor, nightBlend);
        RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, ambientEquatorColor, nightBlend);
        RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, ambientGroundColor, nightBlend);

        Material skybox = RenderSettings.skybox;
        if (skybox == null)
        {
            return;
        }

        if (skybox.HasProperty(SkyTintId))
        {
            skybox.SetColor(SkyTintId, Color.Lerp(skybox.GetColor(SkyTintId), skyTintColor, nightBlend));
        }
        if (skybox.HasProperty(NightSkyTintId))
        {
            skybox.SetColor(NightSkyTintId, Color.Lerp(skybox.GetColor(NightSkyTintId), skyTintColor, nightBlend));
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

    private void RefreshWorldRenderers()
    {
        RestoreCachedWorldRenderers();
        cachedWorldRenderers.Clear();

        if (worldDarkeningRoots == null)
        {
            return;
        }

        for (int i = 0; i < worldDarkeningRoots.Length; i++)
        {
            Transform root = worldDarkeningRoots[i];
            if (root == null)
            {
                continue;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer = renderers[j];
                if (renderer != null && !ShouldExcludeRenderer(renderer))
                {
                    cachedWorldRenderers.Add(renderer);
                }
            }
        }
    }

    private void RestoreCachedWorldRenderers()
    {
        ApplyWorldBrightnessTo(cachedWorldRenderers, 1f);
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

    private void ApplyWorldDarkening(float nightBlend)
    {
        float brightness = Mathf.Lerp(1f, worldSurfaceBrightness, nightBlend);
        ApplyWorldBrightnessTo(cachedWorldRenderers, brightness);
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
                renderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
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

    public static Color CalculateDimmedColor(Color originalColor, float brightness, float blend)
    {
        Color target = originalColor * Mathf.Clamp01(brightness);
        target.a = originalColor.a;
        return Color.Lerp(originalColor, target, Mathf.Clamp01(blend));
    }

    public static float ClampNightLightIntensity(float currentIntensity, float maximumIntensity, float blend)
    {
        float target = Mathf.Min(Mathf.Max(0f, currentIntensity), Mathf.Max(0f, maximumIntensity));
        return Mathf.Lerp(currentIntensity, target, Mathf.Clamp01(blend));
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

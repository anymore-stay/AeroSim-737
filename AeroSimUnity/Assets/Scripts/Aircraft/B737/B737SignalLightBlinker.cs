using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class B737SignalLightBlinker : MonoBehaviour
{
    public const float DefaultCycleSeconds = 1f;
    public const float DefaultPulseSeconds = 0.11f;
    public const float DefaultFadeSeconds = 0.02f;
    public static readonly Color DefaultBeaconRed = new Color(0.72f, 0f, 0f, 1f);
    public const float DefaultEmissionIntensity = 30f;
    public const float DefaultPulseLightIntensity = 85f;
    public const float DefaultLowerPulseLightIntensityScale = 0.09411765f;
    public const float DefaultUpperPulseLightIntensityScale = 1.35f;
    public const float DefaultPulseLightRange = 22f;
    public const LightType DefaultPulseLightType = LightType.Spot;
    public const float DefaultPulseLightSpotAngle = 150f;
    public const float DefaultPulseLightInnerSpotAngle = 95f;
    public const float DefaultPulseLightSurfaceOffset = 0f;
    public const LightShadows DefaultPulseLightShadows = LightShadows.Soft;
    public const bool DefaultAffectTargetRenderer = false;
    public const bool DefaultUsePulseLight = false;
    public const bool DefaultAutoCreateVisual = true;
    public const float DefaultVisualScale = 0.16f;
    public const float DefaultVisualPeakAlpha = 0.6f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Blink")]
    [SerializeField, Min(0.01f)] private float cycleSeconds = DefaultCycleSeconds;
    [SerializeField, Min(0.01f)] private float pulseSeconds = DefaultPulseSeconds;
    [SerializeField, Min(0f)] private float fadeSeconds = DefaultFadeSeconds;
    [SerializeField] private float phaseOffsetSeconds;
    [SerializeField] private bool startOn = true;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color onColor = DefaultBeaconRed;
    [SerializeField] private Color offColor = new Color(0.08f, 0f, 0f, 1f);
    [SerializeField] private float emissionIntensity = DefaultEmissionIntensity;
    [SerializeField] private bool toggleRendererVisibility;
    [SerializeField] private bool affectTargetRenderer = DefaultAffectTargetRenderer;

    [Header("Beacon Visual")]
    [SerializeField] private bool autoCreateVisual = DefaultAutoCreateVisual;
    [SerializeField] private Renderer visualRenderer;
    [SerializeField] private Renderer[] visualRenderers;
    [SerializeField] private float visualScale = DefaultVisualScale;
    [SerializeField, Range(0f, 1f)] private float visualPeakAlpha = DefaultVisualPeakAlpha;

    [Header("Pulse Light")]
    [SerializeField] private bool usePulseLight = DefaultUsePulseLight;
    [SerializeField] private bool autoCreatePulseLight = true;
    [SerializeField] private Light pulseLight;
    [SerializeField] private Light[] pulseLights;
    [SerializeField] private float pulseLightIntensity = DefaultPulseLightIntensity;
    [SerializeField] private float[] pulseLightIntensityScales =
    {
        DefaultLowerPulseLightIntensityScale,
        DefaultUpperPulseLightIntensityScale
    };
    [SerializeField] private float pulseLightRange = DefaultPulseLightRange;
    [SerializeField] private LightType pulseLightType = DefaultPulseLightType;
    [SerializeField, Range(1f, 179f)] private float pulseLightSpotAngle = DefaultPulseLightSpotAngle;
    [SerializeField, Range(0f, 179f)] private float pulseLightInnerSpotAngle = DefaultPulseLightInnerSpotAngle;
    [SerializeField, Min(0f)] private float pulseLightSurfaceOffset = DefaultPulseLightSurfaceOffset;
    [SerializeField] private LightShadows pulseLightShadows = DefaultPulseLightShadows;

    private MaterialPropertyBlock propertyBlock;
    private bool originalRendererEnabled;
    private bool hasOriginalRendererEnabled;
    private Material visualMaterial;
    private PulseLightState[] originalPulseLightStates;

    private struct PulseLightState
    {
        public Light Light;
        public bool Enabled;
        public float Intensity;
        public float Range;
        public Color Color;
        public LightType Type;
        public float SpotAngle;
        public float InnerSpotAngle;
        public LightShadows Shadows;
        public LightRenderMode RenderMode;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    public static float EvaluateBeaconIntensity(float elapsedSeconds, float cycleSeconds, float pulseSeconds, float fadeSeconds)
    {
        float safeCycle = Mathf.Max(0.01f, cycleSeconds);
        float safePulse = Mathf.Clamp(pulseSeconds, 0.01f, safeCycle);
        float safeFade = Mathf.Clamp(fadeSeconds, 0f, safePulse * 0.5f);
        float cycleTime = Mathf.Repeat(Mathf.Max(0f, elapsedSeconds), safeCycle);

        if (cycleTime >= safePulse)
        {
            return 0f;
        }

        if (safeFade <= 0f)
        {
            return 1f;
        }

        if (cycleTime < safeFade)
        {
            return Mathf.SmoothStep(0f, 1f, cycleTime / safeFade);
        }

        float fadeOutStart = safePulse - safeFade;
        if (cycleTime > fadeOutStart)
        {
            return Mathf.SmoothStep(1f, 0f, (cycleTime - fadeOutStart) / safeFade);
        }

        return 1f;
    }

    public static float EvaluatePulseLightIntensity(float beaconIntensity, float peakIntensity)
    {
        return Mathf.Max(0f, peakIntensity) * Mathf.Clamp01(beaconIntensity);
    }

    public static float EvaluatePulseLightIntensity(float beaconIntensity, float peakIntensity, float intensityScale)
    {
        return EvaluatePulseLightIntensity(beaconIntensity, peakIntensity) * Mathf.Max(0f, intensityScale);
    }

    public static Color EvaluateVisualOverlayColor(Color color, float beaconIntensity, float peakAlpha)
    {
        Color result = color;
        result.a = Mathf.Clamp01(beaconIntensity) * Mathf.Clamp01(peakAlpha);
        return result;
    }

    public static Vector3[] GetAutoPulseLightLocalPositions(Bounds targetLocalBounds)
    {
        Vector3 lower = targetLocalBounds.center;
        Vector3 upper = targetLocalBounds.center;
        lower.z = targetLocalBounds.min.z;
        upper.z = targetLocalBounds.max.z;
        return new[] { lower, upper };
    }

    public static Vector3[] GetAutoPulseLightLocalPositions(Bounds targetLocalBounds, Vector3[] meshVertices)
    {
        return GetAutoVisualLocalPositions(targetLocalBounds, meshVertices);
    }

    public static Vector3 GetOutwardPulseLightLocalDirection(Vector3 localPosition, Bounds targetLocalBounds)
    {
        Vector3 offset = localPosition - targetLocalBounds.center;
        Vector3 magnitudes = new Vector3(Mathf.Abs(offset.x), Mathf.Abs(offset.y), Mathf.Abs(offset.z));
        int dominantAxis = GetDominantAxis(magnitudes);

        if (magnitudes[dominantAxis] <= 0.0001f)
        {
            dominantAxis = GetDominantAxis(targetLocalBounds.size);
            offset = localPosition - targetLocalBounds.center;
        }

        Vector3 direction = Vector3.zero;
        float axisValue = GetAxisValue(offset, dominantAxis);
        SetAxisValue(ref direction, dominantAxis, axisValue < 0f ? -1f : 1f);
        return direction;
    }

    public static Vector3[] GetAutoVisualLocalPositions(Bounds targetLocalBounds)
    {
        Vector3 first = targetLocalBounds.center;
        Vector3 second = targetLocalBounds.center;
        int dominantAxis = GetDominantAxis(targetLocalBounds.size);

        if (dominantAxis == 0)
        {
            first.x = targetLocalBounds.min.x;
            second.x = targetLocalBounds.max.x;
        }
        else if (dominantAxis == 1)
        {
            first.y = targetLocalBounds.min.y;
            second.y = targetLocalBounds.max.y;
        }
        else
        {
            first.z = targetLocalBounds.min.z;
            second.z = targetLocalBounds.max.z;
        }

        return new[] { first, second };
    }

    public static Vector3[] GetAutoVisualLocalPositions(Bounds targetLocalBounds, Vector3[] meshVertices)
    {
        if (meshVertices == null || meshVertices.Length == 0)
        {
            return GetAutoVisualLocalPositions(targetLocalBounds);
        }

        int dominantAxis = GetDominantAxis(targetLocalBounds.size);
        float splitValue = GetAxisValue(targetLocalBounds.center, dominantAxis);

        bool hasFirst = false;
        bool hasSecond = false;
        Bounds firstBounds = default;
        Bounds secondBounds = default;

        for (int index = 0; index < meshVertices.Length; index++)
        {
            Vector3 vertex = meshVertices[index];
            bool isFirstCluster = GetAxisValue(vertex, dominantAxis) <= splitValue;

            if (isFirstCluster)
            {
                EncapsulatePoint(ref firstBounds, ref hasFirst, vertex);
            }
            else
            {
                EncapsulatePoint(ref secondBounds, ref hasSecond, vertex);
            }
        }

        if (!hasFirst || !hasSecond)
        {
            return GetAutoVisualLocalPositions(targetLocalBounds);
        }

        return new[] { firstBounds.center, secondBounds.center };
    }

    private static int GetDominantAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z)
        {
            return 0;
        }

        if (size.y >= size.x && size.y >= size.z)
        {
            return 1;
        }

        return 2;
    }

    private static float GetAxisValue(Vector3 value, int axis)
    {
        if (axis == 0)
        {
            return value.x;
        }

        if (axis == 1)
        {
            return value.y;
        }

        return value.z;
    }

    private static void SetAxisValue(ref Vector3 value, int axis, float axisValue)
    {
        if (axis == 0)
        {
            value.x = axisValue;
            return;
        }

        if (axis == 1)
        {
            value.y = axisValue;
            return;
        }

        value.z = axisValue;
    }

    private static void EncapsulatePoint(ref Bounds bounds, ref bool hasBounds, Vector3 point)
    {
        if (!hasBounds)
        {
            bounds = new Bounds(point, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(point);
    }

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        propertyBlock = new MaterialPropertyBlock();
        InitializeVisualRenderer();

        if (targetRenderer != null)
        {
            originalRendererEnabled = targetRenderer.enabled;
            hasOriginalRendererEnabled = true;
        }

        InitializePulseLight();
    }

    private void OnEnable()
    {
        ApplyBeaconIntensity(startOn ? 1f : 0f);
    }

    private void Update()
    {
        float phase = startOn ? phaseOffsetSeconds : phaseOffsetSeconds + cycleSeconds * 0.5f;
        float intensity = EvaluateBeaconIntensity(Time.time + phase, cycleSeconds, pulseSeconds, fadeSeconds);
        ApplyBeaconIntensity(intensity);
    }

    private void OnDisable()
    {
        if (targetRenderer != null)
        {
            if (hasOriginalRendererEnabled)
            {
                targetRenderer.enabled = originalRendererEnabled;
            }

            targetRenderer.SetPropertyBlock(null);
        }

        if (visualRenderer != null)
        {
            visualRenderer.SetPropertyBlock(null);
        }

        if (visualRenderers != null)
        {
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                if (visualRenderers[index] != null && visualRenderers[index] != visualRenderer)
                {
                    visualRenderers[index].SetPropertyBlock(null);
                }
            }
        }

        if (originalPulseLightStates != null)
        {
            for (int index = 0; index < originalPulseLightStates.Length; index++)
            {
                RestorePulseLightState(originalPulseLightStates[index]);
            }
        }
    }

    private void ApplyBeaconIntensity(float intensity)
    {
        float clampedIntensity = Mathf.Clamp01(intensity);

        ApplyVisualRendererIntensity(clampedIntensity);

        if (affectTargetRenderer)
        {
            ApplyRendererIntensity(targetRenderer, clampedIntensity, toggleRendererVisibility);
        }

        ApplyPulseLight(clampedIntensity);
    }

    private void ApplyRendererIntensity(Renderer renderer, float intensity, bool toggleVisibility)
    {
        if (renderer == null)
        {
            return;
        }

        if (toggleVisibility)
        {
            renderer.enabled = intensity > 0.001f;
        }

        EnsurePropertyBlock();
        Color visibleColor = Color.Lerp(offColor, onColor, intensity);
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, visibleColor);
        propertyBlock.SetColor(ColorId, visibleColor);
        propertyBlock.SetColor(EmissionColorId, onColor * (emissionIntensity * intensity));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyVisualRendererIntensity(float intensity)
    {
        if (visualRenderers != null && visualRenderers.Length > 0)
        {
            for (int index = 0; index < visualRenderers.Length; index++)
            {
                ApplyVisualOverlayIntensity(visualRenderers[index], intensity);
            }

            return;
        }

        ApplyVisualOverlayIntensity(visualRenderer, intensity);
    }

    private void ApplyVisualOverlayIntensity(Renderer renderer, float intensity)
    {
        if (renderer == null)
        {
            return;
        }

        EnsurePropertyBlock();
        Color overlayColor = EvaluateVisualOverlayColor(onColor, intensity, visualPeakAlpha);
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, overlayColor);
        propertyBlock.SetColor(ColorId, overlayColor);
        propertyBlock.SetColor(EmissionColorId, onColor * (emissionIntensity * intensity));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void InitializeVisualRenderer()
    {
        if (!autoCreateVisual)
        {
            return;
        }

        if (visualRenderers != null && visualRenderers.Length > 0)
        {
            return;
        }

        if (visualRenderer != null)
        {
            visualRenderers = new[] { visualRenderer };
            return;
        }

        Vector3[] localPositions = GetVisualLocalPositionsFromTarget();

        visualMaterial = CreateVisualMaterial();
        visualRenderers = new Renderer[localPositions.Length];

        for (int index = 0; index < localPositions.Length; index++)
        {
            visualRenderers[index] = CreateVisualRenderer(index, localPositions[index]);
        }

        if (visualRenderers.Length > 0)
        {
            visualRenderer = visualRenderers[0];
        }
    }

    private Renderer CreateVisualRenderer(int index, Vector3 localPosition)
    {
        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualObject.name = $"Beacon Visual {index + 1}";
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = localPosition;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);

        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer renderer = visualObject.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = visualMaterial;
        return renderer;
    }

    private Vector3[] GetVisualLocalPositionsFromTarget()
    {
        if (targetRenderer == null)
        {
            return new[] { Vector3.zero };
        }

        MeshFilter meshFilter = targetRenderer.GetComponent<MeshFilter>();
        Mesh sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;
        if (sharedMesh == null)
        {
            return GetAutoVisualLocalPositions(targetRenderer.localBounds);
        }

        return GetAutoVisualLocalPositions(targetRenderer.localBounds, sharedMesh.vertices);
    }

    private static Material CreateVisualMaterial()
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (material.shader == null)
        {
            material.shader = Shader.Find("Unlit/Color");
        }

        material.name = "Runtime Beacon Visual";
        ConfigureTransparentVisualMaterial(material);
        return material;
    }

    private static void ConfigureTransparentVisualMaterial(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void InitializePulseLight()
    {
        if (!usePulseLight)
        {
            return;
        }

        EnsurePulseLights();

        if (pulseLights == null || pulseLights.Length == 0)
        {
            return;
        }

        Bounds localBounds = GetTargetLocalBounds();
        originalPulseLightStates = new PulseLightState[pulseLights.Length];

        for (int index = 0; index < pulseLights.Length; index++)
        {
            Light light = pulseLights[index];
            if (light == null)
            {
                continue;
            }

            originalPulseLightStates[index] = CapturePulseLightState(light);
            ConfigurePulseLight(light, GetOutwardPulseLightLocalDirection(light.transform.localPosition, localBounds));
        }
    }

    private void EnsurePulseLights()
    {
        if (pulseLights != null && pulseLights.Length > 0)
        {
            if (pulseLight == null)
            {
                pulseLight = pulseLights[0];
            }

            return;
        }

        if (pulseLight != null)
        {
            pulseLights = new[] { pulseLight };
            return;
        }

        if (!autoCreatePulseLight)
        {
            return;
        }

        Vector3[] localPositions = GetPulseLightLocalPositionsFromTarget();
        Bounds localBounds = GetTargetLocalBounds();
        pulseLights = new Light[localPositions.Length];

        for (int index = 0; index < localPositions.Length; index++)
        {
            Vector3 direction = GetOutwardPulseLightLocalDirection(localPositions[index], localBounds);
            GameObject lightObject = new GameObject(GetPulseLightName(index, direction));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = localPositions[index] + direction * Mathf.Max(0f, pulseLightSurfaceOffset);
            lightObject.transform.localRotation = GetPulseLightLocalRotation(direction);
            pulseLights[index] = lightObject.AddComponent<Light>();
        }

        if (pulseLights.Length > 0)
        {
            pulseLight = pulseLights[0];
        }
    }

    private void ApplyPulseLight(float beaconIntensity)
    {
        if (!usePulseLight || pulseLights == null)
        {
            return;
        }

        for (int index = 0; index < pulseLights.Length; index++)
        {
            float lightIntensity = EvaluatePulseLightIntensity(
                beaconIntensity,
                pulseLightIntensity,
                GetPulseLightIntensityScale(index));
            ApplyPulseLightIntensity(pulseLights[index], lightIntensity);
        }
    }

    private Bounds GetTargetLocalBounds()
    {
        return targetRenderer != null ? targetRenderer.localBounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private Vector3[] GetPulseLightLocalPositionsFromTarget()
    {
        if (targetRenderer == null)
        {
            return GetAutoPulseLightLocalPositions(GetTargetLocalBounds());
        }

        MeshFilter meshFilter = targetRenderer.GetComponent<MeshFilter>();
        Mesh sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;
        if (sharedMesh == null)
        {
            return GetAutoPulseLightLocalPositions(targetRenderer.localBounds);
        }

        return GetAutoPulseLightLocalPositions(targetRenderer.localBounds, sharedMesh.vertices);
    }

    private static string GetPulseLightName(int index, Vector3 direction)
    {
        if (Mathf.Abs(direction.z) >= Mathf.Abs(direction.x) && Mathf.Abs(direction.z) >= Mathf.Abs(direction.y))
        {
            return direction.z >= 0f ? "Beacon Pulse Light Upper" : "Beacon Pulse Light Lower";
        }

        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
        {
            return direction.y >= 0f ? "Beacon Pulse Light Upper" : "Beacon Pulse Light Lower";
        }

        return $"Beacon Pulse Light {index + 1}";
    }

    private float GetPulseLightIntensityScale(int index)
    {
        if (pulseLightIntensityScales == null || index < 0 || index >= pulseLightIntensityScales.Length)
        {
            return 1f;
        }

        return Mathf.Max(0f, pulseLightIntensityScales[index]);
    }

    private static Quaternion GetPulseLightLocalRotation(Vector3 localDirection)
    {
        Vector3 forward = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(forward, up);
    }

    private void ConfigurePulseLight(Light light, Vector3 localDirection)
    {
        if (light == null)
        {
            return;
        }

        light.type = pulseLightType;
        light.color = onColor;
        light.range = Mathf.Max(0.1f, pulseLightRange);
        light.intensity = 0f;
        light.enabled = false;
        light.shadows = pulseLightShadows;
        light.renderMode = LightRenderMode.Auto;

        if (pulseLightType == LightType.Spot)
        {
            light.spotAngle = Mathf.Clamp(pulseLightSpotAngle, 1f, 179f);
            light.innerSpotAngle = Mathf.Clamp(pulseLightInnerSpotAngle, 0f, light.spotAngle);
            light.transform.localRotation = GetPulseLightLocalRotation(localDirection);
        }
    }

    private void ApplyPulseLightIntensity(Light light, float lightIntensity)
    {
        if (light == null)
        {
            return;
        }

        light.enabled = lightIntensity > 0.001f;
        light.color = onColor;
        light.range = Mathf.Max(0.1f, pulseLightRange);
        light.intensity = lightIntensity;
    }

    private static PulseLightState CapturePulseLightState(Light light)
    {
        return new PulseLightState
        {
            Light = light,
            Enabled = light.enabled,
            Intensity = light.intensity,
            Range = light.range,
            Color = light.color,
            Type = light.type,
            SpotAngle = light.spotAngle,
            InnerSpotAngle = light.innerSpotAngle,
            Shadows = light.shadows,
            RenderMode = light.renderMode,
            LocalPosition = light.transform.localPosition,
            LocalRotation = light.transform.localRotation
        };
    }

    private static void RestorePulseLightState(PulseLightState state)
    {
        if (state.Light == null)
        {
            return;
        }

        state.Light.enabled = state.Enabled;
        state.Light.intensity = state.Intensity;
        state.Light.range = state.Range;
        state.Light.color = state.Color;
        state.Light.type = state.Type;
        state.Light.spotAngle = state.SpotAngle;
        state.Light.innerSpotAngle = state.InnerSpotAngle;
        state.Light.shadows = state.Shadows;
        state.Light.renderMode = state.RenderMode;
        state.Light.transform.localPosition = state.LocalPosition;
        state.Light.transform.localRotation = state.LocalRotation;
    }
}

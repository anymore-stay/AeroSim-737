using CesiumForUnity;
using System.Collections.Generic;
using UniStorm;
using UnityEngine;

/// <summary>
/// 只在满足高度和速度条件时启用 B737 航迹云，并在 Cesium/飞行器坐标回拉时同步平移已存在的粒子。
/// </summary>
[DefaultExecutionOrder(9000)]
public class B737ContrailController : MonoBehaviour
{
    [Header("引用设置")]
    [Tooltip("提供飞机实时海拔、离地高度和空速的 JsbsimBridge。通常绑定场景中的 B737/JsbsimBridge。")]
    [SerializeField] private JsbsimBridge bridge;

    [Tooltip("被 JsbsimBridge 驱动的飞机根节点。用于检测飞机是否被坐标回拉。")]
    [SerializeField] private Transform aircraft;

    [Tooltip("使用 Realistic Smoke 资源生成的左右航迹云 ParticleSystem。当前只使用这组烟雾。")]
    [SerializeField] private ParticleSystem[] realisticSmokeTrails;

    [Header("航迹云生效条件")]
    [Tooltip("开始产生航迹云所需的最低高度，单位 ft。高度和速度同时满足时才会开启。")]
    [Min(0f)]
    [SerializeField] private float minimumAltitudeFt = 26000f;

    [Tooltip("开始产生航迹云所需的最低校准空速，单位 kt。高度和速度同时满足时才会开启。")]
    [Min(0f)]
    [SerializeField] private float minimumSpeedKts = 250f;

    [Tooltip("高度关闭回差，单位 ft。开启后低于“最低高度 - 此值”才关闭，避免临界高度反复闪烁。")]
    [Min(0f)]
    [SerializeField] private float altitudeHysteresisFt = 500f;

    [Tooltip("速度关闭回差，单位 kt。开启后低于“最低速度 - 此值”才关闭，避免临界速度反复闪烁。")]
    [Min(0f)]
    [SerializeField] private float speedHysteresisKts = 10f;

    [Tooltip("勾选时使用海拔 AltitudeFt 判断；取消勾选时使用离地高度 AglFt 判断。高空航迹云通常使用海拔。")]
    [SerializeField] private bool useAltitudeAboveSeaLevel = true;

    [Tooltip("沿飞行路径每隔多少米手动生成一个烟雾粒子。数值越小越连续，但粒子数量和性能开销越高。")]
    [Min(0.02f)]
    [SerializeField] private float particleSpacingMeters = 2f;

    [Tooltip("航迹云透明粒子的渲染排序。保持比发动机热浪高，让烟雾后渲染，减少被热浪盖住的问题。")]
    [SerializeField] private int smokeSortingOrder = 20;

    [Header("夜间显示")]
    [Tooltip("用于读取当前时间的 UniStorm 系统。为空时会自动查找。")]
    [SerializeField] private UniStormSystem uniStormSystem;

    [Tooltip("夜晚开始小时，和夜间视觉控制器保持一致。")]
    [SerializeField, Range(0f, 24f)] private float nightStartHour = 19f;

    [Tooltip("夜晚结束小时，和夜间视觉控制器保持一致。")]
    [SerializeField, Range(0f, 24f)] private float nightEndHour = 5.5f;

    [Tooltip("昼夜切换过渡小时数。")]
    [SerializeField, Min(0f)] private float nightTransitionHours = 0.75f;

    [Tooltip("满夜时白色航迹云的目标颜色。默认接近黑夜里的微弱冷灰。")]
    [SerializeField] private Color nightSmokeTint = new Color(0.04f, 0.045f, 0.05f, 1f);

    [Tooltip("满夜时航迹云颜色保留比例。数值越低，白色尾迹越不显眼。")]
    [SerializeField, Range(0f, 1f)] private float nightSmokeBrightness = 0.08f;

    [Tooltip("满夜时航迹云透明度保留比例。数值越低，尾迹越淡。")]
    [SerializeField, Range(0f, 1f)] private float nightSmokeAlphaMultiplier = 0.18f;

    [Header("Cesium 回拉补偿")]
    [Tooltip("Cesium 原点变化时，飞机位移至少达到多少米才平移已有烟雾。用于忽略很小的坐标变化。")]
    [Min(0f)]
    [SerializeField] private float minimumRecenteringDistanceMeters = 10f;

    [Tooltip("勾选后在 Console 输出航迹开启、关闭以及坐标回拉补偿信息。仅建议调试时开启。")]
    [SerializeField] private bool logStateChanges;

    [Tooltip("飞机 Transform 单帧位移超过此值时，认为是坐标回拉而不是正常飞行，并同步平移已有烟雾。单位米。")]
    [Min(10f)]
    [SerializeField] private float transformJumpRecenteringDistanceMeters = 300f;

    private CesiumGeoreference georeference;
    private Vector3 lastAircraftPosition;
    private bool hasAircraftPosition;
    private bool emitting;
    private bool realisticSmokeTrailsResolved;
    private ParticleSystem.Particle[] particleBuffer;
    private Vector3[] lastRealisticSmokePositions;
    private bool realisticSmokePositionsInitialized;
    private MaterialPropertyBlock smokePropertyBlock;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private void Awake()
    {
        EnsureSmokePropertyBlock();
        ResolveReferences();
        SetEmission(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToGeoreference();
        CaptureAircraftPosition();
    }

    private void OnDisable()
    {
        UnsubscribeFromGeoreference();
    }

    private void LateUpdate()
    {
        EnsureSmokePropertyBlock();
        ResolveReferences();
        SubscribeToGeoreference();
        DetectTransformRecentering();
        UpdateEmissionState();
        EmitRealisticSmokeByDistance();
        ApplyNightSmokeToExistingParticles();
        CaptureAircraftPosition();
    }

    private void ResolveReferences()
    {
        if (bridge == null) bridge = JsbsimBridge.Instance;
        if (aircraft == null && bridge != null) aircraft = bridge.Aircraft;
        if (uniStormSystem == null) uniStormSystem = FindFirstObjectByType<UniStormSystem>();

        if (!realisticSmokeTrailsResolved || realisticSmokeTrails == null || realisticSmokeTrails.Length == 0)
        {
            realisticSmokeTrails = FindRealisticSmokeTrails();
            realisticSmokeTrailsResolved = true;
        }

        ConfigureRealisticSmokeRenderers();
    }

    private void EnsureSmokePropertyBlock()
    {
        if (smokePropertyBlock == null)
        {
            smokePropertyBlock = new MaterialPropertyBlock();
        }
    }

    private void SubscribeToGeoreference()
    {
        if (georeference != null || aircraft == null) return;

        georeference = aircraft.GetComponentInParent<CesiumGeoreference>();
        if (georeference == null) return;

        georeference.changed += HandleGeoreferenceChanged;
    }

    private void UnsubscribeFromGeoreference()
    {
        if (georeference == null) return;
        georeference.changed -= HandleGeoreferenceChanged;
        georeference = null;
    }

    private void CaptureAircraftPosition()
    {
        if (aircraft == null) return;
        lastAircraftPosition = aircraft.position;
        hasAircraftPosition = true;
    }

    private void HandleGeoreferenceChanged()
    {
        if (aircraft == null) return;

        Vector3 currentPosition = aircraft.position;
        if (!hasAircraftPosition)
        {
            lastAircraftPosition = currentPosition;
            hasAircraftPosition = true;
            return;
        }

        Vector3 displacement = currentPosition - lastAircraftPosition;
        lastAircraftPosition = currentPosition;

        if (displacement.sqrMagnitude < minimumRecenteringDistanceMeters * minimumRecenteringDistanceMeters)
            return;

        ApplyRecenteringOffset(displacement, "Cesium");
    }

    private void DetectTransformRecentering()
    {
        if (aircraft == null || !hasAircraftPosition) return;

        Vector3 currentPosition = aircraft.position;
        Vector3 displacement = currentPosition - lastAircraftPosition;
        float threshold = Mathf.Max(10f, transformJumpRecenteringDistanceMeters);
        if (displacement.sqrMagnitude < threshold * threshold)
            return;

        lastAircraftPosition = currentPosition;
        ApplyRecenteringOffset(displacement, "Transform");
    }

    private void ApplyRecenteringOffset(Vector3 displacement, string source)
    {
        ShiftRealisticSmokeParticles(displacement);

        if (lastRealisticSmokePositions != null)
        {
            for (int i = 0; i < lastRealisticSmokePositions.Length; i++)
                lastRealisticSmokePositions[i] += displacement;
        }

        if (logStateChanges)
            Debug.Log("[Contrail] Compensated " + source + " recenter by " + displacement.ToString("F2"), this);
    }

    private void UpdateEmissionState()
    {
        bool shouldEmit = false;
        if (bridge != null && bridge.HasState)
        {
            float altitude = useAltitudeAboveSeaLevel ? bridge.AltitudeFt : bridge.AglFt;
            float altitudeThreshold = emitting
                ? minimumAltitudeFt - altitudeHysteresisFt
                : minimumAltitudeFt;
            float speedThreshold = emitting
                ? minimumSpeedKts - speedHysteresisKts
                : minimumSpeedKts;

            shouldEmit = altitude >= altitudeThreshold && bridge.SpeedKts >= speedThreshold;
        }

        SetEmission(shouldEmit);
    }

    private void SetEmission(bool enabled)
    {
        SetRealisticSmokeEmission(enabled);
        if (emitting == enabled) return;

        emitting = enabled;
        realisticSmokePositionsInitialized = false;

        if (logStateChanges)
            Debug.Log("[Contrail] Emission " + (enabled ? "enabled" : "disabled"), this);
    }

    private ParticleSystem[] FindRealisticSmokeTrails()
    {
        ParticleSystem[] particleSystems = Resources.FindObjectsOfTypeAll<ParticleSystem>();
        List<ParticleSystem> matches = new List<ParticleSystem>();
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null || !particleSystem.name.Contains("B737RealisticContrail"))
                continue;

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(particleSystem))
                continue;
#endif

            if (!particleSystem.gameObject.scene.IsValid())
                continue;

                matches.Add(particleSystem);
        }

        return matches.ToArray();
    }

    private bool HasRealisticSmokeTrails()
    {
        if (realisticSmokeTrails == null) return false;

        for (int i = 0; i < realisticSmokeTrails.Length; i++)
        {
            if (realisticSmokeTrails[i] != null)
                return true;
        }

        return false;
    }

    private void SetRealisticSmokeEmission(bool enabled)
    {
        if (realisticSmokeTrails == null) return;

        for (int i = 0; i < realisticSmokeTrails.Length; i++)
        {
            ParticleSystem particleSystem = realisticSmokeTrails[i];
            if (particleSystem == null) continue;

            ConfigureRealisticSmokeRenderer(particleSystem);

            ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
            emissionModule.enabled = false;

            if (enabled)
            {
                if (!particleSystem.isPlaying)
                    particleSystem.Play(false);
            }
            else if (particleSystem.isPlaying)
            {
                particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void ConfigureRealisticSmokeRenderers()
    {
        if (realisticSmokeTrails == null) return;

        for (int i = 0; i < realisticSmokeTrails.Length; i++)
        {
            if (realisticSmokeTrails[i] != null)
                ConfigureRealisticSmokeRenderer(realisticSmokeTrails[i]);
        }
    }

    private void ConfigureRealisticSmokeRenderer(ParticleSystem particleSystem)
    {
        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;

        renderer.sortingOrder = smokeSortingOrder;
        renderer.sortingFudge = 4f;
        ApplyNightSmokeTint(renderer);
    }

    private void ApplyNightSmokeTint(ParticleSystemRenderer renderer)
    {
        EnsureSmokePropertyBlock();

        float nightBlend = GetNightBlend();
        Material[] materials = renderer.sharedMaterials;
        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            Material material = materials[materialIndex];
            if (material == null) continue;

            smokePropertyBlock.Clear();
            renderer.GetPropertyBlock(smokePropertyBlock, materialIndex);

            if (material.HasProperty(ColorId))
            {
                Color source = material.GetColor(ColorId);
                smokePropertyBlock.SetColor(
                    ColorId,
                    CalculateNightSmokeColor(source, nightSmokeTint, nightSmokeBrightness, nightSmokeAlphaMultiplier, nightBlend));
            }

            if (material.HasProperty(TintColorId))
            {
                Color source = material.GetColor(TintColorId);
                smokePropertyBlock.SetColor(
                    TintColorId,
                    CalculateNightSmokeColor(source, nightSmokeTint, nightSmokeBrightness, nightSmokeAlphaMultiplier, nightBlend));
            }

            renderer.SetPropertyBlock(smokePropertyBlock, materialIndex);
        }
    }

    private float GetNightBlend()
    {
        float hour = 12f;
        if (uniStormSystem != null)
        {
            hour = Mathf.Repeat(uniStormSystem.Hour + uniStormSystem.Minute / 60f, 24f);
        }

        return B737NightVisualController.CalculateNightBlend(hour, nightStartHour, nightEndHour, nightTransitionHours);
    }

    private void ShiftRealisticSmokeParticles(Vector3 offset)
    {
        if (realisticSmokeTrails == null) return;

        for (int i = 0; i < realisticSmokeTrails.Length; i++)
        {
            ParticleSystem particleSystem = realisticSmokeTrails[i];
            if (particleSystem == null) continue;

            int maxParticles = particleSystem.main.maxParticles;
            if (particleBuffer == null || particleBuffer.Length < maxParticles)
                particleBuffer = new ParticleSystem.Particle[maxParticles];

            int particleCount = particleSystem.GetParticles(particleBuffer);
            for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
                particleBuffer[particleIndex].position += offset;

            particleSystem.SetParticles(particleBuffer, particleCount);
        }
    }

    private void EmitRealisticSmokeByDistance()
    {
        if (!HasRealisticSmokeTrails()) return;

        if (lastRealisticSmokePositions == null || lastRealisticSmokePositions.Length != realisticSmokeTrails.Length)
        {
            lastRealisticSmokePositions = new Vector3[realisticSmokeTrails.Length];
            realisticSmokePositionsInitialized = false;
        }

        if (!emitting)
        {
            realisticSmokePositionsInitialized = false;
            return;
        }

        if (!realisticSmokePositionsInitialized)
        {
            for (int smokeIndex = 0; smokeIndex < realisticSmokeTrails.Length; smokeIndex++)
            {
                if (realisticSmokeTrails[smokeIndex] != null)
                    lastRealisticSmokePositions[smokeIndex] = realisticSmokeTrails[smokeIndex].transform.position;
            }

            realisticSmokePositionsInitialized = true;
            return;
        }

        float spacing = Mathf.Max(0.5f, particleSpacingMeters);
        int maxParticlesPerFrame = 16;
        for (int smokeIndex = 0; smokeIndex < realisticSmokeTrails.Length; smokeIndex++)
        {
            ParticleSystem particleSystem = realisticSmokeTrails[smokeIndex];
            if (particleSystem == null) continue;

            Vector3 currentPosition = particleSystem.transform.position;
            Vector3 segment = currentPosition - lastRealisticSmokePositions[smokeIndex];
            float distance = segment.magnitude;
            if (distance < spacing)
                continue;

            if (distance > transformJumpRecenteringDistanceMeters)
            {
                lastRealisticSmokePositions[smokeIndex] = currentPosition;
                continue;
            }

            Vector3 direction = segment / distance;
            int emitCount = Mathf.Min(Mathf.FloorToInt(distance / spacing), maxParticlesPerFrame);
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            float nightBlend = GetNightBlend();
            if (nightBlend > 0f)
            {
                emitParams.startColor = CalculateCurrentNightSmokeColor(nightBlend);
            }

            for (int particleIndex = 1; particleIndex <= emitCount; particleIndex++)
            {
                emitParams.position = lastRealisticSmokePositions[smokeIndex] + direction * spacing * particleIndex;
                emitParams.velocity = Vector3.zero;
                emitParams.applyShapeToPosition = true;
                particleSystem.Emit(emitParams, 1);
            }

            lastRealisticSmokePositions[smokeIndex] += direction * spacing * emitCount;
        }
    }

    private void ApplyNightSmokeToExistingParticles()
    {
        if (realisticSmokeTrails == null) return;

        float nightBlend = GetNightBlend();
        if (nightBlend <= 0f) return;

        Color smokeColor = CalculateCurrentNightSmokeColor(nightBlend);
        for (int smokeIndex = 0; smokeIndex < realisticSmokeTrails.Length; smokeIndex++)
        {
            ParticleSystem particleSystem = realisticSmokeTrails[smokeIndex];
            if (particleSystem == null) continue;

            int maxParticles = particleSystem.main.maxParticles;
            if (particleBuffer == null || particleBuffer.Length < maxParticles)
                particleBuffer = new ParticleSystem.Particle[maxParticles];

            int particleCount = particleSystem.GetParticles(particleBuffer);
            for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
                particleBuffer[particleIndex].startColor = smokeColor;

            if (particleCount > 0)
                particleSystem.SetParticles(particleBuffer, particleCount);
        }
    }

    private Color CalculateCurrentNightSmokeColor(float nightBlend)
    {
        return CalculateNightSmokeColor(Color.white, nightSmokeTint, nightSmokeBrightness, nightSmokeAlphaMultiplier, nightBlend);
    }

    public static Color CalculateNightSmokeColor(
        Color sourceColor,
        Color nightTint,
        float nightBrightness,
        float nightAlphaMultiplier,
        float nightBlend)
    {
        float blend = Mathf.Clamp01(nightBlend);
        float brightness = Mathf.Clamp01(nightBrightness);
        float alphaMultiplier = Mathf.Clamp01(nightAlphaMultiplier);

        Color target = new Color(
            sourceColor.r * brightness * Mathf.Clamp01(nightTint.r),
            sourceColor.g * brightness * Mathf.Clamp01(nightTint.g),
            sourceColor.b * brightness * Mathf.Clamp01(nightTint.b),
            sourceColor.a * alphaMultiplier);

        return Color.Lerp(sourceColor, target, blend);
    }
}

using System.Collections.Generic;
using UniStorm;
using UnityEngine;

/// <summary>
/// 将 UniStorm 的近景降水固定在当前实际渲染的相机附近，
/// 并额外生成一层相机中心的广域降水，兼顾自由第三人称视角和整机覆盖。
/// </summary>
[DefaultExecutionOrder(100)]
public class B737UniStormPrecipitationAnchor : MonoBehaviour
{
    private struct NearParticleDefaults
    {
        public ParticleSystem ParticleSystem;
        public ParticleSystemSimulationSpace SimulationSpace;
        public bool CollisionEnabled;
        public bool SubEmittersEnabled;
        public bool ShapeEnabled;
        public ParticleSystemShapeType ShapeType;
        public Vector3 ShapePosition;
        public Vector3 ShapeRotation;
        public Vector3 ShapeScale;
        public float ShapeRadius;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public bool EmissionEnabled;
        public ParticleSystem.MinMaxCurve EmissionRate;
        public int MaxParticles;
    }

    private struct ContainerDefaults
    {
        public Transform Transform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    [Header("引用")]
    [SerializeField] private UniStormSystem uniStormSystem;
    [SerializeField] private CameraManager cameraManager;

    [Header("近景降水范围（米）")]
    [SerializeField] private float minimumCoverageRadius = 100f;
    [SerializeField] private float maximumCoverageRadius = 140f;
    [SerializeField] private float minimumSnowHeight = 25f;

    [Header("近景降水密度")]
    [SerializeField, Range(1f, 3f)] private float nearDensityMultiplier = 2f;
    [SerializeField] private int maximumNearParticles = 10000;

    [Header("相机广域降水盒")]
    [SerializeField] private float distantBoxHalfExtent = 300f;
    [SerializeField] private float distantBoxHeight = 100f;
    [SerializeField] private float distantBoxEmitterHeight = 80f;
    [SerializeField, Range(0.01f, 0.5f)] private float distantDensityMultiplier = 0.1f;
    [SerializeField] private float maximumDistantEmissionRate = 7000f;
    [SerializeField] private int maximumDistantParticles = 12000;

    private Transform effectsTransform;
    private Transform soundsTransform;
    private ParticleSystem distantWeatherEffect;
    private ParticleSystem distantSourceEffect;
    private readonly Dictionary<int, NearParticleDefaults> nearParticleDefaults =
        new Dictionary<int, NearParticleDefaults>();
    private ContainerDefaults effectsContainerDefaults;
    private ContainerDefaults soundsContainerDefaults;

    private void Awake()
    {
        if (uniStormSystem == null)
        {
            uniStormSystem = GetComponent<UniStormSystem>();
        }

        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
        }
    }

    private void LateUpdate()
    {
        if (uniStormSystem == null || !uniStormSystem.UniStormInitialized || uniStormSystem.PlayerTransform == null)
        {
            return;
        }

        Camera activeCamera = GetActiveCamera();
        if (activeCamera == null)
        {
            return;
        }

        uniStormSystem.PlayerCamera = activeCamera;
        FindUniStormContainers();

        Vector3 nearAnchorPosition = CalculateNearAnchorPosition(activeCamera.transform.position);
        SetContainerPosition(effectsTransform, nearAnchorPosition);
        SetContainerPosition(soundsTransform, nearAnchorPosition);

        float nearCoverageRadius = CalculateDenseNearRadius(
            minimumCoverageRadius,
            maximumCoverageRadius);
        ApplyNearPrecipitationCoverage(nearAnchorPosition, nearCoverageRadius);
        ApplyNearPrecipitationDensity();

        UpdateDistantPrecipitation(nearAnchorPosition, nearCoverageRadius);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            ClearDistantPrecipitation();
            RestoreNearParticleDefaults();
            RestoreContainerDefaults(effectsContainerDefaults);
            RestoreContainerDefaults(soundsContainerDefaults);
            nearParticleDefaults.Clear();
            effectsContainerDefaults = default;
            soundsContainerDefaults = default;
        }
    }

    /// <summary>
    /// 近景雨雪必须以当前视角为中心，避免自由相机移动后离开降水体积。
    /// </summary>
    public static Vector3 CalculateNearAnchorPosition(Vector3 cameraPosition)
    {
        return cameraPosition;
    }

    /// <summary>
    /// 保留旧计算接口，便于已有工具和测试继续使用。
    /// </summary>
    public static Vector3 CalculateAnchorPosition(Vector3 playerPosition, Vector3 cameraPosition)
    {
        return Vector3.Lerp(playerPosition, cameraPosition, 0.5f);
    }

    /// <summary>
    /// 保留旧计算接口，便于已有工具和测试继续使用。
    /// </summary>
    public static float CalculateCoverageRadius(
        float cameraDistance,
        float minimumRadius,
        float aircraftMargin,
        float maximumRadius)
    {
        float requiredRadius = cameraDistance * 0.5f + aircraftMargin;
        return Mathf.Clamp(Mathf.Max(minimumRadius, requiredRadius), minimumRadius, maximumRadius);
    }

    /// <summary>
    /// 飞机周边补充层需要完整包住飞机、相机距离和风致横向漂移。
    /// </summary>
    public static float CalculateDistantCoverageRadius(
        float cameraDistance,
        float minimumRadius,
        float coverageMargin,
        float maximumRadius)
    {
        float requiredRadius = cameraDistance + coverageMargin;
        return Mathf.Clamp(Mathf.Max(minimumRadius, requiredRadius), minimumRadius, maximumRadius);
    }

    /// <summary>
    /// 远景补充层也以相机为中心，保证绕机旋转时各个方向的降水距离一致。
    /// </summary>
    public static Vector3 CalculateDistantAnchorPosition(Vector3 cameraPosition, Vector3 sourceOffset)
    {
        return cameraPosition + sourceOffset;
    }

    /// <summary>
    /// 广域层使用相机上方的世界轴对齐发射盒，避免半球形状只覆盖某一个水平方向。
    /// </summary>
    public static Vector3 CalculateDistantBoxAnchorPosition(Vector3 cameraPosition, float emitterHeight)
    {
        return cameraPosition + Vector3.up * Mathf.Max(0f, emitterHeight);
    }

    /// <summary>
    /// 返回以相机为中心的广域降水盒尺寸，X/Z 两轴保持完全对称。
    /// </summary>
    public static Vector3 CalculateDistantBoxSize(float halfExtent, float height)
    {
        float safeHalfExtent = Mathf.Max(1f, halfExtent);
        return new Vector3(safeHalfExtent * 2f, Mathf.Max(1f, height), safeHalfExtent * 2f);
    }

    /// <summary>
    /// 近景层也使用世界轴对齐的降水盒，避免半球发射在相机绕机时偏向固定方向。
    /// </summary>
    public static Vector3 CalculateNearBoxSize(float coverageRadius, float height)
    {
        return CalculateDistantBoxSize(coverageRadius, height);
    }

    /// <summary>
    /// 雪的下落速度较慢，发射盒必须贴近相机，避免粒子寿命结束前始终停留在高空。
    /// </summary>
    public static Vector2 CalculateSnowBoxVerticalSettings(float fallDistance)
    {
        float boxHeight = Mathf.Clamp(Mathf.Max(0f, fallDistance) * 0.75f, 20f, 30f);
        float emitterHeight = boxHeight * 0.5f + 5f;
        return new Vector2(emitterHeight, boxHeight);
    }

    /// <summary>
    /// 以平均存活时间反向补偿水平风，令存活雨滴的中心仍落在相机附近。
    /// </summary>
    public static Vector3 CalculateWindCompensatedAnchorPosition(
        Vector3 anchorPosition,
        Vector3 velocity,
        float particleLifetime)
    {
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        return anchorPosition - horizontalVelocity * Mathf.Max(0f, particleLifetime) * 0.5f;
    }

    /// <summary>
    /// UniStorm 的常量曲线可能保留无意义的 Min/Max 默认值，优先读取实际常量。
    /// </summary>
    public static float CalculateParticleCurveValue(
        float constantValue,
        float minimumValue,
        float maximumValue)
    {
        return !Mathf.Approximately(constantValue, 0f)
            ? constantValue
            : (minimumValue + maximumValue) * 0.5f;
    }

    /// <summary>
    /// 按广域盒平面面积与近景圆形面积的比例计算远景发射率。
    /// </summary>
    public static float CalculateDistantBoxEmissionRate(
        float nearEmissionRate,
        float referenceRadius,
        float halfExtent,
        float densityMultiplier,
        float maximumEmissionRate)
    {
        float safeReferenceRadius = Mathf.Max(1f, referenceRadius);
        float sideLength = Mathf.Max(1f, halfExtent) * 2f;
        float areaRatio = sideLength * sideLength / (Mathf.PI * safeReferenceRadius * safeReferenceRadius);
        float emissionRate = Mathf.Max(0f, nearEmissionRate) * areaRatio * Mathf.Max(0f, densityMultiplier);
        return Mathf.Min(emissionRate, Mathf.Max(0f, maximumEmissionRate));
    }

    /// <summary>
    /// 长寿命粒子不能超过远景层的存活粒子预算，避免达到上限后继续无效发射。
    /// </summary>
    public static float CalculateDistantBudgetedEmissionRate(
        float requestedEmissionRate,
        float particleLifetime,
        int particleBudget)
    {
        float budgetLimitedEmissionRate = Mathf.Max(0, particleBudget) / Mathf.Max(0.01f, particleLifetime);
        return Mathf.Min(Mathf.Max(0f, requestedEmissionRate), budgetLimitedEmissionRate);
    }

    /// <summary>
    /// 按覆盖面积提高远景层发射率，同时限制在预设的性能上限内。
    /// </summary>
    public static float CalculateDistantEmissionRate(
        float baseEmissionRate,
        float referenceRadius,
        float coverageRadius,
        float densityMultiplier,
        float maximumEmissionRate)
    {
        float safeReferenceRadius = Mathf.Max(1f, referenceRadius);
        float radiusRatio = Mathf.Max(0f, coverageRadius) / safeReferenceRadius;
        float emissionRate = Mathf.Max(0f, baseEmissionRate) * radiusRatio * radiusRatio * Mathf.Max(0f, densityMultiplier);
        return Mathf.Min(emissionRate, Mathf.Max(0f, maximumEmissionRate));
    }

    /// <summary>
    /// 在不超过存活粒子预算的前提下提高发射率，避免长寿命雪粒子被上限截断。
    /// </summary>
    public static float CalculateBudgetedEmissionRate(
        float baseEmissionRate,
        float densityMultiplier,
        float particleLifetime,
        int particleBudget)
    {
        float requestedEmissionRate = Mathf.Max(0f, baseEmissionRate) * Mathf.Max(0f, densityMultiplier);
        float budgetLimitedEmissionRate = Mathf.Max(0, particleBudget) / Mathf.Max(0.01f, particleLifetime);
        return Mathf.Min(requestedEmissionRate, budgetLimitedEmissionRate);
    }

    /// <summary>
    /// 近景层允许比 UniStorm 预制体的默认发射半径更小，以换取更高的画面粒子密度。
    /// </summary>
    public static float CalculateDenseNearRadius(float requestedRadius, float maximumRadius)
    {
        return Mathf.Clamp(requestedRadius, 1f, Mathf.Max(1f, maximumRadius));
    }

    /// <summary>
    /// 所有跟随相机移动的降水都使用本地模拟，使已有粒子与相机一起移动。
    /// </summary>
    public static ParticleSystemSimulationSpace GetCameraFollowSimulationSpace()
    {
        return ParticleSystemSimulationSpace.Local;
    }

    private Camera GetActiveCamera()
    {
        if (IsUsableCamera(cameraManager != null ? cameraManager.ActiveCamera : null))
        {
            return cameraManager.ActiveCamera;
        }

        if (IsUsableCamera(uniStormSystem.PlayerCamera))
        {
            return uniStormSystem.PlayerCamera;
        }

        Camera[] activeCameras = Camera.allCameras;
        for (int index = 0; index < activeCameras.Length; index++)
        {
            Camera candidate = activeCameras[index];
            AudioListener listener = candidate != null ? candidate.GetComponent<AudioListener>() : null;
            if (IsUsableCamera(candidate) && listener != null && listener.enabled)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsUsableCamera(Camera camera)
    {
        return camera != null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
    }

    private void FindUniStormContainers()
    {
        if (effectsTransform == null)
        {
            effectsTransform = FindDescendant("UniStorm Effects");
        }

        if (soundsTransform == null)
        {
            soundsTransform = FindDescendant("UniStorm Sounds");
        }

        CacheContainerDefaults(effectsTransform, ref effectsContainerDefaults);
        CacheContainerDefaults(soundsTransform, ref soundsContainerDefaults);
    }

    private Transform FindDescendant(string objectName)
    {
        Transform[] transforms = uniStormSystem.PlayerTransform.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            if (transforms[index].name == objectName)
            {
                return transforms[index];
            }
        }

        return null;
    }

    private static void SetContainerPosition(Transform container, Vector3 position)
    {
        if (container == null)
        {
            return;
        }

        container.SetPositionAndRotation(position, Quaternion.identity);
    }

    private static void CacheContainerDefaults(Transform container, ref ContainerDefaults defaults)
    {
        if (container == null || defaults.Transform != null)
        {
            return;
        }

        defaults = new ContainerDefaults
        {
            Transform = container,
            LocalPosition = container.localPosition,
            LocalRotation = container.localRotation
        };
    }

    private static void RestoreContainerDefaults(ContainerDefaults defaults)
    {
        if (defaults.Transform == null)
        {
            return;
        }

        defaults.Transform.localPosition = defaults.LocalPosition;
        defaults.Transform.localRotation = defaults.LocalRotation;
    }

    private void ApplyNearPrecipitationCoverage(Vector3 cameraPosition, float coverageRadius)
    {
        for (int index = 0; index < uniStormSystem.WeatherEffectsList.Count; index++)
        {
            ParticleSystem particleSystem = uniStormSystem.WeatherEffectsList[index];
            if (particleSystem == null || !IsPrecipitationEffect(particleSystem.name))
            {
                continue;
            }

            ConfigureNearParticleSystem(particleSystem);
            ApplyNearPrecipitationBox(particleSystem, cameraPosition, coverageRadius);
        }
    }

    private void ApplyNearPrecipitationDensity()
    {
        WeatherType currentWeatherType = uniStormSystem.CurrentWeatherType;
        ParticleSystem currentEffect = uniStormSystem.CurrentParticleSystem;
        if (currentWeatherType == null ||
            currentWeatherType.PrecipitationWeatherType != WeatherType.Yes_No.Yes ||
            currentEffect == null ||
            !IsPrecipitationEffect(currentEffect.name))
        {
            return;
        }

        ParticleSystem.MainModule main = currentEffect.main;
        float lifetime = Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
        float baseEmissionRate = currentWeatherType.ParticleEffectAmount > 0
            ? currentWeatherType.ParticleEffectAmount
            : currentEffect.emission.rateOverTime.constant;
        float emissionRate = CalculateBudgetedEmissionRate(
            baseEmissionRate,
            nearDensityMultiplier,
            lifetime,
            maximumNearParticles);

        ParticleSystem.EmissionModule emission = currentEffect.emission;
        emission.enabled = emissionRate > 0f;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);
        main.maxParticles = Mathf.Max(
            main.maxParticles,
            Mathf.Min(Mathf.CeilToInt(emissionRate * Mathf.Max(1f, lifetime)), maximumNearParticles));
    }

    private void UpdateDistantPrecipitation(Vector3 cameraPosition, float nearCoverageRadius)
    {
        WeatherType currentWeatherType = uniStormSystem.CurrentWeatherType;
        ParticleSystem sourceEffect = uniStormSystem.CurrentParticleSystem;
        if (currentWeatherType == null ||
            currentWeatherType.PrecipitationWeatherType != WeatherType.Yes_No.Yes ||
            sourceEffect == null ||
            !IsPrecipitationEffect(sourceEffect.name))
        {
            ClearDistantPrecipitation();
            return;
        }

        if (distantWeatherEffect == null || distantSourceEffect != sourceEffect)
        {
            CreateDistantPrecipitation(sourceEffect);
        }

        if (distantWeatherEffect == null)
        {
            return;
        }

        Vector2 verticalSettings = GetPrecipitationBoxVerticalSettings(sourceEffect);
        distantWeatherEffect.transform.SetPositionAndRotation(
            CalculateWindCompensatedBoxAnchorPosition(
                cameraPosition,
                distantWeatherEffect,
                verticalSettings.x),
            Quaternion.identity);

        ApplyDistantBoxShape(distantWeatherEffect, verticalSettings.y);

        float referenceRadius = Mathf.Max(1f, nearCoverageRadius);
        float requestedDistantEmissionRate = CalculateDistantBoxEmissionRate(
            sourceEffect.emission.rateOverTime.constant,
            referenceRadius,
            distantBoxHalfExtent,
            distantDensityMultiplier,
            maximumDistantEmissionRate);

        ParticleSystem.MainModule main = distantWeatherEffect.main;
        float lifetime = Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
        float distantEmissionRate = CalculateDistantBudgetedEmissionRate(
            requestedDistantEmissionRate,
            lifetime,
            maximumDistantParticles);

        ParticleSystem.EmissionModule emission = distantWeatherEffect.emission;
        emission.enabled = distantEmissionRate > 0f;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(distantEmissionRate);

        int requiredParticles = Mathf.CeilToInt(distantEmissionRate * Mathf.Max(1f, lifetime));
        main.maxParticles = Mathf.Max(main.maxParticles, Mathf.Min(requiredParticles, maximumDistantParticles));
    }

    private void CreateDistantPrecipitation(ParticleSystem sourceEffect)
    {
        ClearDistantPrecipitation();

        distantWeatherEffect = Instantiate(sourceEffect);
        distantWeatherEffect.name = "UniStorm 远景补充降水";
        distantWeatherEffect.transform.SetParent(uniStormSystem.transform, true);
        distantSourceEffect = sourceEffect;

        ParticleSystem[] nestedEffects = distantWeatherEffect.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < nestedEffects.Length; index++)
        {
            if (nestedEffects[index] != distantWeatherEffect)
            {
                nestedEffects[index].gameObject.SetActive(false);
            }
        }

        distantWeatherEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = distantWeatherEffect.main;
        main.simulationSpace = GetCameraFollowSimulationSpace();
        ParticleSystem.CollisionModule collision = distantWeatherEffect.collision;
        collision.enabled = false;
        ParticleSystem.SubEmittersModule subEmitters = distantWeatherEffect.subEmitters;
        subEmitters.enabled = false;
        distantWeatherEffect.Play(true);
    }

    private void ClearDistantPrecipitation()
    {
        if (distantWeatherEffect != null)
        {
            Destroy(distantWeatherEffect.gameObject);
        }

        distantWeatherEffect = null;
        distantSourceEffect = null;
    }

    private void ApplyNearPrecipitationBox(
        ParticleSystem particleSystem,
        Vector3 cameraPosition,
        float coverageRadius)
    {
        Vector2 verticalSettings = GetPrecipitationBoxVerticalSettings(particleSystem);
        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = CalculateNearBoxSize(
            coverageRadius,
            verticalSettings.y);

        particleSystem.transform.SetPositionAndRotation(
            CalculateWindCompensatedBoxAnchorPosition(
                cameraPosition,
                particleSystem,
                verticalSettings.x),
            Quaternion.identity);
    }

    private void ConfigureNearParticleSystem(ParticleSystem particleSystem)
    {
        CacheNearParticleDefaults(particleSystem);

        ParticleSystem.MainModule main = particleSystem.main;
        if (main.simulationSpace != GetCameraFollowSimulationSpace())
        {
            main.simulationSpace = GetCameraFollowSimulationSpace();
            particleSystem.Clear(true);
        }

        ParticleSystem.CollisionModule collision = particleSystem.collision;
        collision.enabled = false;
        ParticleSystem.SubEmittersModule subEmitters = particleSystem.subEmitters;
        subEmitters.enabled = false;
    }

    private void CacheNearParticleDefaults(ParticleSystem particleSystem)
    {
        int instanceId = particleSystem.GetInstanceID();
        if (nearParticleDefaults.ContainsKey(instanceId))
        {
            return;
        }

        ParticleSystem.MainModule main = particleSystem.main;
        ParticleSystem.CollisionModule collision = particleSystem.collision;
        ParticleSystem.SubEmittersModule subEmitters = particleSystem.subEmitters;
        ParticleSystem.ShapeModule shape = particleSystem.shape;
        ParticleSystem.EmissionModule emission = particleSystem.emission;
        nearParticleDefaults.Add(instanceId, new NearParticleDefaults
        {
            ParticleSystem = particleSystem,
            SimulationSpace = main.simulationSpace,
            CollisionEnabled = collision.enabled,
            SubEmittersEnabled = subEmitters.enabled,
            ShapeEnabled = shape.enabled,
            ShapeType = shape.shapeType,
            ShapePosition = shape.position,
            ShapeRotation = shape.rotation,
            ShapeScale = shape.scale,
            ShapeRadius = shape.radius,
            LocalPosition = particleSystem.transform.localPosition,
            LocalRotation = particleSystem.transform.localRotation,
            EmissionEnabled = emission.enabled,
            EmissionRate = emission.rateOverTime,
            MaxParticles = main.maxParticles
        });
    }

    private void RestoreNearParticleDefaults()
    {
        foreach (NearParticleDefaults defaults in nearParticleDefaults.Values)
        {
            if (defaults.ParticleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = defaults.ParticleSystem.main;
            main.simulationSpace = defaults.SimulationSpace;
            main.maxParticles = defaults.MaxParticles;

            ParticleSystem.CollisionModule collision = defaults.ParticleSystem.collision;
            collision.enabled = defaults.CollisionEnabled;
            ParticleSystem.SubEmittersModule subEmitters = defaults.ParticleSystem.subEmitters;
            subEmitters.enabled = defaults.SubEmittersEnabled;

            ParticleSystem.ShapeModule shape = defaults.ParticleSystem.shape;
            shape.enabled = defaults.ShapeEnabled;
            shape.shapeType = defaults.ShapeType;
            shape.position = defaults.ShapePosition;
            shape.rotation = defaults.ShapeRotation;
            shape.scale = defaults.ShapeScale;
            shape.radius = defaults.ShapeRadius;

            defaults.ParticleSystem.transform.localPosition = defaults.LocalPosition;
            defaults.ParticleSystem.transform.localRotation = defaults.LocalRotation;

            ParticleSystem.EmissionModule emission = defaults.ParticleSystem.emission;
            emission.enabled = defaults.EmissionEnabled;
            emission.rateOverTime = defaults.EmissionRate;
        }
    }

    private void ApplyDistantBoxShape(ParticleSystem particleSystem, float height)
    {
        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = CalculateDistantBoxSize(distantBoxHalfExtent, height);
    }

    private Vector2 GetPrecipitationBoxVerticalSettings(ParticleSystem particleSystem)
    {
        if (!IsSnowEffect(particleSystem.name))
        {
            return new Vector2(distantBoxEmitterHeight, distantBoxHeight);
        }

        ParticleSystem.MainModule main = particleSystem.main;
        float lifetime = Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        float downwardSpeed = velocity.enabled
            ? Mathf.Max(0f, -CalculateParticleCurveValue(
                velocity.y.constant,
                velocity.y.constantMin,
                velocity.y.constantMax))
            : 0f;
        float fallDistance = downwardSpeed * lifetime;
        Vector2 settings = CalculateSnowBoxVerticalSettings(fallDistance);
        float boxHeight = Mathf.Max(settings.y, Mathf.Clamp(minimumSnowHeight, 1f, 30f));
        return new Vector2(boxHeight * 0.5f + 5f, boxHeight);
    }

    private static Vector3 CalculateWindCompensatedBoxAnchorPosition(
        Vector3 cameraPosition,
        ParticleSystem particleSystem,
        float emitterHeight)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        float averageLifetime = Mathf.Max(0f, CalculateParticleCurveValue(
            main.startLifetime.constant,
            main.startLifetime.constantMin,
            main.startLifetime.constantMax));
        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        Vector3 averageVelocity = velocity.enabled
            ? new Vector3(
                CalculateParticleCurveValue(
                    velocity.x.constant,
                    velocity.x.constantMin,
                    velocity.x.constantMax),
                CalculateParticleCurveValue(
                    velocity.y.constant,
                    velocity.y.constantMin,
                    velocity.y.constantMax),
                CalculateParticleCurveValue(
                    velocity.z.constant,
                    velocity.z.constantMin,
                    velocity.z.constantMax))
            : Vector3.zero;
        Vector3 anchorPosition = CalculateDistantBoxAnchorPosition(cameraPosition, emitterHeight);
        return CalculateWindCompensatedAnchorPosition(anchorPosition, averageVelocity, averageLifetime);
    }

    private static bool IsPrecipitationEffect(string effectName)
    {
        return effectName.Contains("Rain")
            || effectName.Contains("Snow")
            || effectName.Contains("Drizzle")
            || effectName.Contains("Hail");
    }

    private static bool IsSnowEffect(string effectName)
    {
        return effectName.Contains("Snow");
    }
}

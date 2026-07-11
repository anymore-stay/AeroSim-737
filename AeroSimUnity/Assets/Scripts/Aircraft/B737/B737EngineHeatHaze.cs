using System;
using UnityEngine;

/// <summary>
/// B737 发动机尾流热浪。使用透明粒子承载屏幕扰动材质，N1 越高热浪越明显。
/// </summary>
[DisallowMultipleComponent]
public class B737EngineHeatHaze : MonoBehaviour
{
    [Serializable]
    public class EngineExhaust
    {
        public string label = "Engine";
        public string engineRootNameContains = "";
        public Transform engineRoot;
        public Transform exhaustPoint;
        public string n1Key = "";
        [Tooltip("喷流方向，基于飞机根物体本地坐标。本项目机头朝 -Z，尾喷方向默认 +Z。")]
        public Vector3 aircraftLocalFlowDirection = Vector3.forward;
        [Tooltip("自动创建喷口时，在发动机包围盒尾侧额外外推的距离。")]
        public float tailOffsetMeters = 0.15f;
        public float radiusMultiplier = 1f;
        public float intensityMultiplier = 1f;

        [NonSerialized] public ParticleSystem particles;
        [NonSerialized] public ParticleSystemRenderer renderer;
    }

    [Header("数据源")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private bool autoFindBridge = true;
    [SerializeField] private B737EngineSpinner engineSpinner;
    [SerializeField] private bool autoFindEngineSpinner = true;

    [Header("材质")]
    [SerializeField] private Material heatHazeMaterial;
    [SerializeField] private bool createRuntimeMaterialWhenMissing = true;

    [Header("发动机")]
    [SerializeField] private bool autoFindEngineRoots = true;
    [SerializeField] private EngineExhaust leftEngine = new EngineExhaust
    {
        label = "Left Engine",
        engineRootNameContains = "左发动机",
        n1Key = "propulsion_engine_n1",
        aircraftLocalFlowDirection = Vector3.forward,
        tailOffsetMeters = 0.18f
    };

    [SerializeField] private EngineExhaust rightEngine = new EngineExhaust
    {
        label = "Right Engine",
        engineRootNameContains = "右发动机",
        n1Key = "propulsion_engine_1_n1",
        aircraftLocalFlowDirection = Vector3.forward,
        tailOffsetMeters = 0.18f
    };

    [Header("N1 映射")]
    [SerializeField] private float idleN1 = 18f;
    [SerializeField] private float maxN1 = 95f;
    [SerializeField] private float previewN1WhenNoJsbsim = 70f;

    [Header("粒子强度")]
    [SerializeField] private float minEmissionRate = 6f;
    [SerializeField] private float maxEmissionRate = 140f;
    [SerializeField] private float minSpeed = 1.8f;
    [SerializeField] private float maxSpeed = 18f;
    [SerializeField] private float minStartSize = 0.28f;
    [SerializeField] private float maxStartSize = 1.35f;
    [SerializeField] private float minAlpha = 0.04f;
    [SerializeField] private float maxAlpha = 0.24f;
    [SerializeField] private float particleLifetime = 0.75f;
    [SerializeField] private float shapeRadius = 0.28f;
    [SerializeField] private float shapeAngle = 10f;
    [Tooltip("热浪透明粒子的渲染排序。保持比航迹云低，让热浪先渲染，避免盖住后方烟雾。")]
    [SerializeField] private int heatHazeSortingOrder = -20;

    [Header("调试")]
    [SerializeField] private bool logMissingRoots;
    [SerializeField] private bool showEmitterGizmos = true;

    private bool rigReady;
    private Material runtimeMaterial;

    private void Awake()
    {
        AttachBridge();
        AttachEngineSpinner();
        EnsureRuntimeRig();
    }

    private void OnEnable()
    {
        EnsureRuntimeRig();
        Play(leftEngine);
        Play(rightEngine);
    }

    private void OnDisable()
    {
        Stop(leftEngine);
        Stop(rightEngine);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }
        }
    }

    private void Update()
    {
        if (!rigReady)
        {
            EnsureRuntimeRig();
            if (!rigReady)
            {
                return;
            }
        }

        AttachBridge();
        AttachEngineSpinner();
        UpdateEngine(leftEngine);
        UpdateEngine(rightEngine);
    }

    public void SetHeatHazeMaterial(Material material)
    {
        heatHazeMaterial = material;
    }

    [ContextMenu("Rebuild Heat Haze Emitters")]
    public void RebuildEmitters()
    {
        rigReady = false;
        EnsureRuntimeRig();
    }

    private void AttachBridge()
    {
        if (bridge != null || !autoFindBridge)
        {
            return;
        }

        bridge = GetComponent<JsbsimBridge>();
        if (bridge == null)
        {
            bridge = GetComponentInParent<JsbsimBridge>();
        }
        if (bridge == null)
        {
            bridge = FindObjectOfType<JsbsimBridge>();
        }
    }

    private void AttachEngineSpinner()
    {
        if (engineSpinner != null || !autoFindEngineSpinner)
        {
            return;
        }

        engineSpinner = GetComponent<B737EngineSpinner>();
        if (engineSpinner == null)
        {
            engineSpinner = GetComponentInParent<B737EngineSpinner>();
        }
        if (engineSpinner == null)
        {
            engineSpinner = FindObjectOfType<B737EngineSpinner>();
        }
    }

    private void EnsureRuntimeRig()
    {
        AttachEngineSpinner();
        Material material = ResolveMaterial();
        bool leftReady = EnsureEngine(leftEngine, material);
        bool rightReady = EnsureEngine(rightEngine, material);
        rigReady = leftReady && rightReady;
    }

    private bool EnsureEngine(EngineExhaust engine, Material material)
    {
        if (engine == null)
        {
            return false;
        }

        if (engine.engineRoot == null && autoFindEngineRoots)
        {
            engine.engineRoot = FindDeepChild(transform, engine.engineRootNameContains);
        }

        if (engine.engineRoot == null && autoFindEngineRoots)
        {
            engine.engineRoot = FindEngineRootFromSpinner(engine);
        }

        if (engine.engineRoot == null)
        {
            if (logMissingRoots)
            {
                Debug.LogWarning($"{nameof(B737EngineHeatHaze)} 未找到 {engine.label} 根节点：{engine.engineRootNameContains}", this);
            }
            return false;
        }

        if (engine.exhaustPoint == null)
        {
            string emitterName = $"HeatHaze_{SanitizeName(engine.label)}_Emitter";
            Transform existing = engine.engineRoot.Find(emitterName);
            if (existing != null)
            {
                engine.exhaustPoint = existing;
            }
            else
            {
                GameObject emitterGo = new GameObject(emitterName);
                engine.exhaustPoint = emitterGo.transform;
                engine.exhaustPoint.SetParent(engine.engineRoot, false);
                PositionExhaustPoint(engine);
            }
        }

        engine.particles = engine.exhaustPoint.GetComponent<ParticleSystem>();
        if (engine.particles == null)
        {
            engine.particles = engine.exhaustPoint.gameObject.AddComponent<ParticleSystem>();
        }

        engine.renderer = engine.exhaustPoint.GetComponent<ParticleSystemRenderer>();
        if (engine.renderer == null)
        {
            engine.renderer = engine.exhaustPoint.gameObject.AddComponent<ParticleSystemRenderer>();
        }

        ConfigureParticleSystem(engine, material);
        return true;
    }

    private void PositionExhaustPoint(EngineExhaust engine)
    {
        Vector3 flowDirection = TransformDirectionOrFallback(engine.aircraftLocalFlowDirection, transform.forward);
        if (TryCalculateEngineBounds(engine, out Bounds bounds))
        {
            float extent = ProjectExtent(bounds, flowDirection);
            engine.exhaustPoint.position = bounds.center + flowDirection * (extent + engine.tailOffsetMeters);
        }
        else
        {
            engine.exhaustPoint.position = engine.engineRoot.position + flowDirection * engine.tailOffsetMeters;
        }

        engine.exhaustPoint.rotation = Quaternion.LookRotation(flowDirection, transform.up);
        engine.exhaustPoint.localScale = Vector3.one;
    }

    private void ConfigureParticleSystem(EngineExhaust engine, Material material)
    {
        ParticleSystem ps = engine.particles;
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = particleLifetime;
        main.startSpeed = minSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(minStartSize * 0.7f, minStartSize * 1.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, minAlpha * 0.5f),
            new Color(1f, 1f, 1f, minAlpha));
        main.maxParticles = 180;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = minEmissionRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = shapeAngle;
        shape.radius = shapeRadius * Mathf.Max(0.1f, engine.radiusMultiplier);
        shape.length = 0.35f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0.35f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 1.75f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 1.8f;
        noise.scrollSpeed = 0.75f;

        engine.renderer.renderMode = ParticleSystemRenderMode.Billboard;
        engine.renderer.alignment = ParticleSystemRenderSpace.View;
        engine.renderer.sortingOrder = heatHazeSortingOrder;
        engine.renderer.sortingFudge = -4f;
        engine.renderer.sharedMaterial = material;
    }

    private void UpdateEngine(EngineExhaust engine)
    {
        if (engine?.particles == null)
        {
            return;
        }

        float n1 = ReadN1(engine);
        float power = Mathf.InverseLerp(idleN1, maxN1, n1);
        power = Mathf.SmoothStep(0f, 1f, power) * Mathf.Max(0f, engine.intensityMultiplier);

        var emission = engine.particles.emission;
        emission.rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, power);

        var main = engine.particles.main;
        float speed = Mathf.Lerp(minSpeed, maxSpeed, power);
        float size = Mathf.Lerp(minStartSize, maxStartSize, power) * Mathf.Max(0.1f, engine.radiusMultiplier);
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, power);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.75f, speed * 1.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size * 1.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, alpha * 0.45f),
            new Color(1f, 1f, 1f, alpha));

        if (!engine.particles.isPlaying)
        {
            engine.particles.Play();
        }
    }

    private Transform FindEngineRootFromSpinner(EngineExhaust engine)
    {
        B737EngineSpinner.EngineSide side = GetSpinnerSide(engine);
        if (side == null || side.blades == null || side.blades.Length == 0)
        {
            return null;
        }

        Transform commonParent = FindCommonParent(side.blades);
        if (commonParent != null)
        {
            return commonParent;
        }

        for (int i = 0; i < side.blades.Length; i++)
        {
            Transform blade = side.blades[i];
            if (blade != null)
            {
                return blade.parent != null ? blade.parent : blade;
            }
        }

        return null;
    }

    private B737EngineSpinner.EngineSide GetSpinnerSide(EngineExhaust engine)
    {
        if (engineSpinner == null || engine == null)
        {
            return null;
        }

        return engine.label.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0
            ? engineSpinner.RightEngine
            : engineSpinner.LeftEngine;
    }

    private float ReadN1(EngineExhaust engine)
    {
        if (bridge != null && bridge.HasState && !string.IsNullOrWhiteSpace(engine.n1Key) &&
            bridge.TryGetValue(engine.n1Key, out float n1))
        {
            return Mathf.Abs(n1);
        }

        if (bridge != null && bridge.HasState)
        {
            return Mathf.Abs(bridge.Rpm);
        }

        return previewN1WhenNoJsbsim;
    }

    private Material ResolveMaterial()
    {
        if (heatHazeMaterial != null)
        {
            return heatHazeMaterial;
        }

        if (!createRuntimeMaterialWhenMissing)
        {
            return null;
        }

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("AeroSim/B737/Heat Haze Distortion");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader)
                {
                    name = "B737HeatHaze_Runtime"
                };
            }
        }

        return runtimeMaterial;
    }

    private static bool TryCalculateBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private bool TryCalculateEngineBounds(EngineExhaust engine, out Bounds bounds)
    {
        B737EngineSpinner.EngineSide side = GetSpinnerSide(engine);
        if (side != null && TryCalculateBounds(side.blades, out bounds))
        {
            return true;
        }

        return TryCalculateBounds(engine.engineRoot, out bounds);
    }

    private static bool TryCalculateBounds(Transform[] roots, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (roots == null)
        {
            return false;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null || !TryCalculateBounds(root, out Bounds rootBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = rootBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rootBounds);
            }
        }

        return hasBounds;
    }

    private static float ProjectExtent(Bounds bounds, Vector3 direction)
    {
        Vector3 dir = direction.normalized;
        return Mathf.Abs(Vector3.Dot(dir, Vector3.right)) * bounds.extents.x +
               Mathf.Abs(Vector3.Dot(dir, Vector3.up)) * bounds.extents.y +
               Mathf.Abs(Vector3.Dot(dir, Vector3.forward)) * bounds.extents.z;
    }

    private Vector3 TransformDirectionOrFallback(Vector3 localDirection, Vector3 fallback)
    {
        return localDirection.sqrMagnitude > 0.0001f
            ? transform.TransformDirection(localDirection).normalized
            : fallback.normalized;
    }

    private static Transform FindDeepChild(Transform root, string nameContains)
    {
        if (root == null || string.IsNullOrWhiteSpace(nameContains))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindCommonParent(Transform[] transforms)
    {
        Transform common = null;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null)
            {
                continue;
            }

            current = current.parent != null ? current.parent : current;
            common = common == null ? current : FindCommonAncestor(common, current);
        }

        return common;
    }

    private static Transform FindCommonAncestor(Transform a, Transform b)
    {
        Transform current = a;
        while (current != null)
        {
            if (b != null && b.IsChildOf(current))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Engine";
        }

        return value.Replace(" ", "");
    }

    private static void Play(EngineExhaust engine)
    {
        if (engine?.particles != null && !engine.particles.isPlaying)
        {
            engine.particles.Play();
        }
    }

    private static void Stop(EngineExhaust engine)
    {
        if (engine?.particles != null)
        {
            engine.particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showEmitterGizmos)
        {
            return;
        }

        DrawEmitterGizmo(leftEngine, Color.cyan);
        DrawEmitterGizmo(rightEngine, new Color(0.4f, 0.8f, 1f, 1f));
    }

    private void DrawEmitterGizmo(EngineExhaust engine, Color color)
    {
        if (engine?.exhaustPoint == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(engine.exhaustPoint.position, shapeRadius * Mathf.Max(0.1f, engine.radiusMultiplier));
        Gizmos.DrawRay(engine.exhaustPoint.position, engine.exhaustPoint.forward * 1.5f);
    }
}

using System;
using UnityEngine;

[DisallowMultipleComponent]
public class B737EngineSpinner : MonoBehaviour
{
    [Serializable]
    public class EngineSide
    {
        public string label;
        public Transform[] blades = Array.Empty<Transform>();
        public float rpmMultiplier = 1f;
        public Vector3 localSpinAxis = Vector3.up;

        [HideInInspector] public RuntimeBladePivot[] runtimePivots = Array.Empty<RuntimeBladePivot>();
    }

    public sealed class RuntimeBladePivot
    {
        public Transform visual;
        public Transform pivot;
        public Quaternion baseLocalRotation;
    }

    [Header("数据源")]
    [SerializeField] private JsbsimBridge bridge;

    [Header("旋转映射")]
    [SerializeField] private float rpmToDegreesPerSecond = 6f;
    [SerializeField] private float minVisibleRpm = 50f;
    [SerializeField] private bool useAbsoluteRpm = true;

    [Header("左右发动机")]
    [SerializeField] private EngineSide leftEngine = new EngineSide
    {
        label = "Left Engine",
        localSpinAxis = Vector3.up,
        rpmMultiplier = 1f
    };

    [SerializeField] private EngineSide rightEngine = new EngineSide
    {
        label = "Right Engine",
        localSpinAxis = Vector3.up,
        rpmMultiplier = 1f
    };

    [Header("调试")]
    [SerializeField] private bool hideGeneratedPivots = true;
    [SerializeField] private float currentRpm;

    private bool rigBuilt;

    public EngineSide LeftEngine => leftEngine;
    public EngineSide RightEngine => rightEngine;

    private void Awake()
    {
        if (bridge == null)
        {
            bridge = GetComponent<JsbsimBridge>();
        }
    }

    private void Start()
    {
        rigBuilt = BuildRuntimeRig(leftEngine) & BuildRuntimeRig(rightEngine);
        if (!rigBuilt)
        {
            Debug.LogWarning($"{nameof(B737EngineSpinner)} 初始化失败，请检查发动机叶片引用。", this);
        }
    }

    private void Update()
    {
        if (!rigBuilt)
        {
            return;
        }

        currentRpm = bridge != null && bridge.HasState ? bridge.Rpm : 0f;

        float rpm = useAbsoluteRpm ? Mathf.Abs(currentRpm) : currentRpm;
        if (rpm < minVisibleRpm)
        {
            return;
        }

        RotateEngine(leftEngine, rpm);
        RotateEngine(rightEngine, rpm);
    }

    private void RotateEngine(EngineSide engine, float rpm)
    {
        if (engine.runtimePivots == null)
        {
            return;
        }

        float degreesPerSecond = rpm * rpmToDegreesPerSecond * engine.rpmMultiplier;
        float deltaAngle = degreesPerSecond * Time.deltaTime;
        Vector3 axis = NormalizeOrFallback(engine.localSpinAxis, Vector3.up);

        for (int i = 0; i < engine.runtimePivots.Length; i++)
        {
            RuntimeBladePivot runtime = engine.runtimePivots[i];
            if (runtime?.pivot == null)
            {
                continue;
            }

            runtime.pivot.localRotation *= Quaternion.AngleAxis(deltaAngle, axis);
        }
    }

    private bool BuildRuntimeRig(EngineSide engine)
    {
        if (engine == null || engine.blades == null || engine.blades.Length == 0)
        {
            return false;
        }

        engine.runtimePivots = new RuntimeBladePivot[engine.blades.Length];

        for (int i = 0; i < engine.blades.Length; i++)
        {
            Transform blade = engine.blades[i];
            if (blade == null)
            {
                return false;
            }

            Transform parent = blade.parent;
            if (parent == null)
            {
                return false;
            }

            Bounds bounds = CalculateWorldBounds(blade);
            Vector3 pivotPosition = bounds.center;
            Quaternion pivotRotation = parent.rotation;

            GameObject pivotObject = new GameObject($"{blade.name}_RuntimeSpinPivot");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(parent, false);
            pivot.position = pivotPosition;
            pivot.rotation = pivotRotation;
            pivot.localScale = Vector3.one;

            if (hideGeneratedPivots)
            {
                pivot.hideFlags = HideFlags.HideInHierarchy;
            }

            blade.SetParent(pivot, true);

            engine.runtimePivots[i] = new RuntimeBladePivot
            {
                visual = blade,
                pivot = pivot,
                baseLocalRotation = pivot.localRotation
            };
        }

        return true;
    }

    private static Bounds CalculateWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.one * 0.01f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
    }
}

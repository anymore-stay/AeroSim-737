using UnityEngine;

/// <summary>
/// Boeing 737 yoke 控制器。
///
/// 作用：
/// 1. 驱动整套 yoke 的俯仰推拉。
/// 2. 驱动左右 yoke 把手同步滚转。
/// 3. 优先读取 JSBSim 实时状态；如果当前没有状态，则回退到本地 FlightInput。
///
/// 结构假设：
/// - assemblyRoot 是整套 yoke 外层父物体。
/// - pitchVisualRoot 是负责整套 yoke 俯仰的公共父节点。
/// - leftWheelVisual / rightWheelVisual 是左右把手/盘的可见节点。
/// </summary>
[DisallowMultipleComponent]
public class B737YokeController : MonoBehaviour
{
    [Header("数据源")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private FlightInput flightInput;
    [SerializeField] private bool preferJsbsimState = true;
    [SerializeField] private bool fallBackToFlightInput = true;

    [Header("平滑")]
    [SerializeField] private float inputSmoothing = 12f;
    [SerializeField] private float returnToCenterSpeed = 2.5f;

    [Header("动作限制（角度）")]
    [Tooltip("前推最大角度。")]
    [SerializeField] private float maxForwardPitchAngle = 6f;
    [Tooltip("后拉最大角度。")]
    [SerializeField] private float maxBackwardPitchAngle = 16f;
    [SerializeField] private float maxRollAngle = 70f;

    [Header("模型层级")]
    [Tooltip("整套 yoke 的最外层总父物体。")]
    [SerializeField] private Transform assemblyRoot;
    [Tooltip("负责整套 yoke 前后俯仰的公共父节点。")]
    [SerializeField] private Transform pitchVisualRoot;
    [Tooltip("左边 yoke 上半部分把手/盘。")]
    [SerializeField] private Transform leftWheelVisual;
    [Tooltip("右边 yoke 上半部分把手/盘。")]
    [SerializeField] private Transform rightWheelVisual;

    [Header("虚拟轴心位置（只用位置，不强依赖旋转）")]
    [SerializeField] private Transform pitchPivotAnchor;
    [SerializeField] private Transform leftWheelPivotAnchor;
    [SerializeField] private Transform rightWheelPivotAnchor;

    [Header("轴向设置（在 pivot 本地空间里）")]
    [SerializeField] private Vector3 pitchAxis = Vector3.right;
    [SerializeField] private Vector3 leftWheelRollAxis = Vector3.up;
    [SerializeField] private Vector3 rightWheelRollAxis = Vector3.up;

    [Header("角度乘数")]
    [SerializeField] private float pitchAngleMultiplier = 1f;
    [SerializeField] private float leftWheelAngleMultiplier = 1f;
    [SerializeField] private float rightWheelAngleMultiplier = 1f;

    [Header("运行时创建虚拟转轴")]
    [SerializeField] private bool useVirtualPitchPivot = false;
    [SerializeField] private bool useVirtualWheelPivots = true;
    [SerializeField] private bool hideGeneratedPivots = false;

    [Header("调试")]
    [SerializeField, Range(-1f, 1f)] private float currentPitchInput;
    [SerializeField, Range(-1f, 1f)] private float currentRollInput;
    [SerializeField] private bool usingJsbsimState;

    private Transform pitchPivotRuntime;
    private Transform leftWheelPivotRuntime;
    private Transform rightWheelPivotRuntime;

    private Quaternion pitchPivotBaseLocalRotation;
    private Quaternion leftWheelPivotBaseLocalRotation;
    private Quaternion rightWheelPivotBaseLocalRotation;

    private bool rigReady;

    private void Awake()
    {
        if (bridge == null) bridge = GetComponent<JsbsimBridge>();
        if (flightInput == null) flightInput = GetComponent<FlightInput>();
    }

    private void Start()
    {
        rigReady = BuildRigIfNeeded();
        if (!rigReady)
        {
            Debug.LogWarning($"{nameof(B737YokeController)} 初始化失败，请检查 Inspector 引用是否完整。", this);
        }
    }

    private void Update()
    {
        if (!rigReady)
        {
            return;
        }

        UpdateInputs();
        ApplyAngles();
    }

    [ContextMenu("Reset To Neutral")]
    public void ResetToNeutral()
    {
        currentPitchInput = 0f;
        currentRollInput = 0f;
        ApplyAngles();
    }

    private void UpdateInputs()
    {
        float targetPitch;
        float targetRoll;
        bool hasDrivingSource = TryReadDrivingInputs(out targetPitch, out targetRoll);

        if (!hasDrivingSource)
        {
            targetPitch = 0f;
            targetRoll = 0f;
        }

        float lerpFactor = inputSmoothing > 0f
            ? 1f - Mathf.Exp(-inputSmoothing * Time.deltaTime)
            : 1f;

        if (hasDrivingSource)
        {
            currentPitchInput = Mathf.Lerp(currentPitchInput, targetPitch, lerpFactor);
            currentRollInput = Mathf.Lerp(currentRollInput, targetRoll, lerpFactor);
        }
        else
        {
            currentPitchInput = Mathf.MoveTowards(currentPitchInput, 0f, returnToCenterSpeed * Time.deltaTime);
            currentRollInput = Mathf.MoveTowards(currentRollInput, 0f, returnToCenterSpeed * Time.deltaTime);
        }

        currentPitchInput = Mathf.Clamp(currentPitchInput, -1f, 1f);
        currentRollInput = Mathf.Clamp(currentRollInput, -1f, 1f);
    }

    private bool TryReadDrivingInputs(out float pitch, out float roll)
    {
        pitch = 0f;
        roll = 0f;
        usingJsbsimState = false;

        if (preferJsbsimState && bridge != null && bridge.HasState)
        {
            float jsbsimPitch;
            float jsbsimRoll;

            bool hasPitch = TryReadFirstAvailableValue(
                out jsbsimPitch,
                "fcs/elevator-cmd-norm",
                "fcs/elevator-pos-norm",
                "elevator_cmd_norm",
                "elevator_norm",
                "stick_pitch_norm");

            bool hasRoll = TryReadFirstAvailableValue(
                out jsbsimRoll,
                "fcs/aileron-cmd-norm",
                "fcs/aileron-pos-norm",
                "aileron_cmd_norm",
                "aileron_norm",
                "stick_roll_norm");

            if (hasPitch || hasRoll)
            {
                pitch = hasPitch ? Mathf.Clamp(jsbsimPitch, -1f, 1f) : 0f;
                roll = hasRoll ? Mathf.Clamp(jsbsimRoll, -1f, 1f) : 0f;
                usingJsbsimState = true;
                return true;
            }
        }

        if (fallBackToFlightInput && flightInput != null)
        {
            pitch = Mathf.Clamp(-flightInput.Elevator, -1f, 1f);
            roll = Mathf.Clamp(-flightInput.Aileron, -1f, 1f);
            return true;
        }

        return false;
    }

    private bool TryReadFirstAvailableValue(out float value, params string[] keys)
    {
        value = 0f;
        if (bridge == null)
        {
            return false;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            if (bridge.TryGetValue(keys[i], out value))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyAngles()
    {
        float pitchAngle = currentPitchInput >= 0f
            ? currentPitchInput * maxBackwardPitchAngle
            : currentPitchInput * maxForwardPitchAngle;

        float rollAngle = currentRollInput * maxRollAngle;

        if (pitchPivotRuntime != null)
        {
            Vector3 axis = NormalizeOrFallback(pitchAxis, Vector3.right);
            pitchPivotRuntime.localRotation =
                pitchPivotBaseLocalRotation *
                Quaternion.AngleAxis(pitchAngle * pitchAngleMultiplier, axis);
        }

        if (leftWheelPivotRuntime != null)
        {
            Vector3 axis = NormalizeOrFallback(leftWheelRollAxis, Vector3.up);
            leftWheelPivotRuntime.localRotation =
                leftWheelPivotBaseLocalRotation *
                Quaternion.AngleAxis(rollAngle * leftWheelAngleMultiplier, axis);
        }

        if (rightWheelPivotRuntime != null)
        {
            Vector3 axis = NormalizeOrFallback(rightWheelRollAxis, Vector3.up);
            rightWheelPivotRuntime.localRotation =
                rightWheelPivotBaseLocalRotation *
                Quaternion.AngleAxis(rollAngle * rightWheelAngleMultiplier, axis);
        }
    }

    private bool BuildRigIfNeeded()
    {
        if (!ValidateReferences())
        {
            return false;
        }

        if (useVirtualPitchPivot)
        {
            Transform pitchParent = pitchVisualRoot.parent;
            pitchPivotRuntime = CreateRuntimePivot(
                "RuntimePitchPivot",
                pitchVisualRoot,
                pitchPivotAnchor,
                pitchParent);
        }
        else
        {
            pitchPivotRuntime = pitchVisualRoot;
        }

        if (useVirtualWheelPivots)
        {
            Transform leftWheelParent = leftWheelVisual.parent;
            leftWheelPivotRuntime = CreateRuntimePivot(
                "RuntimeLeftWheelPivot",
                leftWheelVisual,
                leftWheelPivotAnchor,
                leftWheelParent);

            Transform rightWheelParent = rightWheelVisual.parent;
            rightWheelPivotRuntime = CreateRuntimePivot(
                "RuntimeRightWheelPivot",
                rightWheelVisual,
                rightWheelPivotAnchor,
                rightWheelParent);
        }
        else
        {
            leftWheelPivotRuntime = leftWheelVisual;
            rightWheelPivotRuntime = rightWheelVisual;
        }

        pitchPivotBaseLocalRotation = pitchPivotRuntime.localRotation;
        leftWheelPivotBaseLocalRotation = leftWheelPivotRuntime.localRotation;
        rightWheelPivotBaseLocalRotation = rightWheelPivotRuntime.localRotation;

        return true;
    }

    private bool ValidateReferences()
    {
        if (assemblyRoot == null || pitchVisualRoot == null || leftWheelVisual == null || rightWheelVisual == null)
        {
            return false;
        }

        if (!pitchVisualRoot.IsChildOf(assemblyRoot))
        {
            Debug.LogWarning("pitchVisualRoot 不是 assemblyRoot 的子物体，请再检查层级。", this);
        }

        if (!leftWheelVisual.IsChildOf(pitchVisualRoot) || !rightWheelVisual.IsChildOf(pitchVisualRoot))
        {
            Debug.LogWarning("左右把手/盘最好都是 pitchVisualRoot 的子物体，这样俯仰时会一起跟着动。", this);
        }

        return true;
    }

    private Transform CreateRuntimePivot(string pivotName, Transform visualRoot, Transform pivotReference, Transform desiredParent)
    {
        GameObject pivotObject = new GameObject(pivotName);
        Transform pivot = pivotObject.transform;

        if (desiredParent != null)
        {
            pivot.SetParent(desiredParent, false);
        }

        pivot.position = pivotReference != null ? pivotReference.position : visualRoot.position;
        pivot.rotation = pivotReference != null ? pivotReference.rotation : visualRoot.rotation;
        pivot.localScale = Vector3.one;

        if (hideGeneratedPivots)
        {
            pivot.hideFlags = HideFlags.HideInHierarchy;
        }

        visualRoot.SetParent(pivot, true);
        return pivot;
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
    }

    private void OnDrawGizmosSelected()
    {
        DrawAnchorGizmo(pitchPivotAnchor, new Color(1f, 0.6f, 0.1f));
        DrawAnchorGizmo(leftWheelPivotAnchor, new Color(0.1f, 0.8f, 1f));
        DrawAnchorGizmo(rightWheelPivotAnchor, new Color(0.1f, 1f, 0.4f));
    }

    private void DrawAnchorGizmo(Transform anchor, Color color)
    {
        if (anchor == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawSphere(anchor.position, 0.03f);
        Gizmos.DrawLine(anchor.position, anchor.position + anchor.right * 0.12f);
        Gizmos.DrawLine(anchor.position, anchor.position + anchor.up * 0.12f);
        Gizmos.DrawLine(anchor.position, anchor.position + anchor.forward * 0.12f);
    }
}

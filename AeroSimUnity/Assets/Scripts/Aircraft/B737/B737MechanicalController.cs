using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class B737MechanicalController : MonoBehaviour
{
    public enum LocalAxis
    {
        X,
        Y,
        Z,
        Custom
    }

    [Serializable]
    public class KeyAxis
    {
        public KeyCode negativeKey;
        public KeyCode positiveKey;
    }

    [Serializable]
    public class ControlSurfacePart
    {
        public string label;
        public Transform target;
        [HideInInspector] public string targetPath;
        public LocalAxis rotationAxis = LocalAxis.X;
        public Vector3 customAxis = Vector3.right;
        public float maxAngle = 20f;
        public bool invert;

        [HideInInspector] public Quaternion neutralLocalRotation;
    }

    [Serializable]
    public class LandingGearPart
    {
        public string label;
        public Transform target;
        [HideInInspector] public string targetPath;
        public Vector3 retractedLocalEuler;
        public Vector3 retractedLocalPositionOffset;
        public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [HideInInspector] public Quaternion neutralLocalRotation;
        [HideInInspector] public Vector3 neutralLocalPosition;
    }

    [Serializable]
    public class CockpitYokePart
    {
        public string label;
        public Transform target;
        [HideInInspector] public string targetPath;
        public LocalAxis rollAxis = LocalAxis.Z;
        public Vector3 customRollAxis = Vector3.forward;
        public float maxRollAngle = 65f;
        public bool invertRoll;
        public LocalAxis pitchAxis = LocalAxis.X;
        public Vector3 customPitchAxis = Vector3.right;
        public float maxPitchAngle = 12f;
        public bool invertPitch;
        public Vector3 pitchLocalPositionOffset;

        [HideInInspector] public Quaternion neutralLocalRotation;
        [HideInInspector] public Vector3 neutralLocalPosition;
    }

    [Header("Input")]
    [Tooltip("存在时，机身升降舵直接读取该组件保持的 Elevator 状态。")]
    [SerializeField] private FlightInput flightInput;
    [SerializeField] private bool useKeyboardInput = true;
    [SerializeField] private KeyAxis pitchKeys = new KeyAxis { negativeKey = KeyCode.W, positiveKey = KeyCode.S };
    [SerializeField] private KeyAxis rollKeys = new KeyAxis { negativeKey = KeyCode.A, positiveKey = KeyCode.D };
    [SerializeField] private KeyAxis yawKeys = new KeyAxis { negativeKey = KeyCode.Q, positiveKey = KeyCode.E };
    [SerializeField] private bool useKeyboardGearToggle = true;
    [SerializeField] private KeyCode toggleGearKey = KeyCode.G;
    [SerializeField] private float axisResponseSpeed = 3.5f;
    [SerializeField] private float axisReturnSpeed = 2.5f;
    [SerializeField] private float gearTransitionSeconds = 1.8f;

    [Header("Initial State")]
    [SerializeField] private bool startWithGearExtended = true;

    [Header("Control Surfaces")]
    [SerializeField] private ControlSurfacePart[] ailerons = Array.Empty<ControlSurfacePart>();
    [SerializeField] private ControlSurfacePart[] elevators = Array.Empty<ControlSurfacePart>();
    [SerializeField] private ControlSurfacePart[] rudders = Array.Empty<ControlSurfacePart>();

    [Header("Landing Gear")]
    [SerializeField] private bool useLegacyLandingGearAnimation;
    [SerializeField] private LandingGearPart[] landingGearParts = Array.Empty<LandingGearPart>();

    [Header("Cockpit")]
    [SerializeField] private CockpitYokePart[] cockpitYokes = Array.Empty<CockpitYokePart>();

    [Header("Debug")]
    [SerializeField, Range(-1f, 1f)] private float pitchInput;
    [SerializeField, Range(-1f, 1f)] private float rollInput;
    [SerializeField, Range(-1f, 1f)] private float yawInput;
    [SerializeField, Range(0f, 1f)] private float gearBlend;

    private bool gearExtended;

    public float PitchInput => pitchInput;
    public float RollInput => rollInput;
    public float YawInput => yawInput;
    public bool GearExtended => gearExtended;

    private void OnValidate()
    {
        SyncTargetBindings();
    }

    private void Awake()
    {
        if (flightInput == null)
        {
            flightInput = GetComponent<FlightInput>();
        }

        SyncTargetBindings();
        CaptureNeutralPose();
        gearExtended = startWithGearExtended;
        gearBlend = gearExtended ? 0f : 1f;
        ApplyAll();
    }

    private void LateUpdate()
    {
        if (flightInput == null)
        {
            return;
        }

        // FlightInput 中 W 为正、S 为负；机械模型原有约定与其相反。
        pitchInput = Mathf.Clamp(-flightInput.Elevator, -1f, 1f);
        ApplyControlSurfaceGroup(elevators, pitchInput);
        ApplyCockpitYokes();
    }

    private void Update()
    {
        if (useKeyboardInput)
        {
            UpdateKeyboardInput();
        }

        if (useLegacyLandingGearAnimation &&
            useKeyboardGearToggle &&
            Input.GetKeyDown(toggleGearKey) &&
            LandingGearToggleGate.CanUseToggleInputThisFrame)
        {
            ToggleGear();
        }

        if (useLegacyLandingGearAnimation)
        {
            UpdateGear();
        }

        ApplyAll();
    }

    [ContextMenu("Capture Neutral Pose")]
    public void CaptureNeutralPose()
    {
        SyncTargetBindings();
        CaptureControlSurfacePose(ailerons);
        CaptureControlSurfacePose(elevators);
        CaptureControlSurfacePose(rudders);
        CaptureLandingGearPose();
        CaptureYokePose();
    }

    public void SetExternalInputs(float pitch, float roll, float yaw)
    {
        pitchInput = Mathf.Clamp(pitch, -1f, 1f);
        rollInput = Mathf.Clamp(roll, -1f, 1f);
        yawInput = Mathf.Clamp(yaw, -1f, 1f);
    }

    public void ToggleGear()
    {
        gearExtended = !gearExtended;
    }

    public void SetGearExtended(bool extended, bool snapImmediately = false)
    {
        gearExtended = extended;
        if (useLegacyLandingGearAnimation && snapImmediately)
        {
            gearBlend = gearExtended ? 0f : 1f;
            ApplyAll();
        }
    }

    private void UpdateKeyboardInput()
    {
        pitchInput = UpdateAxisValue(pitchInput, pitchKeys);
        rollInput = UpdateAxisValue(rollInput, rollKeys);
        yawInput = UpdateAxisValue(yawInput, yawKeys);
    }

    private float UpdateAxisValue(float currentValue, KeyAxis keys)
    {
        float targetValue = 0f;
        bool negativePressed = Input.GetKey(keys.negativeKey);
        bool positivePressed = Input.GetKey(keys.positiveKey);

        if (negativePressed && !positivePressed)
        {
            targetValue = -1f;
        }
        else if (positivePressed && !negativePressed)
        {
            targetValue = 1f;
        }

        float speed = Mathf.Approximately(targetValue, 0f) ? axisReturnSpeed : axisResponseSpeed;
        return Mathf.MoveTowards(currentValue, targetValue, speed * Time.deltaTime);
    }

    private void UpdateGear()
    {
        float targetBlend = gearExtended ? 0f : 1f;
        float seconds = Mathf.Max(0.01f, gearTransitionSeconds);
        float speed = Time.deltaTime / seconds;
        gearBlend = Mathf.MoveTowards(gearBlend, targetBlend, speed);
    }

    private void ApplyAll()
    {
        ApplyControlSurfaceGroup(ailerons, rollInput);
        ApplyControlSurfaceGroup(elevators, pitchInput);
        ApplyControlSurfaceGroup(rudders, yawInput);
        if (useLegacyLandingGearAnimation)
        {
            ApplyLandingGear();
        }

        ApplyCockpitYokes();
    }

    [ContextMenu("Rebind Targets From Saved Paths")]
    private void RebindTargetsFromSavedPaths()
    {
        SyncTargetBindings();
    }

    private void SyncTargetBindings()
    {
        SyncControlSurfaceTargets(ailerons);
        SyncControlSurfaceTargets(elevators);
        SyncControlSurfaceTargets(rudders);
        SyncLandingGearTargets();
        SyncCockpitYokeTargets();
    }

    private void SyncControlSurfaceTargets(ControlSurfacePart[] parts)
    {
        if (parts == null)
        {
            return;
        }

        foreach (ControlSurfacePart part in parts)
        {
            if (part == null)
            {
                continue;
            }

            SyncTargetReference(ref part.target, ref part.targetPath);
        }
    }

    private void SyncLandingGearTargets()
    {
        if (landingGearParts == null)
        {
            return;
        }

        foreach (LandingGearPart part in landingGearParts)
        {
            if (part == null)
            {
                continue;
            }

            SyncTargetReference(ref part.target, ref part.targetPath);
        }
    }

    private void SyncCockpitYokeTargets()
    {
        if (cockpitYokes == null)
        {
            return;
        }

        foreach (CockpitYokePart part in cockpitYokes)
        {
            if (part == null)
            {
                continue;
            }

            SyncTargetReference(ref part.target, ref part.targetPath);
        }
    }

    private void SyncTargetReference(ref Transform target, ref string targetPath)
    {
        if (target != null)
        {
            string relativePath = GetRelativePath(target);
            if (!string.IsNullOrEmpty(relativePath))
            {
                targetPath = relativePath;
            }

            return;
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            return;
        }

        Transform resolvedTarget = FindRelativeTarget(targetPath);
        if (resolvedTarget == null)
        {
            resolvedTarget = FindDescendantByName(transform, GetLeafName(targetPath));
        }

        if (resolvedTarget == null)
        {
            return;
        }

        target = resolvedTarget;
        targetPath = GetRelativePath(resolvedTarget);
    }

    private void CaptureControlSurfacePose(ControlSurfacePart[] parts)
    {
        if (parts == null)
        {
            return;
        }

        foreach (ControlSurfacePart part in parts)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            part.neutralLocalRotation = part.target.localRotation;
        }
    }

    private void CaptureLandingGearPose()
    {
        if (landingGearParts == null)
        {
            return;
        }

        foreach (LandingGearPart part in landingGearParts)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            part.neutralLocalRotation = part.target.localRotation;
            part.neutralLocalPosition = part.target.localPosition;
        }
    }

    private void CaptureYokePose()
    {
        if (cockpitYokes == null)
        {
            return;
        }

        foreach (CockpitYokePart part in cockpitYokes)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            part.neutralLocalRotation = part.target.localRotation;
            part.neutralLocalPosition = part.target.localPosition;
        }
    }

    private void ApplyControlSurfaceGroup(ControlSurfacePart[] parts, float axisValue)
    {
        if (parts == null)
        {
            return;
        }

        foreach (ControlSurfacePart part in parts)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            float signedAngle = axisValue * part.maxAngle * (part.invert ? -1f : 1f);
            Quaternion delta = Quaternion.AngleAxis(signedAngle, AxisToVector(part.rotationAxis, part.customAxis));
            part.target.localRotation = part.neutralLocalRotation * delta;
        }
    }

    private void ApplyLandingGear()
    {
        if (landingGearParts == null)
        {
            return;
        }

        foreach (LandingGearPart part in landingGearParts)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            float easedBlend = part.motionCurve == null ? gearBlend : part.motionCurve.Evaluate(gearBlend);
            Quaternion delta = Quaternion.Euler(part.retractedLocalEuler * easedBlend);
            part.target.localRotation = part.neutralLocalRotation * delta;
            part.target.localPosition = part.neutralLocalPosition + part.retractedLocalPositionOffset * easedBlend;
        }
    }

    private void ApplyCockpitYokes()
    {
        if (cockpitYokes == null)
        {
            return;
        }

        foreach (CockpitYokePart part in cockpitYokes)
        {
            if (part == null || part.target == null)
            {
                continue;
            }

            float rollAngle = rollInput * part.maxRollAngle * (part.invertRoll ? -1f : 1f);
            float pitchAngle = pitchInput * part.maxPitchAngle * (part.invertPitch ? -1f : 1f);

            Quaternion rollDelta = Quaternion.AngleAxis(rollAngle, AxisToVector(part.rollAxis, part.customRollAxis));
            Quaternion pitchDelta = Quaternion.AngleAxis(pitchAngle, AxisToVector(part.pitchAxis, part.customPitchAxis));

            part.target.localRotation = part.neutralLocalRotation * rollDelta * pitchDelta;
            part.target.localPosition = part.neutralLocalPosition + part.pitchLocalPositionOffset * pitchInput;
        }
    }

    private static Vector3 AxisToVector(LocalAxis axis, Vector3 customAxis)
    {
        switch (axis)
        {
            case LocalAxis.X:
                return Vector3.right;
            case LocalAxis.Y:
                return Vector3.up;
            case LocalAxis.Z:
                return Vector3.forward;
            case LocalAxis.Custom:
                return NormalizeOrFallback(customAxis, Vector3.right);
            default:
                return Vector3.forward;
        }
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
    }

    private string GetRelativePath(Transform target)
    {
        if (target == null || target == transform)
        {
            return string.Empty;
        }

        Stack<string> segments = new Stack<string>();
        Transform current = target;

        while (current != null && current != transform)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        if (current != transform || segments.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("/", segments.ToArray());
    }

    private Transform FindRelativeTarget(string targetPath)
    {
        if (string.IsNullOrEmpty(targetPath))
        {
            return null;
        }

        return transform.Find(targetPath);
    }

    private static string GetLeafName(string targetPath)
    {
        if (string.IsNullOrEmpty(targetPath))
        {
            return string.Empty;
        }

        int lastSlashIndex = targetPath.LastIndexOf('/');
        return lastSlashIndex >= 0 ? targetPath.Substring(lastSlashIndex + 1) : targetPath;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == targetName)
            {
                return child;
            }

            Transform nestedMatch = FindDescendantByName(child, targetName);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }
}

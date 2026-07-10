using UnityEngine;

/// <summary>
/// Retracts a landing gear assembly by rotating it around an external hinge pivot and axis.
/// Use this when the wheel assembly should fold around a strut/hinge endpoint instead of
/// rotating around its own transform origin.
/// </summary>
public class LandingGearHingeRetractionController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform rotatingRoot;
    [SerializeField] private Transform hingePivot;

    [Header("Input")]
    [SerializeField] private bool useKeyboard = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.G;

    [Header("Hinge Motion")]
    [SerializeField] private Vector3 hingeLocalAxis = Vector3.right;
    [SerializeField] private float retractedAngle = -90f;
    [SerializeField] private float transitionDuration = 3f;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool startExtended = true;
    [SerializeField] private bool currentScenePoseMatchesStartState = true;
    [SerializeField] private bool lockHingeAtStart = true;
    [SerializeField] private bool applyStartPoseOnAwake = false;

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float gearAmount = 1f;
    [SerializeField] private bool previewInEditMode;

    private Vector3 extendedLocalPosition;
    private Quaternion extendedLocalRotation;
    private Vector3 retractedLocalPosition;
    private Quaternion retractedLocalRotation;
    private Vector3 fixedPivotLocalPosition;
    private Vector3 fixedParentLocalAxis;
    private float targetAmount;
    private bool captured;
    private bool gateMotionActive;

    public float GearAmount => gearAmount;
    public bool IsFullyExtended => gearAmount >= 0.99f;

    private void Awake()
    {
        if (GetComponentInParent<LandingGearDoorSequenceController>() != null ||
            GetComponentInParent<LandingGearSynchronizedDoorGearController>() != null)
        {
            useKeyboard = false;
        }

        CaptureExtendedPose();
        gearAmount = startExtended ? 1f : 0f;
        targetAmount = gearAmount;

        if (applyStartPoseOnAwake)
        {
            ApplyPose();
        }
    }

    private void Update()
    {
        if (useKeyboard && Input.GetKeyDown(toggleKey))
        {
            bool currentlyExtended = targetAmount > 0.5f;
            if (LandingGearToggleGate.TryGetToggleTarget(this, currentlyExtended, out bool extendGear) &&
                extendGear != currentlyExtended &&
                TryBeginGlobalMotion())
            {
                if (extendGear)
                {
                    Extend();
                }
                else
                {
                    Retract();
                }
            }
        }

        if (!Mathf.Approximately(gearAmount, targetAmount))
        {
            gearAmount = Mathf.MoveTowards(gearAmount, targetAmount, Time.deltaTime / Mathf.Max(0.01f, transitionDuration));
            ApplyPose();
        }
        else
        {
            EndGlobalMotion();
        }
    }

    private void OnValidate()
    {
        if (transitionDuration < 0.01f)
        {
            transitionDuration = 0.01f;
        }

        if (!Application.isPlaying && previewInEditMode)
        {
            CaptureExtendedPose();
            targetAmount = gearAmount;
            ApplyPose();
        }
    }

    [ContextMenu("Capture Current As Extended")]
    private void CaptureExtendedPose()
    {
        if (rotatingRoot == null || hingePivot == null)
        {
            captured = false;
            return;
        }

        Transform motionParent = rotatingRoot.parent;
        if (motionParent == null)
        {
            captured = false;
            return;
        }

        extendedLocalPosition = rotatingRoot.localPosition;
        extendedLocalRotation = rotatingRoot.localRotation;
        fixedPivotLocalPosition = motionParent.InverseTransformPoint(hingePivot.position);
        fixedParentLocalAxis = ResolveParentLocalAxis(motionParent);

        if (currentScenePoseMatchesStartState && !startExtended)
        {
            retractedLocalPosition = rotatingRoot.localPosition;
            retractedLocalRotation = rotatingRoot.localRotation;

            Quaternion fullRetractedRotation = Quaternion.AngleAxis(retractedAngle, fixedParentLocalAxis);
            Quaternion inverseRetractedRotation = Quaternion.Inverse(fullRetractedRotation);

            extendedLocalPosition = fixedPivotLocalPosition + inverseRetractedRotation * (rotatingRoot.localPosition - fixedPivotLocalPosition);
            extendedLocalRotation = inverseRetractedRotation * rotatingRoot.localRotation;
        }
        else
        {
            Quaternion fullRetractedRotation = Quaternion.AngleAxis(retractedAngle, fixedParentLocalAxis);
            retractedLocalPosition = fixedPivotLocalPosition + fullRetractedRotation * (extendedLocalPosition - fixedPivotLocalPosition);
            retractedLocalRotation = fullRetractedRotation * extendedLocalRotation;
        }

        captured = true;
    }

    [ContextMenu("Test Toggle")]
    private void TestToggle()
    {
        Toggle();
    }

    [ContextMenu("Test Retract")]
    private void TestRetract()
    {
        Retract();
    }

    [ContextMenu("Test Extend")]
    private void TestExtend()
    {
        Extend();
    }

    public void Toggle()
    {
        if (targetAmount > 0.5f)
        {
            Retract();
        }
        else
        {
            Extend();
        }
    }

    public void Retract()
    {
        targetAmount = 0f;
    }

    public void Extend()
    {
        targetAmount = 1f;
    }

    public void SetGearAmount(float amount)
    {
        gearAmount = Mathf.Clamp01(amount);
        targetAmount = gearAmount;
        ApplyPose();
    }

    private void ApplyPose()
    {
        if (rotatingRoot == null || hingePivot == null)
        {
            return;
        }

        Transform motionParent = rotatingRoot.parent;
        if (motionParent == null)
        {
            return;
        }

        if (!captured)
        {
            CaptureExtendedPose();
        }

        float retractProgress = 1f - gearAmount;
        float curvedProgress = motionCurve != null ? motionCurve.Evaluate(retractProgress) : retractProgress;

        if (lockHingeAtStart)
        {
            rotatingRoot.localPosition = Vector3.Lerp(extendedLocalPosition, retractedLocalPosition, curvedProgress);
            rotatingRoot.localRotation = Quaternion.Slerp(extendedLocalRotation, retractedLocalRotation, curvedProgress);
            return;
        }

        Vector3 parentLocalAxis = ResolveParentLocalAxis(motionParent);
        Vector3 pivotLocalPosition = motionParent.InverseTransformPoint(hingePivot.position);
        float angle = retractedAngle * curvedProgress;
        Quaternion hingeRotation = Quaternion.AngleAxis(angle, parentLocalAxis);

        rotatingRoot.localPosition = pivotLocalPosition + hingeRotation * (extendedLocalPosition - pivotLocalPosition);
        rotatingRoot.localRotation = hingeRotation * extendedLocalRotation;
    }

    private Vector3 ResolveParentLocalAxis(Transform motionParent)
    {
        Vector3 axis = hingeLocalAxis.sqrMagnitude > 0.001f ? hingeLocalAxis.normalized : Vector3.right;
        if (hingePivot == null || motionParent == null)
        {
            return axis;
        }

        return motionParent.InverseTransformDirection(hingePivot.TransformDirection(axis)).normalized;
    }

    private bool TryBeginGlobalMotion()
    {
        if (gateMotionActive)
        {
            return true;
        }

        if (!LandingGearToggleGate.TryBeginMotion(this))
        {
            return false;
        }

        gateMotionActive = true;
        return true;
    }

    private void EndGlobalMotion()
    {
        if (!gateMotionActive)
        {
            return;
        }

        LandingGearToggleGate.EndMotion(this);
        gateMotionActive = false;
    }

    private void OnDisable()
    {
        EndGlobalMotion();
    }
}

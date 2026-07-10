using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sequences landing gear bay doors and hinge-driven gear movement.
/// Press the toggle key to open both doors first, then move the landing gear.
/// </summary>
public class LandingGearDoorSequenceController : MonoBehaviour
{
    private enum SequenceState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    [Header("Door Targets")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Door Hinge Settings")]
    [SerializeField] private Transform leftDoorHingePivot;
    [SerializeField] private Transform rightDoorHingePivot;
    [SerializeField] private Vector3 leftDoorLocalAxis = Vector3.right;
    [SerializeField] private Vector3 rightDoorLocalAxis = Vector3.right;
    [SerializeField] private float leftDoorOpenAngle = -85f;
    [SerializeField] private float rightDoorOpenAngle = 85f;
    [SerializeField] private bool lockDoorHingesAtStart = true;
    [SerializeField] private bool rotateAroundDoorHingePivots = true;

    [Header("Gear Targets")]
    [SerializeField] private List<LandingGearHingeRetractionController> gearHinges =
        new List<LandingGearHingeRetractionController>();
    [SerializeField] private bool autoBindChildGearHinges = true;

    [Header("Input")]
    [SerializeField] private bool useKeyboard = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.G;

    [Header("Timing")]
    [SerializeField] private bool startGearExtended = true;
    [SerializeField] private bool applyInitialGearPose = false;
    [SerializeField] private float doorOpenDuration = 1f;
    [SerializeField] private float gearMoveDuration = 3f;
    [SerializeField] private float doorCloseDuration = 1f;
    [SerializeField] private bool closeDoorsAfterGearMoves;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float doorAmount;
    [SerializeField, Range(0f, 1f)] private float gearAmount = 1f;
    [SerializeField] private SequenceState state;
    [SerializeField] private bool logDoorBindingsOnFirstToggle = true;

    private Quaternion leftDoorClosedRotation;
    private Quaternion rightDoorClosedRotation;
    private Vector3 leftDoorClosedPosition;
    private Vector3 rightDoorClosedPosition;
    private Vector3 leftDoorClosedLocalPosition;
    private Vector3 rightDoorClosedLocalPosition;
    private Vector3 leftDoorFixedPivotPosition;
    private Vector3 rightDoorFixedPivotPosition;
    private Vector3 leftDoorFixedParentLocalAxis;
    private Vector3 rightDoorFixedParentLocalAxis;
    private Transform leftDoorRuntimePivot;
    private Transform rightDoorRuntimePivot;
    private Quaternion leftDoorRuntimePivotClosedRotation;
    private Quaternion rightDoorRuntimePivotClosedRotation;
    private Coroutine sequenceRoutine;
    private bool gateMotionActive;
    private bool gearExtended;
    private bool loggedDoorBindings;

    private void Awake()
    {
        AutoBindChildGearHinges();
        PrepareRuntimeDoorPivots();
        CaptureClosedDoorRotations();
        gearExtended = startGearExtended;
        gearAmount = gearExtended ? 1f : 0f;
        doorAmount = gearExtended ? 1f : 0f;
        state = gearExtended ? SequenceState.Open : SequenceState.Closed;
    }

    private void Start()
    {
        ApplyDoorAmount(doorAmount);

        if (applyInitialGearPose)
        {
            ApplyGearAmount(gearAmount);
        }
    }

    private void Update()
    {
        if (useKeyboard && Input.GetKeyDown(toggleKey))
        {
            ToggleGearSequence();
        }
    }

    private void OnValidate()
    {
        doorOpenDuration = Mathf.Max(0.01f, doorOpenDuration);
        gearMoveDuration = Mathf.Max(0.01f, gearMoveDuration);
        doorCloseDuration = Mathf.Max(0.01f, doorCloseDuration);

        if (!Application.isPlaying)
        {
            AutoBindChildGearHinges();
        }
    }

    [ContextMenu("Capture Current Doors As Closed")]
    private void CaptureClosedDoorRotations()
    {
        if (leftDoor != null)
        {
            leftDoorClosedPosition = leftDoor.position;
            leftDoorClosedLocalPosition = leftDoor.localPosition;
            leftDoorClosedRotation = leftDoor.localRotation;
            leftDoorFixedPivotPosition = ResolveDoorParentSpacePivot(leftDoorHingePivot, leftDoor);
            leftDoorFixedParentLocalAxis = ResolveDoorParentLocalAxis(leftDoorHingePivot, leftDoor, leftDoorLocalAxis);
        }

        if (rightDoor != null)
        {
            rightDoorClosedPosition = rightDoor.position;
            rightDoorClosedLocalPosition = rightDoor.localPosition;
            rightDoorClosedRotation = rightDoor.localRotation;
            rightDoorFixedPivotPosition = ResolveDoorParentSpacePivot(rightDoorHingePivot, rightDoor);
            rightDoorFixedParentLocalAxis = ResolveDoorParentLocalAxis(rightDoorHingePivot, rightDoor, rightDoorLocalAxis);
        }
    }

    [ContextMenu("Test Toggle Sequence")]
    private void TestToggleSequence()
    {
        ToggleGearSequence();
    }

    [ContextMenu("Test Extend Sequence")]
    private void TestExtendSequence()
    {
        StartSequence(true);
    }

    [ContextMenu("Test Retract Sequence")]
    private void TestRetractSequence()
    {
        StartSequence(false);
    }

    public void ToggleGearSequence()
    {
        LogDoorBindingsOnce();

        if (sequenceRoutine != null)
        {
            return;
        }

        bool currentlyExtended = state == SequenceState.Open;
        if (!LandingGearToggleGate.TryGetToggleTarget(this, currentlyExtended, out bool extendGear))
        {
            return;
        }

        if (extendGear != currentlyExtended)
        {
            StartSequence(extendGear);
        }
    }

    public void StartSequence(bool extendGear)
    {
        if (sequenceRoutine != null)
        {
            return;
        }

        if (!TryBeginGlobalMotion())
        {
            return;
        }

        sequenceRoutine = StartCoroutine(RunSequence(extendGear));
    }

    private IEnumerator RunSequence(bool extendGear)
    {
        if (extendGear)
        {
            state = SequenceState.Opening;
            yield return AnimateDoors(doorAmount, 1f, doorOpenDuration);
            yield return AnimateGear(gearAmount, 1f, gearMoveDuration);
            gearExtended = true;

            if (closeDoorsAfterGearMoves)
            {
                yield return AnimateDoors(doorAmount, 0f, doorCloseDuration);
            }

            state = SequenceState.Open;
        }
        else
        {
            state = SequenceState.Closing;
            yield return AnimateGear(gearAmount, 0f, gearMoveDuration);
            gearExtended = false;
            yield return AnimateDoors(doorAmount, 0f, doorCloseDuration);
            state = SequenceState.Closed;
        }

        sequenceRoutine = null;
        EndGlobalMotion();
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

    private IEnumerator AnimateDoors(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = motionCurve != null ? motionCurve.Evaluate(t) : t;
            doorAmount = Mathf.Lerp(from, to, curvedT);
            ApplyDoorAmount(doorAmount);
            yield return null;
        }

        doorAmount = to;
        ApplyDoorAmount(doorAmount);
    }

    private IEnumerator AnimateGear(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = motionCurve != null ? motionCurve.Evaluate(t) : t;
            gearAmount = Mathf.Lerp(from, to, curvedT);
            ApplyGearAmount(gearAmount);
            yield return null;
        }

        gearAmount = to;
        ApplyGearAmount(gearAmount);
    }

    private void ApplyDoorAmount(float amount)
    {
        if (leftDoor != null)
        {
            ApplyDoorPose(
                leftDoor,
                leftDoorHingePivot,
                leftDoorRuntimePivot,
                leftDoorRuntimePivotClosedRotation,
                leftDoorClosedPosition,
                leftDoorClosedLocalPosition,
                leftDoorClosedRotation,
                leftDoorFixedPivotPosition,
                leftDoorFixedParentLocalAxis,
                leftDoorLocalAxis,
                leftDoorOpenAngle,
                amount);
        }

        if (rightDoor != null)
        {
            ApplyDoorPose(
                rightDoor,
                rightDoorHingePivot,
                rightDoorRuntimePivot,
                rightDoorRuntimePivotClosedRotation,
                rightDoorClosedPosition,
                rightDoorClosedLocalPosition,
                rightDoorClosedRotation,
                rightDoorFixedPivotPosition,
                rightDoorFixedParentLocalAxis,
                rightDoorLocalAxis,
                rightDoorOpenAngle,
                amount);
        }
    }

    private void ApplyDoorPose(
        Transform door,
        Transform hingePivot,
        Transform runtimePivot,
        Quaternion runtimePivotClosedRotation,
        Vector3 closedPosition,
        Vector3 closedLocalPosition,
        Quaternion closedLocalRotation,
        Vector3 fixedPivotParentPosition,
        Vector3 fixedParentLocalAxis,
        Vector3 localAxis,
        float openAngle,
        float amount)
    {
        float angle = openAngle * amount;

        if (runtimePivot != null)
        {
            door.localPosition = closedLocalPosition;
            door.localRotation = closedLocalRotation;
            runtimePivot.localRotation = runtimePivotClosedRotation * Quaternion.AngleAxis(angle, ResolveAxis(localAxis));
            return;
        }

        if (hingePivot == null)
        {
            door.localRotation = closedLocalRotation * Quaternion.AngleAxis(angle, ResolveAxis(localAxis));
            return;
        }

        Transform motionParent = door.parent;
        if (motionParent == null)
        {
            door.localRotation = closedLocalRotation * Quaternion.AngleAxis(angle, ResolveAxis(localAxis));
            return;
        }

        if (!rotateAroundDoorHingePivots)
        {
            door.localPosition = closedLocalPosition;
            door.localRotation = closedLocalRotation * Quaternion.AngleAxis(angle, ResolveAxis(localAxis));
            return;
        }

        Vector3 closedParentPosition = motionParent.InverseTransformPoint(closedPosition);
        Vector3 pivotParentPosition = lockDoorHingesAtStart
            ? fixedPivotParentPosition
            : ResolveDoorParentSpacePivot(hingePivot, door);
        Vector3 parentLocalAxis = lockDoorHingesAtStart
            ? fixedParentLocalAxis
            : ResolveDoorParentLocalAxis(hingePivot, door, localAxis);
        Quaternion hingeRotation = Quaternion.AngleAxis(angle, parentLocalAxis);

        door.localPosition = pivotParentPosition + hingeRotation * (closedParentPosition - pivotParentPosition);
        door.localRotation = hingeRotation * closedLocalRotation;
    }

    private void ApplyGearAmount(float amount)
    {
        for (int i = 0; i < gearHinges.Count; i++)
        {
            if (gearHinges[i] != null)
            {
                gearHinges[i].SetGearAmount(amount);
            }
        }
    }

    private void LogDoorBindingsOnce()
    {
        if (!logDoorBindingsOnFirstToggle || loggedDoorBindings)
        {
            return;
        }

        loggedDoorBindings = true;
        string gearHingeList = string.Join(", ", GearHingeNames());
        Debug.Log(
            $"[{nameof(LandingGearDoorSequenceController)}] {name} leftDoor={NameOf(leftDoor)}, rightDoor={NameOf(rightDoor)}, " +
            $"leftPivot={NameOf(leftDoorHingePivot)}, rightPivot={NameOf(rightDoorHingePivot)}, " +
            $"leftAxis={leftDoorLocalAxis}, rightAxis={rightDoorLocalAxis}, " +
            $"leftAngle={leftDoorOpenAngle}, rightAngle={rightDoorOpenAngle}, rotateAroundPivots={rotateAroundDoorHingePivots}, " +
            $"gearHinges={gearHingeList}");
    }

    private static string NameOf(Transform target)
    {
        return target != null ? target.name : "<none>";
    }

    private void AutoBindChildGearHinges()
    {
        if (!autoBindChildGearHinges)
        {
            return;
        }

        gearHinges.RemoveAll(hinge => hinge == null || !hinge.transform.IsChildOf(transform));

        LandingGearHingeRetractionController[] childHinges =
            GetComponentsInChildren<LandingGearHingeRetractionController>(true);

        foreach (LandingGearHingeRetractionController childHinge in childHinges)
        {
            if (childHinge != null && !gearHinges.Contains(childHinge))
            {
                gearHinges.Add(childHinge);
            }
        }
    }

    private IEnumerable<string> GearHingeNames()
    {
        foreach (LandingGearHingeRetractionController hinge in gearHinges)
        {
            yield return hinge != null ? hinge.name : "<none>";
        }
    }

    private static Vector3 ResolveAxis(Vector3 axis)
    {
        return axis.sqrMagnitude > 0.001f ? axis.normalized : Vector3.right;
    }

    private void PrepareRuntimeDoorPivots()
    {
        leftDoorRuntimePivot = CreateRuntimeDoorPivot(
            leftDoor,
            leftDoorHingePivot,
            "Left Door Runtime Pivot",
            out leftDoorRuntimePivotClosedRotation);

        rightDoorRuntimePivot = CreateRuntimeDoorPivot(
            rightDoor,
            rightDoorHingePivot,
            "Right Door Runtime Pivot",
            out rightDoorRuntimePivotClosedRotation);
    }

    private Transform CreateRuntimeDoorPivot(
        Transform door,
        Transform hingePivot,
        string pivotName,
        out Quaternion closedLocalRotation)
    {
        closedLocalRotation = Quaternion.identity;

        if (!Application.isPlaying || !rotateAroundDoorHingePivots || door == null || hingePivot == null || door.parent == null)
        {
            return null;
        }

        Transform originalParent = door.parent;
        GameObject pivotObject = new GameObject($"{door.name} {pivotName}");
        Transform runtimePivot = pivotObject.transform;
        runtimePivot.SetParent(originalParent, false);
        runtimePivot.SetPositionAndRotation(hingePivot.position, hingePivot.rotation);
        runtimePivot.localScale = Vector3.one;
        closedLocalRotation = runtimePivot.localRotation;

        door.SetParent(runtimePivot, true);
        return runtimePivot;
    }

    private static Vector3 ResolveDoorParentSpacePivot(Transform hingePivot, Transform door)
    {
        if (door == null || door.parent == null)
        {
            return Vector3.zero;
        }

        Vector3 pivotWorldPosition = hingePivot != null ? hingePivot.position : door.position;
        return door.parent.InverseTransformPoint(pivotWorldPosition);
    }

    private static Vector3 ResolveDoorParentLocalAxis(Transform hingePivot, Transform door, Vector3 localAxis)
    {
        Vector3 axis = ResolveAxis(localAxis);

        if (door == null || door.parent == null)
        {
            return axis;
        }

        Vector3 worldAxis = hingePivot != null ? hingePivot.TransformDirection(axis) : door.TransformDirection(axis);
        return door.parent.InverseTransformDirection(worldAxis).normalized;
    }
}

using UnityEngine;

/// <summary>
/// Rotates the nose wheel assembly around its own vertical steering axis.
/// This is independent from the hinge retraction motion and only becomes active
/// when the linked landing gear is fully extended.
/// </summary>
public class NoseGearSteeringController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform steeringRoot;
    [SerializeField] private LandingGearHingeRetractionController gearRetraction;

    [Header("Input")]
    [SerializeField] private bool useKeyboard = true;
    [SerializeField] private KeyCode steerLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode steerRightKey = KeyCode.E;
    [SerializeField] private bool invertInput;

    [Header("Steering")]
    [SerializeField] private Vector3 localSteeringAxis = Vector3.forward;
    [SerializeField] private float maxSteeringAngle = 35f;
    [SerializeField] private float steeringSpeed = 90f;
    [SerializeField] private float returnSpeed = 120f;
    [SerializeField] private bool requireGearFullyExtended = true;
    [SerializeField] private bool requireNearGround = true;
    [SerializeField] private float maxGroundAglFt = 15f;

    [Header("Debug")]
    [SerializeField] private float steeringAngle;

    private Quaternion neutralLocalRotation;

    private void Awake()
    {
        if (steeringRoot == null)
        {
            steeringRoot = transform;
        }

        neutralLocalRotation = steeringRoot.localRotation;
    }

    private void Update()
    {
        float targetAngle = 0f;

        if (CanSteer())
        {
            float input = ReadInput();
            targetAngle = input * maxSteeringAngle;
        }

        float speed = Mathf.Abs(targetAngle) > 0.01f ? steeringSpeed : returnSpeed;
        steeringAngle = Mathf.MoveTowards(steeringAngle, targetAngle, speed * Time.deltaTime);
        ApplySteering();
    }

    private float ReadInput()
    {
        if (!useKeyboard)
        {
            return 0f;
        }

        float input = 0f;
        if (Input.GetKey(steerLeftKey)) input -= 1f;
        if (Input.GetKey(steerRightKey)) input += 1f;
        return invertInput ? -input : input;
    }

    private bool CanSteer()
    {
        if (!requireGearFullyExtended)
        {
            return IsNearGround();
        }

        bool gearReady = gearRetraction == null || gearRetraction.IsFullyExtended;
        return gearReady && IsNearGround();
    }

    private bool IsNearGround()
    {
        if (!requireNearGround)
        {
            return true;
        }

        JsbsimBridge bridge = JsbsimBridge.Instance;
        if (bridge == null || !bridge.HasState)
        {
            return true;
        }

        return bridge.AglFt <= maxGroundAglFt;
    }

    private void ApplySteering()
    {
        if (steeringRoot == null)
        {
            return;
        }

        Vector3 axis = localSteeringAxis.sqrMagnitude > 0.001f ? localSteeringAxis.normalized : Vector3.up;
        steeringRoot.localRotation = neutralLocalRotation * Quaternion.AngleAxis(steeringAngle, axis);
    }
}

using UnityEngine;

/// <summary>
/// Moves Rigidbody-managed objects through the physics position when the
/// floating origin shifts.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FloatingOriginRigidbody : MonoBehaviour
{
    [SerializeField] private bool syncTransformsAfterShift;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        FloatingOriginManager.OriginShifted += HandleOriginShift;
    }

    private void OnDisable()
    {
        FloatingOriginManager.OriginShifted -= HandleOriginShift;
    }

    private void HandleOriginShift(Vector3 offset)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.position += offset;

        if (syncTransformsAfterShift)
            Physics.SyncTransforms();
    }
}

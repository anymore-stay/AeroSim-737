using System;
using UnityEngine;

/// <summary>
/// Keeps the active simulation target close to Unity's origin by broadcasting
/// a shared world offset when the target gets too far away.
/// </summary>
[DefaultExecutionOrder(10000)]
public class FloatingOriginManager : MonoBehaviour
{
    public static FloatingOriginManager Instance { get; private set; }
    public static event Action<Vector3> OriginShifted;

    [SerializeField] private Transform target;
    [SerializeField] private float thresholdMeters = 500f;
    [SerializeField] private Vector3 centerPosition = Vector3.zero;
    [SerializeField] private bool includeVerticalAxis = true;
    [SerializeField] private bool logShifts;

    public Transform Target => target;
    public float ThresholdMeters => thresholdMeters;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[FloatingOrigin] Scene has more than one FloatingOriginManager. Instance keeps the first manager.");
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (target == null || thresholdMeters <= 0f) return;

        Vector3 delta = target.position - centerPosition;
        if (!includeVerticalAxis) delta.y = 0f;

        if (delta.sqrMagnitude < thresholdMeters * thresholdMeters) return;

        Vector3 offset = -delta;
        RequestShift(offset);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void RequestShift(Vector3 offset)
    {
        if (offset == Vector3.zero) return;

        if (logShifts)
            Debug.Log("[FloatingOrigin] Shift origin by " + offset.ToString("F2"));

        OriginShifted?.Invoke(offset);
    }
}

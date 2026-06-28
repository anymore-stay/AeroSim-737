using UnityEngine;

/// <summary>
/// Fixes TrailRenderer world-space history after an origin shift.
/// Preserving points keeps the trail continuous; clearing avoids long stretched
/// lines for effects where old trail history is not important.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class FloatingOriginTrailRenderer : MonoBehaviour
{
    [SerializeField] private bool preserveTrail = true;

    private TrailRenderer trail;
    private Vector3[] points;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
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
        if (trail == null) trail = GetComponent<TrailRenderer>();

        if (!preserveTrail)
        {
            trail.Clear();
            return;
        }

        int count = trail.positionCount;
        if (count == 0) return;

        if (points == null || points.Length != count)
            points = new Vector3[count];

        int read = trail.GetPositions(points);
        for (int i = 0; i < read; i++)
            points[i] += offset;

        trail.SetPositions(points);
    }
}

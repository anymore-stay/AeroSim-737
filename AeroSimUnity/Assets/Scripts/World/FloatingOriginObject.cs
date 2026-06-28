using UnityEngine;

/// <summary>
/// Moves a regular Transform when the floating origin shifts.
/// Put this on terrain, airport roots, skybox anchors, clouds, and other
/// non-physics world objects that must keep their relative position to the aircraft.
/// </summary>
public class FloatingOriginObject : MonoBehaviour
{
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
        transform.position += offset;
    }
}

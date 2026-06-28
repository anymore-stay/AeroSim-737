using Cinemachine;
using UnityEngine;

/// <summary>
/// Notifies Cinemachine cameras that their targets were warped by the floating
/// origin shift, preventing damped cameras from smoothing across the whole offset.
/// </summary>
public class FloatingOriginCinemachine : MonoBehaviour
{
    [SerializeField] private Transform explicitTarget;
    [SerializeField] private CinemachineVirtualCameraBase[] virtualCameras;

    private void Awake()
    {
        if (virtualCameras == null || virtualCameras.Length == 0)
            virtualCameras = GetComponentsInChildren<CinemachineVirtualCameraBase>(true);
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
        if (virtualCameras == null) return;

        for (int i = 0; i < virtualCameras.Length; i++)
        {
            var vcam = virtualCameras[i];
            if (vcam == null) continue;

            Transform target = explicitTarget != null ? explicitTarget : vcam.Follow;
            if (target == null) target = vcam.LookAt;
            if (target == null) continue;

            vcam.OnTargetObjectWarped(target, offset);
        }
    }
}

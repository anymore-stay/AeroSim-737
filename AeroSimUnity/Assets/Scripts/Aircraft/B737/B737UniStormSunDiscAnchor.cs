using UniStorm;
using UnityEngine;

/// <summary>
/// 将 UniStorm 太阳盘固定到当前活动相机的太阳方向上，消除第三人称绕机时的有限距离视差。
/// </summary>
[DefaultExecutionOrder(150)]
public class B737UniStormSunDiscAnchor : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private UniStormSystem uniStormSystem;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Transform sunDisc;

    [Header("太阳盘距离")]
    [SerializeField] private float referenceDistance = 2000f;
    [SerializeField, Range(0.1f, 0.99f)] private float farClipUsage = 0.9f;
    [SerializeField] private bool preserveAngularSize = true;

    private Vector3 lastBaseScale = Vector3.one;
    private Vector3 lastAppliedScale = Vector3.one;
    private bool hasAppliedScale;

    private void Awake()
    {
        if (uniStormSystem == null)
        {
            uniStormSystem = GetComponent<UniStormSystem>();
        }

        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
        }
    }

    private void LateUpdate()
    {
        if (uniStormSystem == null || !uniStormSystem.UniStormInitialized)
        {
            return;
        }

        Camera activeCamera = GetActiveCamera();
        Light sunLight = GetSunLight();
        Transform activeSunDisc = GetSunDisc();
        if (activeCamera == null || sunLight == null || activeSunDisc == null)
        {
            return;
        }

        float targetDistance = CalculateEffectiveDistance(
            referenceDistance,
            activeCamera.farClipPlane,
            farClipUsage);
        Vector3 sunDirection = CalculateSunDirection(sunLight.transform);

        activeSunDisc.position = CalculateSunDiscPosition(
            activeCamera.transform.position,
            sunDirection,
            targetDistance);

        if (preserveAngularSize)
        {
            Vector3 baseScale = activeSunDisc.localScale;
            if (hasAppliedScale && Approximately(baseScale, lastAppliedScale))
            {
                baseScale = lastBaseScale;
            }

            lastBaseScale = baseScale;
            lastAppliedScale = CalculateSunDiscScale(baseScale, targetDistance, referenceDistance);
            activeSunDisc.localScale = lastAppliedScale;
            hasAppliedScale = true;
        }
        else
        {
            lastAppliedScale = activeSunDisc.localScale;
            hasAppliedScale = true;
        }
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return Mathf.Approximately(left.x, right.x)
            && Mathf.Approximately(left.y, right.y)
            && Mathf.Approximately(left.z, right.z);
    }

    public static Vector3 CalculateSunDirection(Transform sunLightTransform)
    {
        return sunLightTransform == null
            ? Vector3.forward
            : (sunLightTransform.rotation * -Vector3.forward).normalized;
    }

    public static Vector3 CalculateSunDiscPosition(
        Vector3 cameraPosition,
        Vector3 sunDirection,
        float distance)
    {
        Vector3 safeDirection = sunDirection.sqrMagnitude > 0.0001f
            ? sunDirection.normalized
            : Vector3.forward;
        return cameraPosition + safeDirection * Mathf.Max(1f, distance);
    }

    public static float CalculateEffectiveDistance(
        float requestedDistance,
        float cameraFarClip,
        float farClipUsage)
    {
        float safeRequestedDistance = Mathf.Max(1f, requestedDistance);
        if (cameraFarClip <= 1f || float.IsNaN(cameraFarClip) || float.IsInfinity(cameraFarClip))
        {
            return safeRequestedDistance;
        }

        float safeUsage = Mathf.Clamp(farClipUsage, 0.1f, 0.99f);
        return Mathf.Clamp(cameraFarClip * safeUsage, 1f, safeRequestedDistance);
    }

    public static Vector3 CalculateSunDiscScale(
        Vector3 sourceScale,
        float targetDistance,
        float referenceDistance)
    {
        float scaleRatio = Mathf.Max(1f, targetDistance) / Mathf.Max(1f, referenceDistance);
        return sourceScale * scaleRatio;
    }

    private Camera GetActiveCamera()
    {
        if (IsUsableCamera(cameraManager != null ? cameraManager.ActiveCamera : null))
        {
            return cameraManager.ActiveCamera;
        }

        if (uniStormSystem != null && IsUsableCamera(uniStormSystem.PlayerCamera))
        {
            return uniStormSystem.PlayerCamera;
        }

        Camera[] activeCameras = Camera.allCameras;
        for (int index = 0; index < activeCameras.Length; index++)
        {
            Camera candidate = activeCameras[index];
            AudioListener listener = candidate != null ? candidate.GetComponent<AudioListener>() : null;
            if (IsUsableCamera(candidate) && listener != null && listener.enabled)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsUsableCamera(Camera camera)
    {
        return camera != null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
    }

    private Light GetSunLight()
    {
        if (uniStormSystem != null && uniStormSystem.m_SunLight != null)
        {
            return uniStormSystem.m_SunLight;
        }

        GameObject sunObject = GameObject.Find("UniStorm Sun");
        return sunObject != null ? sunObject.GetComponent<Light>() : null;
    }

    private Transform GetSunDisc()
    {
        if (sunDisc != null)
        {
            return sunDisc;
        }

        GameObject sunObject = GameObject.Find("UniStorm Sun Object");
        if (sunObject != null)
        {
            sunDisc = sunObject.transform;
        }

        return sunDisc;
    }
}

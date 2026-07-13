using UniStorm;
using UnityEngine;

/// <summary>
/// 根据 B737 的 JSBSim 海拔动态调整 UniStorm 的大气雾起始距离。
/// </summary>
public class B737UniStormFogDistanceController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private JsbsimBridge bridge;
    [SerializeField] private UniStormSystem uniStormSystem;

    [Header("高度范围（英尺）")]
    [SerializeField] private float lowAltitudeFt = 500f;
    [SerializeField] private float cruiseAltitudeFt = 35000f;

    [Header("雾起始距离（米）")]
    [SerializeField] private float lowAltitudeFogStartDistance = 8000f;
    [SerializeField] private float cruiseFogStartDistance = 35000f;

    private void Awake()
    {
        if (bridge == null)
        {
            bridge = FindFirstObjectByType<JsbsimBridge>();
        }

        if (uniStormSystem == null)
        {
            uniStormSystem = GetComponent<UniStormSystem>();
        }
    }

    private void Update()
    {
        if (uniStormSystem == null || uniStormSystem.m_UniStormAtmosphericFog == null)
        {
            return;
        }

        float altitudeFt = bridge == null ? float.NaN : bridge.AltitudeFt;
        uniStormSystem.m_UniStormAtmosphericFog.startDistance = CalculateFogStartDistance(
            altitudeFt,
            lowAltitudeFt,
            cruiseAltitudeFt,
            lowAltitudeFogStartDistance,
            cruiseFogStartDistance);
    }

    public static float CalculateFogStartDistance(
        float altitudeFt,
        float lowAltitudeFt,
        float cruiseAltitudeFt,
        float lowDistance,
        float cruiseDistance)
    {
        if (float.IsNaN(altitudeFt) || float.IsInfinity(altitudeFt) || altitudeFt <= lowAltitudeFt)
        {
            return lowDistance;
        }

        if (altitudeFt >= cruiseAltitudeFt)
        {
            return cruiseDistance;
        }

        float progress = Mathf.InverseLerp(lowAltitudeFt, cruiseAltitudeFt, altitudeFt);
        return Mathf.Lerp(lowDistance, cruiseDistance, Mathf.SmoothStep(0f, 1f, progress));
    }
}

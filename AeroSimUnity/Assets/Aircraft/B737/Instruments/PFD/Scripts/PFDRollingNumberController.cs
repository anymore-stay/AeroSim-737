using UnityEngine;

public class PFDRollingNumberController : MonoBehaviour
{
    private static readonly float[] AirspeedPlaces = { 100f, 10f, 1f };
    private static readonly float[] AltitudePlaces = { 10000f, 1000f, 100f };

    [Header("空速三位数字轮")]
    [SerializeField] private RectTransform[] guideAirspeedWheels;
    [SerializeField] private RectTransform[] finalAirspeedWheels;

    [Header("海拔前三位数字轮")]
    [SerializeField] private RectTransform[] guideAltitudeMainWheels;
    [SerializeField] private RectTransform[] finalAltitudeMainWheels;

    [Header("海拔末两位五档滚轮")]
    [SerializeField] private RectTransform guideAltitudeTwoDigitWheel;
    [SerializeField] private RectTransform finalAltitudeTwoDigitWheel;

    private float[] guideAirspeedBaseY;
    private float[] finalAirspeedBaseY;
    private float[] guideAltitudeBaseY;
    private float[] finalAltitudeBaseY;
    private float guideAltitudePairBaseY;
    private float finalAltitudePairBaseY;
    private bool hasCachedBasePositions;

    /// <summary>
    /// 更新三位空速数字滚轮。
    /// </summary>
    public void SetAirspeed(float airspeedKts)
    {
        EnsureBasePositions();
        float value = Mathf.Clamp(airspeedKts, 0f, 999.999f);

        for (int i = 0; i < AirspeedPlaces.Length; i++)
        {
            float wheelValue = PFDRollingNumberMath.CalculateDecimalWheelValue(
                value,
                AirspeedPlaces[i],
                1f);
            ApplyWheelValue(guideAirspeedWheels, guideAirspeedBaseY, i, wheelValue);
            ApplyWheelValue(finalAirspeedWheels, finalAirspeedBaseY, i, wheelValue);
        }
    }

    /// <summary>
    /// 更新海拔前三位与末两位五档数字滚轮。
    /// </summary>
    public void SetAltitude(float altitudeFt)
    {
        EnsureBasePositions();
        float value = Mathf.Clamp(altitudeFt, 0f, 99999.999f);

        for (int i = 0; i < AltitudePlaces.Length; i++)
        {
            float wheelValue = PFDRollingNumberMath.CalculateDecimalWheelValue(
                value,
                AltitudePlaces[i],
                20f);
            ApplyWheelValue(guideAltitudeMainWheels, guideAltitudeBaseY, i, wheelValue);
            ApplyWheelValue(finalAltitudeMainWheels, finalAltitudeBaseY, i, wheelValue);
        }

        float pairWheelValue = PFDRollingNumberMath.CalculateAltitudeTwoDigitWheelValue(value);
        ApplyWheelValue(guideAltitudeTwoDigitWheel, guideAltitudePairBaseY, pairWheelValue);
        ApplyWheelValue(finalAltitudeTwoDigitWheel, finalAltitudePairBaseY, pairWheelValue);
    }

    private void EnsureBasePositions()
    {
        if (EnsureBindings())
        {
            // 重新生成 Final 后引用可能变化，必须重新读取各滚轮的初始位置。
            hasCachedBasePositions = false;
        }

        if (hasCachedBasePositions)
        {
            return;
        }

        guideAirspeedBaseY = CacheBasePositions(guideAirspeedWheels);
        finalAirspeedBaseY = CacheBasePositions(finalAirspeedWheels);
        guideAltitudeBaseY = CacheBasePositions(guideAltitudeMainWheels);
        finalAltitudeBaseY = CacheBasePositions(finalAltitudeMainWheels);
        guideAltitudePairBaseY = GetBaseY(guideAltitudeTwoDigitWheel);
        finalAltitudePairBaseY = GetBaseY(finalAltitudeTwoDigitWheel);
        hasCachedBasePositions = true;
    }

    private bool EnsureBindings()
    {
        RectTransform[] descendants = GetComponentsInChildren<RectTransform>(true);
        bool changed = false;

        changed |= BindWheelArray(ref guideAirspeedWheels, descendants, "Guide_AirspeedWheel_");
        changed |= BindWheelArray(ref finalAirspeedWheels, descendants, "Final_AirspeedWheel_");
        changed |= BindWheelArray(ref guideAltitudeMainWheels, descendants, "Guide_AltitudeWheel_");
        changed |= BindWheelArray(ref finalAltitudeMainWheels, descendants, "Final_AltitudeWheel_");
        changed |= BindWheel(
            ref guideAltitudeTwoDigitWheel,
            descendants,
            "Guide_AltitudeTwoDigitWheel");
        changed |= BindWheel(
            ref finalAltitudeTwoDigitWheel,
            descendants,
            "Final_AltitudeTwoDigitWheel");
        return changed;
    }

    private static bool BindWheelArray(
        ref RectTransform[] wheels,
        RectTransform[] descendants,
        string namePrefix)
    {
        bool changed = false;
        if (wheels == null || wheels.Length != 3)
        {
            wheels = new RectTransform[3];
            changed = true;
        }

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] != null)
            {
                continue;
            }

            RectTransform found = FindByName(descendants, namePrefix + i);
            if (found != null)
            {
                wheels[i] = found;
                changed = true;
            }
        }

        return changed;
    }

    private static bool BindWheel(
        ref RectTransform wheel,
        RectTransform[] descendants,
        string objectName)
    {
        if (wheel != null)
        {
            return false;
        }

        RectTransform found = FindByName(descendants, objectName);
        if (found == null)
        {
            return false;
        }

        wheel = found;
        return true;
    }

    private static RectTransform FindByName(RectTransform[] descendants, string objectName)
    {
        foreach (RectTransform descendant in descendants)
        {
            if (descendant.name == objectName)
            {
                return descendant;
            }
        }

        return null;
    }

    private static float[] CacheBasePositions(RectTransform[] wheels)
    {
        if (wheels == null)
        {
            return null;
        }

        float[] result = new float[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            result[i] = GetBaseY(wheels[i]);
        }

        return result;
    }

    private static float GetBaseY(RectTransform wheel)
    {
        return wheel == null ? 0f : wheel.anchoredPosition.y;
    }

    private static void ApplyWheelValue(
        RectTransform[] wheels,
        float[] basePositions,
        int index,
        float wheelValue)
    {
        if (wheels == null
            || basePositions == null
            || index < 0
            || index >= wheels.Length
            || index >= basePositions.Length)
        {
            return;
        }

        ApplyWheelValue(wheels[index], basePositions[index], wheelValue);
    }

    private static void ApplyWheelValue(RectTransform wheel, float baseY, float wheelValue)
    {
        if (wheel == null)
        {
            return;
        }

        Vector2 position = wheel.anchoredPosition;
        position.y = baseY - wheelValue * PFDRollingNumberMath.RowHeight;
        wheel.anchoredPosition = position;
    }
}

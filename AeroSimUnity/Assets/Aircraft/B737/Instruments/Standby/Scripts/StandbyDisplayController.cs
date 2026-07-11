using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将空速、高度和姿态数据转换为 Standby 仪表的 UI 运动。
/// </summary>
public class StandbyDisplayController : MonoBehaviour
{
    private static readonly float[] AirspeedPlaces = { 100f, 10f, 1f };
    private static readonly float[] AltitudePlaces = { 10000f, 1000f, 100f };

    [Header("滚动内容")]
    [SerializeField] private RectTransform speedTapeContent;
    [SerializeField] private RectTransform altitudeTapeContent;
    [SerializeField] private RectTransform attitudeRollGroup;
    [SerializeField] private RectTransform bankPointerGroup;
    [SerializeField] private RectTransform horizonContent;
    [SerializeField] private RectTransform headingRose;

    [Header("空速数字滚轮")]
    [SerializeField] private RawImage[] airspeedDigitWheels = new RawImage[3];

    [Header("高度数字滚轮")]
    [SerializeField] private RawImage[] altitudeMainDigitWheels = new RawImage[3];
    [SerializeField] private RawImage altitudePairWheel;
    [SerializeField] private float altitudeHundredsWheelUvOffsetY = 0.008f;

    [Header("空速带校准")]
    [SerializeField] private float minimumSpeedKnots = 0f;
    [SerializeField] private float maximumSpeedKnots = 500f;
    [SerializeField] private float speedReferenceKnots = 40f;
    [SerializeField] private float speedPixelsPerKnot = 2.8f;
    [SerializeField] private bool invertSpeedTape;

    [Header("高度带校准")]
    [SerializeField] private float minimumAltitudeFeet = -1000f;
    [SerializeField] private float maximumAltitudeFeet = 48000f;
    [SerializeField] private float altitudeReferenceFeet;
    [SerializeField] private float altitudePixelsPerFoot = 0.28f;
    [SerializeField] private bool invertAltitudeTape;

    [Header("姿态校准")]
    [SerializeField] private float pitchPixelsPerDegree = 4.8f;
    [SerializeField] private bool invertPitch;
    [SerializeField] private bool invertRoll;

    [Header("航向校准")]
    [SerializeField] private bool invertHeading = true;

    [Header("当前数据")]
    [SerializeField] private float airspeedKnots = 40f;
    [SerializeField] private float altitudeFeet;
    [SerializeField] private float pitchDegrees;
    [SerializeField] private float rollDegrees;
    [SerializeField] private float magneticHeadingDegrees;

    private Vector2 speedTapeBasePosition;
    private Vector2 altitudeTapeBasePosition;
    private Vector2 horizonBasePosition;
    private float attitudeRollBaseRotationZ;
    private float bankPointerBaseRotationZ;
    private float horizonBaseRotationZ;
    private float headingBaseRotationZ;
    private bool basePoseReady;

    public float AirspeedKnots => airspeedKnots;
    public float AltitudeFeet => altitudeFeet;
    public float PitchDegrees => pitchDegrees;
    public float RollDegrees => rollDegrees;
    public float MagneticHeadingDegrees => magneticHeadingDegrees;

    private void Awake()
    {
        RebuildBasePose();
        RefreshAll();
    }

    /// <summary>
    /// 在 Prefab 完成布局或手动调整 Content 后重新记录零点位置。
    /// </summary>
    public void RebuildBasePose()
    {
        speedTapeBasePosition = GetAnchoredPosition(speedTapeContent);
        altitudeTapeBasePosition = GetAnchoredPosition(altitudeTapeContent);
        horizonBasePosition = GetAnchoredPosition(horizonContent);
        attitudeRollBaseRotationZ = attitudeRollGroup != null
            ? NormalizeAngle(attitudeRollGroup.localEulerAngles.z)
            : 0f;
        bankPointerBaseRotationZ = bankPointerGroup != null
            ? NormalizeAngle(bankPointerGroup.localEulerAngles.z)
            : 0f;
        horizonBaseRotationZ = horizonContent != null ? NormalizeAngle(horizonContent.localEulerAngles.z) : 0f;
        headingBaseRotationZ = headingRose != null ? NormalizeAngle(headingRose.localEulerAngles.z) : 0f;
        basePoseReady = true;
    }

    public void SetAirspeedKnots(float value)
    {
        EnsureBasePose();
        airspeedKnots = Mathf.Clamp(value, minimumSpeedKnots, maximumSpeedKnots);

        if (speedTapeContent != null)
        {
            // 低于基准速度时保持刻度带静止，数字仍显示真实的 0～40 节。
            float tapeSpeedKnots = Mathf.Max(airspeedKnots, speedReferenceKnots);
            float offset = StandbyDisplayMath.CalculateTapeOffset(
                tapeSpeedKnots,
                speedReferenceKnots,
                speedPixelsPerKnot,
                invertSpeedTape);

            // 速度带贴图使用 Point 采样；对齐位移量可避免小数像素移动时出现闪动。
            offset = Mathf.Round(offset);
            speedTapeContent.anchoredPosition = speedTapeBasePosition + Vector2.up * offset;
        }

        UpdateAirspeedWheels();
    }

    public void SetAltitudeFeet(float value)
    {
        EnsureBasePose();
        altitudeFeet = Mathf.Clamp(value, minimumAltitudeFeet, maximumAltitudeFeet);

        if (altitudeTapeContent != null)
        {
            float offset = StandbyDisplayMath.CalculateTapeOffset(
                altitudeFeet,
                altitudeReferenceFeet,
                altitudePixelsPerFoot,
                invertAltitudeTape);
            altitudeTapeContent.anchoredPosition = altitudeTapeBasePosition + Vector2.up * offset;
        }

        UpdateAltitudeWheels();
    }

    public void SetAttitudeDegrees(float pitch, float roll)
    {
        EnsureBasePose();
        pitchDegrees = Mathf.Clamp(pitch, -90f, 90f);
        rollDegrees = Mathf.DeltaAngle(0f, roll);

        if (horizonContent == null)
        {
            return;
        }

        if (attitudeRollGroup != null)
        {
            float pitchDirection = invertPitch ? -1f : 1f;
            horizonContent.anchoredPosition = horizonBasePosition
                + Vector2.down * pitchDegrees * pitchPixelsPerDegree * pitchDirection;
            float rollRotation = StandbyDisplayMath.CalculateHorizonRotation(
                attitudeRollBaseRotationZ,
                rollDegrees,
                invertRoll);
            attitudeRollGroup.localRotation = Quaternion.Euler(0f, 0f, rollRotation);
        }
        else
        {
            horizonContent.anchoredPosition = StandbyDisplayMath.CalculateHorizonPosition(
                horizonBasePosition,
                pitchDegrees,
                rollDegrees,
                pitchPixelsPerDegree,
                invertPitch,
                invertRoll);
            float rotationZ = StandbyDisplayMath.CalculateHorizonRotation(
                horizonBaseRotationZ,
                rollDegrees,
                invertRoll);
            horizonContent.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        }

        if (bankPointerGroup != null)
        {
            float pointerRotation = StandbyDisplayMath.CalculateHorizonRotation(
                bankPointerBaseRotationZ,
                rollDegrees,
                invertRoll);
            bankPointerGroup.localRotation = Quaternion.Euler(0f, 0f, pointerRotation);
        }
    }

    public void SetMagneticHeadingDegrees(float value)
    {
        EnsureBasePose();
        magneticHeadingDegrees = Mathf.Repeat(value, 360f);
        if (headingRose == null)
        {
            return;
        }

        float direction = invertHeading ? -1f : 1f;
        headingRose.localRotation = Quaternion.Euler(
            0f,
            0f,
            headingBaseRotationZ + magneticHeadingDegrees * direction);
    }

    public void RefreshAll()
    {
        SetAirspeedKnots(airspeedKnots);
        SetAltitudeFeet(altitudeFeet);
        SetAttitudeDegrees(pitchDegrees, rollDegrees);
        SetMagneticHeadingDegrees(magneticHeadingDegrees);
    }

    private void UpdateAirspeedWheels()
    {
        for (int i = 0; i < AirspeedPlaces.Length; i++)
        {
            if (airspeedDigitWheels == null || i >= airspeedDigitWheels.Length || airspeedDigitWheels[i] == null)
            {
                continue;
            }

            float wheelValue = StandbyDisplayMath.CalculateDecimalWheelValue(
                Mathf.Clamp(airspeedKnots, 0f, 999.999f),
                AirspeedPlaces[i],
                1f);
            airspeedDigitWheels[i].uvRect = StandbyDisplayMath.CalculateDigitStripUv(
                wheelValue,
                i,
                3,
                39f,
                294f);
        }
    }

    private void UpdateAltitudeWheels()
    {
        float value = Mathf.Clamp(altitudeFeet, 0f, 99999.999f);
        for (int i = 0; i < AltitudePlaces.Length; i++)
        {
            if (altitudeMainDigitWheels == null
                || i >= altitudeMainDigitWheels.Length
                || altitudeMainDigitWheels[i] == null)
            {
                continue;
            }

            float wheelValue = StandbyDisplayMath.CalculateDecimalWheelValue(
                value,
                AltitudePlaces[i],
                20f);
            Rect uvRect = StandbyDisplayMath.CalculateDigitStripUv(
                wheelValue,
                i,
                3,
                39f,
                294f);

            // 原始高度数字图集的百位列零点比前两列高 0.008 UV，需要单独校准。
            if (i == 2)
            {
                uvRect.y += altitudeHundredsWheelUvOffsetY;
            }

            altitudeMainDigitWheels[i].uvRect = uvRect;
        }

        if (altitudePairWheel != null)
        {
            float pairValue = StandbyDisplayMath.CalculateAltitudePairWheelValue(value);
            altitudePairWheel.uvRect = StandbyDisplayMath.CalculateAltitudePairUv(
                pairValue,
                37f,
                136f);
        }
    }

    private void EnsureBasePose()
    {
        if (!basePoseReady)
        {
            RebuildBasePose();
        }
    }

    private static Vector2 GetAnchoredPosition(RectTransform target)
    {
        return target != null ? target.anchoredPosition : Vector2.zero;
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将空速、高度和姿态数据转换为 Standby 仪表的 UI 运动。
/// </summary>
public class StandbyDisplayController : MonoBehaviour
{
    private const float PositionEpsilon = 0.01f;
    private const float RotationEpsilon = 0.01f;
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
    [SerializeField, Min(0.01f)] private float speedTapeSmoothTime = 0.08f;

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
    private float targetSpeedTapeOffset;
    private float displayedSpeedTapeOffset;
    private float speedTapeVelocity;

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

    private void LateUpdate()
    {
        AdvanceSpeedTape(Time.unscaledDeltaTime);
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

        // 数据更新只计算目标位置，实际位移在每个渲染帧平滑追踪，避免低频数据造成跳动。
        float tapeSpeedKnots = Mathf.Max(airspeedKnots, speedReferenceKnots);
        targetSpeedTapeOffset = StandbyDisplayMath.CalculateTapeOffset(
            tapeSpeedKnots,
            speedReferenceKnots,
            speedPixelsPerKnot,
            invertSpeedTape);

        UpdateAirspeedWheels();
    }

    /// <summary>
    /// 推进空速带的平滑动画。公开此方法便于编辑器测试验证不同帧率下的运动。
    /// </summary>
    public void AdvanceSpeedTape(float deltaTime)
    {
        EnsureBasePose();
        if (speedTapeContent == null || deltaTime <= 0f)
        {
            return;
        }

        float nextOffset = Mathf.SmoothDamp(
            displayedSpeedTapeOffset,
            targetSpeedTapeOffset,
            ref speedTapeVelocity,
            speedTapeSmoothTime,
            Mathf.Infinity,
            deltaTime);
        if (Mathf.Abs(nextOffset - targetSpeedTapeOffset) <= PositionEpsilon)
        {
            nextOffset = targetSpeedTapeOffset;
            speedTapeVelocity = 0f;
        }

        displayedSpeedTapeOffset = nextOffset;
        SetAnchoredPositionIfChanged(
            speedTapeContent,
            speedTapeBasePosition + Vector2.up * displayedSpeedTapeOffset);
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
            SetAnchoredPositionIfChanged(
                altitudeTapeContent,
                altitudeTapeBasePosition + Vector2.up * offset);
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
            SetAnchoredPositionIfChanged(
                horizonContent,
                horizonBasePosition + Vector2.down * pitchDegrees * pitchPixelsPerDegree * pitchDirection);
            float rollRotation = StandbyDisplayMath.CalculateHorizonRotation(
                attitudeRollBaseRotationZ,
                rollDegrees,
                invertRoll);
            SetLocalRotationIfChanged(attitudeRollGroup, rollRotation);
        }
        else
        {
            SetAnchoredPositionIfChanged(
                horizonContent,
                StandbyDisplayMath.CalculateHorizonPosition(
                    horizonBasePosition,
                    pitchDegrees,
                    rollDegrees,
                    pitchPixelsPerDegree,
                    invertPitch,
                    invertRoll));
            float rotationZ = StandbyDisplayMath.CalculateHorizonRotation(
                horizonBaseRotationZ,
                rollDegrees,
                invertRoll);
            SetLocalRotationIfChanged(horizonContent, rotationZ);
        }

        if (bankPointerGroup != null)
        {
            float pointerRotation = StandbyDisplayMath.CalculateHorizonRotation(
                bankPointerBaseRotationZ,
                rollDegrees,
                invertRoll);
            SetLocalRotationIfChanged(bankPointerGroup, pointerRotation);
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
        SetLocalRotationIfChanged(
            headingRose,
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
            SetUvRectIfChanged(
                airspeedDigitWheels[i],
                StandbyDisplayMath.CalculateDigitStripUv(
                    wheelValue,
                    i,
                    3,
                    39f,
                    294f));
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

            SetUvRectIfChanged(altitudeMainDigitWheels[i], uvRect);
        }

        if (altitudePairWheel != null)
        {
            float pairValue = StandbyDisplayMath.CalculateAltitudePairWheelValue(value);
            SetUvRectIfChanged(
                altitudePairWheel,
                StandbyDisplayMath.CalculateAltitudePairUv(pairValue, 37f, 136f));
        }
    }

    private static void SetAnchoredPositionIfChanged(RectTransform target, Vector2 value)
    {
        if ((target.anchoredPosition - value).sqrMagnitude > PositionEpsilon * PositionEpsilon)
        {
            target.anchoredPosition = value;
        }
    }

    private static void SetLocalRotationIfChanged(RectTransform target, float rotationZ)
    {
        if (Mathf.Abs(Mathf.DeltaAngle(target.localEulerAngles.z, rotationZ)) > RotationEpsilon)
        {
            target.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        }
    }

    private static void SetUvRectIfChanged(RawImage image, Rect value)
    {
        Rect current = image.uvRect;
        float textureHeight = image.texture != null ? image.texture.height : 0f;
        float yEpsilon = textureHeight > 0f ? 0.5f / textureHeight : 0.0001f;
        if (Mathf.Abs(current.x - value.x) > 0.0001f
            || Mathf.Abs(current.y - value.y) > yEpsilon
            || Mathf.Abs(current.width - value.width) > 0.0001f
            || Mathf.Abs(current.height - value.height) > yEpsilon)
        {
            image.uvRect = value;
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

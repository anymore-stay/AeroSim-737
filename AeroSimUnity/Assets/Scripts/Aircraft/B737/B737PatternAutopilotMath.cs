using UnityEngine;

public static class B737PatternAutopilotMath
{
    public static float NormalizeHeading(float headingDeg)
    {
        headingDeg %= 360f;
        if (headingDeg < 0f)
            headingDeg += 360f;
        return headingDeg;
    }

    public static Vector2 GetPatternCoordinates(
        float northM,
        float eastM,
        float thresholdNorthM,
        float thresholdEastM,
        float runwayHeadingDeg)
    {
        float headingRad = runwayHeadingDeg * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
        Vector2 right = new Vector2(-Mathf.Sin(headingRad), Mathf.Cos(headingRad));
        Vector2 relativePosition = new Vector2(
            northM - thresholdNorthM,
            eastM - thresholdEastM);

        return new Vector2(
            Vector2.Dot(relativePosition, forward),
            Vector2.Dot(relativePosition, right));
    }

    public static float GetLineTrackHeading(
        float courseHeadingDeg,
        float lateralErrorM,
        float lookaheadM,
        float interceptLimitDeg)
    {
        float interceptDeg = -Mathf.Rad2Deg *
                             Mathf.Atan2(lateralErrorM, Mathf.Max(1f, lookaheadM));
        interceptDeg = Mathf.Clamp(
            interceptDeg,
            -Mathf.Abs(interceptLimitDeg),
            Mathf.Abs(interceptLimitDeg));
        return NormalizeHeading(courseHeadingDeg + interceptDeg);
    }

    public static float GetDynamicTurnLeadM(
        float speedKts,
        float bankDeg,
        float scale,
        float minimumM,
        float maximumM)
    {
        float speedMps = Mathf.Clamp(speedKts, 120f, 220f) * 0.514444f;
        float safeBankDeg = Mathf.Clamp(Mathf.Abs(bankDeg), 5f, 35f);
        float radiusM = speedMps * speedMps /
                        (9.80665f * Mathf.Tan(safeBankDeg * Mathf.Deg2Rad));
        float lowerBoundM = Mathf.Max(0f, Mathf.Min(minimumM, maximumM));
        float upperBoundM = Mathf.Max(lowerBoundM, Mathf.Max(minimumM, maximumM));
        return Mathf.Clamp(
            radiusM * Mathf.Max(0f, scale),
            lowerBoundM,
            upperBoundM);
    }

    public static float CalculateAileronCommand(
        float currentHeadingDeg,
        float desiredHeadingDeg,
        float currentBankDeg,
        float rollRateRadSec,
        float bankPerHeadingError,
        float bankLimitDeg,
        float aileronGain,
        float rollRateDamping,
        float maxAileron)
    {
        float headingErrorDeg = Mathf.DeltaAngle(currentHeadingDeg, desiredHeadingDeg);
        float targetBankDeg = Mathf.Clamp(
            headingErrorDeg * bankPerHeadingError,
            -Mathf.Abs(bankLimitDeg),
            Mathf.Abs(bankLimitDeg));
        float bankErrorDeg = targetBankDeg - currentBankDeg;
        float command = bankErrorDeg * aileronGain - rollRateRadSec * rollRateDamping;
        return Mathf.Clamp(command, -Mathf.Abs(maxAileron), Mathf.Abs(maxAileron));
    }

    public static float CalculateElevatorCommand(
        float targetPitchDeg,
        float currentPitchDeg,
        float pitchRateRadSec,
        float pitchGain,
        float pitchRateDamping,
        float noseUpLimit,
        float noseDownLimit)
    {
        float pitchErrorDeg = targetPitchDeg - currentPitchDeg;
        float noseUpDemand = pitchErrorDeg * pitchGain -
                             pitchRateRadSec * pitchRateDamping;
        return Mathf.Clamp(
            -noseUpDemand,
            -Mathf.Abs(noseUpLimit),
            Mathf.Abs(noseDownLimit));
    }

    public static float CalculateFlareTargetPitch(
        float aglFt,
        float flareStartAglFt,
        float touchdownAglFt,
        float entrySinkRateFps,
        float touchdownSinkRateFps,
        float currentVerticalSpeedFps,
        float basePitchDeg,
        float verticalSpeedToPitchGain,
        float minimumPitchDeg,
        float maximumPitchDeg)
    {
        float altitudeSpanFt = Mathf.Max(1f, flareStartAglFt - touchdownAglFt);
        float flareProgress = Mathf.Clamp01(
            (flareStartAglFt - aglFt) / altitudeSpanFt);
        float desiredVerticalSpeedFps = Mathf.Lerp(
            -Mathf.Abs(entrySinkRateFps),
            -Mathf.Abs(touchdownSinkRateFps),
            flareProgress);
        float targetPitchDeg = basePitchDeg +
                               (desiredVerticalSpeedFps - currentVerticalSpeedFps) *
                               Mathf.Max(0f, verticalSpeedToPitchGain);
        float lowerPitchDeg = Mathf.Min(minimumPitchDeg, maximumPitchDeg);
        float upperPitchDeg = Mathf.Max(minimumPitchDeg, maximumPitchDeg);
        return Mathf.Clamp(targetPitchDeg, lowerPitchDeg, upperPitchDeg);
    }
}

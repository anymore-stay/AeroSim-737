using UnityEngine;

public static class SideSlipProtectionMath
{
    public static float CalculateRudderCommand(
        float pilotCommand,
        float sideSlipDeg,
        float limitDeg,
        float softZoneDeg,
        float correctionGain,
        float maxCorrection)
    {
        if (float.IsNaN(sideSlipDeg) || float.IsInfinity(sideSlipDeg))
            return Mathf.Clamp(pilotCommand, -1f, 1f);

        float safeLimit = Mathf.Max(0.1f, limitDeg);
        float onset = Mathf.Max(0f, safeLimit - Mathf.Max(0f, softZoneDeg));
        float absoluteSideSlip = Mathf.Abs(sideSlipDeg);
        if (absoluteSideSlip <= onset)
            return Mathf.Clamp(pilotCommand, -1f, 1f);

        float protection = Mathf.InverseLerp(onset, safeLimit, absoluteSideSlip);
        float protectedCommand = pilotCommand;

        // A command opposite to beta increases the current aerodynamic side slip.
        if (protectedCommand * sideSlipDeg < 0f)
            protectedCommand *= 1f - protection;

        float correction = Mathf.Min(
            (absoluteSideSlip - onset) * Mathf.Max(0f, correctionGain),
            Mathf.Clamp01(maxCorrection));
        protectedCommand += Mathf.Sign(sideSlipDeg) * correction;

        if (absoluteSideSlip >= safeLimit && protectedCommand * sideSlipDeg < 0f)
            protectedCommand = 0f;

        return Mathf.Clamp(protectedCommand, -1f, 1f);
    }
}

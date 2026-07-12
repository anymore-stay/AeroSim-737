using UnityEngine;

public static class BankAngleProtectionMath
{
    public static float CalculateAileronCommand(
        float pilotCommand,
        float bankAngleDeg,
        float limitDeg,
        float softZoneDeg,
        float correctionGain,
        float maxCorrection)
    {
        if (float.IsNaN(bankAngleDeg) || float.IsInfinity(bankAngleDeg))
            return Mathf.Clamp(pilotCommand, -1f, 1f);

        float safeLimit = Mathf.Clamp(limitDeg, 1f, 89f);
        float onset = Mathf.Max(0f, safeLimit - Mathf.Max(0f, softZoneDeg));
        float absoluteBank = Mathf.Abs(bankAngleDeg);
        if (absoluteBank <= onset)
            return Mathf.Clamp(pilotCommand, -1f, 1f);

        float protection = Mathf.InverseLerp(onset, safeLimit, absoluteBank);
        float protectedCommand = pilotCommand;

        // A command with the same sign as bank angle rolls farther away from level.
        if (protectedCommand * bankAngleDeg > 0f)
            protectedCommand *= 1f - protection;

        float correction = Mathf.Min(
            (absoluteBank - onset) * Mathf.Max(0f, correctionGain),
            Mathf.Clamp01(maxCorrection));
        protectedCommand -= Mathf.Sign(bankAngleDeg) * correction;

        if (absoluteBank >= safeLimit && protectedCommand * bankAngleDeg > 0f)
            protectedCommand = 0f;

        return Mathf.Clamp(protectedCommand, -1f, 1f);
    }
}

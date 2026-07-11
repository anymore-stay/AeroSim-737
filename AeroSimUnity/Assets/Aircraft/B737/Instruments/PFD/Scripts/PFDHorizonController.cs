using UnityEngine;

public class PFDHorizonController : MonoBehaviour
{
    [SerializeField] private RectTransform guideHorizon;
    [SerializeField] private RectTransform finalHorizon;
    [SerializeField] private RectTransform guideHorizonOverlay;
    [SerializeField] private RectTransform finalHorizonOverlay;
    [SerializeField] private RectTransform guideBankDiamond;
    [SerializeField] private RectTransform finalBankDiamond;
    [SerializeField] private float pixelsPerDegree = 5.2f;
    [SerializeField] private bool invertPitch;
    [SerializeField] private bool invertRoll;
    [SerializeField] private bool smoothMotion;
    [SerializeField, Min(0f)] private float smoothingSpeed = 12f;

    private RectTransform cachedGuideHorizon;
    private RectTransform cachedFinalHorizon;
    private RectTransform cachedGuideHorizonOverlay;
    private RectTransform cachedFinalHorizonOverlay;
    private RectTransform cachedGuideBankDiamond;
    private RectTransform cachedFinalBankDiamond;
    private Vector2 guideBasePosition;
    private Vector2 finalBasePosition;
    private float guideBaseRotationZ;
    private float finalBaseRotationZ;
    private Vector2 guideOverlayCenter;
    private Vector2 finalOverlayCenter;
    private Vector2 guideDiamondBasePosition;
    private Vector2 finalDiamondBasePosition;
    private float guideDiamondBaseRotationZ;
    private float finalDiamondBaseRotationZ;
    private float currentPitchDeg;
    private float currentRollDeg;
    private float targetPitchDeg;
    private float targetRollDeg;
    private bool hasPendingAttitude;

    public void SetAttitude(float pitchDeg, float rollDeg)
    {
        EnsureBindings();

        targetPitchDeg = pitchDeg;
        targetRollDeg = rollDeg;

        if (!Application.isPlaying || !smoothMotion || smoothingSpeed <= 0f)
        {
            currentPitchDeg = targetPitchDeg;
            currentRollDeg = targetRollDeg;
            hasPendingAttitude = false;
            ApplyAttitude(currentPitchDeg, currentRollDeg);
            return;
        }

        hasPendingAttitude = true;
    }

    public void ResetAttitude()
    {
        EnsureBindings();

        RestoreBasePose(guideHorizon, guideBasePosition, guideBaseRotationZ);
        RestoreBasePose(finalHorizon, finalBasePosition, finalBaseRotationZ);
        RestoreBasePose(guideBankDiamond, guideDiamondBasePosition, guideDiamondBaseRotationZ);
        RestoreBasePose(finalBankDiamond, finalDiamondBasePosition, finalDiamondBaseRotationZ);

        currentPitchDeg = 0f;
        currentRollDeg = 0f;
        targetPitchDeg = 0f;
        targetRollDeg = 0f;
        hasPendingAttitude = false;
    }

    private void Update()
    {
        if (!hasPendingAttitude)
        {
            return;
        }

        if (!smoothMotion || smoothingSpeed <= 0f)
        {
            currentPitchDeg = targetPitchDeg;
            currentRollDeg = targetRollDeg;
            hasPendingAttitude = false;
            ApplyAttitude(currentPitchDeg, currentRollDeg);
            return;
        }

        float interpolation = 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
        currentPitchDeg = Mathf.Lerp(currentPitchDeg, targetPitchDeg, interpolation);
        currentRollDeg = Mathf.LerpAngle(currentRollDeg, targetRollDeg, interpolation);

        if (Mathf.Abs(currentPitchDeg - targetPitchDeg) < 0.001f
            && Mathf.Abs(Mathf.DeltaAngle(currentRollDeg, targetRollDeg)) < 0.001f)
        {
            currentPitchDeg = targetPitchDeg;
            currentRollDeg = targetRollDeg;
            hasPendingAttitude = false;
        }

        ApplyAttitude(currentPitchDeg, currentRollDeg);
    }

    private void EnsureBindings()
    {
        if (guideHorizon == null
            || finalHorizon == null
            || guideHorizonOverlay == null
            || finalHorizonOverlay == null
            || guideBankDiamond == null
            || finalBankDiamond == null)
        {
            RectTransform[] descendants = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform descendant in descendants)
            {
                if (guideHorizon == null && descendant.name == "Guide_Horizon")
                {
                    guideHorizon = descendant;
                }
                else if (finalHorizon == null && descendant.name == "Final_Horizon")
                {
                    finalHorizon = descendant;
                }
                else if (guideHorizonOverlay == null && descendant.name == "Guide_HorizonOverlay")
                {
                    guideHorizonOverlay = descendant;
                }
                else if (finalHorizonOverlay == null && descendant.name == "Final_HorizonOverlay")
                {
                    finalHorizonOverlay = descendant;
                }
                else if (guideBankDiamond == null && descendant.name == "Guide_bank_diamond")
                {
                    guideBankDiamond = descendant;
                }
                else if (finalBankDiamond == null && descendant.name == "Final_bank_diamond")
                {
                    finalBankDiamond = descendant;
                }
            }
        }

        if (guideHorizon != cachedGuideHorizon)
        {
            cachedGuideHorizon = guideHorizon;
            CacheBasePose(guideHorizon, out guideBasePosition, out guideBaseRotationZ);
        }

        if (finalHorizon != cachedFinalHorizon)
        {
            cachedFinalHorizon = finalHorizon;
            CacheBasePose(finalHorizon, out finalBasePosition, out finalBaseRotationZ);
        }

        if (guideHorizonOverlay != cachedGuideHorizonOverlay)
        {
            cachedGuideHorizonOverlay = guideHorizonOverlay;
            guideOverlayCenter = GetAnchoredPosition(guideHorizonOverlay);
        }

        if (finalHorizonOverlay != cachedFinalHorizonOverlay)
        {
            cachedFinalHorizonOverlay = finalHorizonOverlay;
            finalOverlayCenter = GetAnchoredPosition(finalHorizonOverlay);
        }

        if (guideBankDiamond != cachedGuideBankDiamond)
        {
            cachedGuideBankDiamond = guideBankDiamond;
            CacheBasePose(guideBankDiamond, out guideDiamondBasePosition, out guideDiamondBaseRotationZ);
        }

        if (finalBankDiamond != cachedFinalBankDiamond)
        {
            cachedFinalBankDiamond = finalBankDiamond;
            CacheBasePose(finalBankDiamond, out finalDiamondBasePosition, out finalDiamondBaseRotationZ);
        }
    }

    private void ApplyAttitude(float pitchDeg, float rollDeg)
    {
        EnsureBindings();
        ApplyPose(guideHorizon, guideBasePosition, guideBaseRotationZ, pitchDeg, rollDeg);
        ApplyPose(finalHorizon, finalBasePosition, finalBaseRotationZ, pitchDeg, rollDeg);
        ApplyBankDiamond(
            guideBankDiamond,
            guideDiamondBasePosition,
            guideDiamondBaseRotationZ,
            guideOverlayCenter,
            rollDeg);
        ApplyBankDiamond(
            finalBankDiamond,
            finalDiamondBasePosition,
            finalDiamondBaseRotationZ,
            finalOverlayCenter,
            rollDeg);
    }

    private void ApplyBankDiamond(
        RectTransform bankDiamond,
        Vector2 basePosition,
        float baseRotationZ,
        Vector2 overlayCenter,
        float rollDeg)
    {
        if (bankDiamond == null)
        {
            return;
        }

        bankDiamond.anchoredPosition = PFDHorizonMath.RotatePointAroundCenter(
            basePosition,
            overlayCenter,
            rollDeg,
            invertRoll);

        SetRotationZ(
            bankDiamond,
            PFDHorizonMath.CalculateRotationZ(baseRotationZ, rollDeg, invertRoll));
    }

    private void ApplyPose(
        RectTransform horizon,
        Vector2 basePosition,
        float baseRotationZ,
        float pitchDeg,
        float rollDeg)
    {
        if (horizon == null)
        {
            return;
        }

        horizon.anchoredPosition = PFDHorizonMath.CalculateAnchoredPosition(
            basePosition,
            pitchDeg,
            rollDeg,
            pixelsPerDegree,
            invertPitch,
            invertRoll);

        SetRotationZ(
            horizon,
            PFDHorizonMath.CalculateRotationZ(baseRotationZ, rollDeg, invertRoll));
    }

    private static void CacheBasePose(
        RectTransform horizon,
        out Vector2 basePosition,
        out float baseRotationZ)
    {
        if (horizon == null)
        {
            basePosition = Vector2.zero;
            baseRotationZ = 0f;
            return;
        }

        basePosition = horizon.anchoredPosition;
        baseRotationZ = horizon.localEulerAngles.z;
    }

    private static void RestoreBasePose(
        RectTransform horizon,
        Vector2 basePosition,
        float baseRotationZ)
    {
        if (horizon == null)
        {
            return;
        }

        horizon.anchoredPosition = basePosition;
        SetRotationZ(horizon, baseRotationZ);
    }

    private static Vector2 GetAnchoredPosition(RectTransform rectTransform)
    {
        return rectTransform == null ? Vector2.zero : rectTransform.anchoredPosition;
    }

    private static void SetRotationZ(RectTransform horizon, float rotationZ)
    {
        Vector3 localEulerAngles = horizon.localEulerAngles;
        localEulerAngles.z = rotationZ;
        horizon.localEulerAngles = localEulerAngles;
    }
}

using UnityEngine;

public class PFDHeadingRoseController : MonoBehaviour
{
    [SerializeField] private RectTransform guideHeadingRose;
    [SerializeField] private RectTransform finalHeadingRose;
    [SerializeField] private RectTransform guideHeadingReference;
    [SerializeField] private RectTransform finalHeadingReference;

    private RectTransform cachedGuideHeadingRose;
    private RectTransform cachedFinalHeadingRose;
    private RectTransform cachedGuideHeadingReference;
    private RectTransform cachedFinalHeadingReference;
    private float guideBaseRotationZ;
    private float finalBaseRotationZ;
    private float guideReferenceBaseRotationZ;
    private float finalReferenceBaseRotationZ;

    /// <summary>
    /// 设置当前磁航向，单位为度。后续可直接传入 JsbsimBridge.HeadingDeg。
    /// </summary>
    public void SetMagneticHeading(float magneticHeadingDeg)
    {
        EnsureBindings();
        ApplyHeading(guideHeadingRose, guideBaseRotationZ, magneticHeadingDeg);
        ApplyHeading(finalHeadingRose, finalBaseRotationZ, magneticHeadingDeg);
        ApplyReferenceCompensation(
            guideHeadingReference,
            guideReferenceBaseRotationZ,
            magneticHeadingDeg);
        ApplyReferenceCompensation(
            finalHeadingReference,
            finalReferenceBaseRotationZ,
            magneticHeadingDeg);
    }

    private void EnsureBindings()
    {
        if (guideHeadingRose == null
            || finalHeadingRose == null
            || guideHeadingReference == null
            || finalHeadingReference == null)
        {
            RectTransform[] descendants = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform descendant in descendants)
            {
                if (guideHeadingRose == null && descendant.name == "Guide_heading_rose")
                {
                    guideHeadingRose = descendant;
                }
                else if (finalHeadingRose == null && descendant.name == "Final_heading_rose")
                {
                    finalHeadingRose = descendant;
                }
                else if (guideHeadingReference == null && descendant.name == "Guide_HeadingReference")
                {
                    guideHeadingReference = descendant;
                }
                else if (finalHeadingReference == null && descendant.name == "Final_HeadingReference")
                {
                    finalHeadingReference = descendant;
                }
            }
        }

        if (guideHeadingRose != cachedGuideHeadingRose)
        {
            cachedGuideHeadingRose = guideHeadingRose;
            guideBaseRotationZ = GetRotationZ(guideHeadingRose);
        }

        if (finalHeadingRose != cachedFinalHeadingRose)
        {
            cachedFinalHeadingRose = finalHeadingRose;
            finalBaseRotationZ = GetRotationZ(finalHeadingRose);
        }

        if (guideHeadingReference != cachedGuideHeadingReference)
        {
            cachedGuideHeadingReference = guideHeadingReference;
            guideReferenceBaseRotationZ = GetRotationZ(guideHeadingReference);
        }

        if (finalHeadingReference != cachedFinalHeadingReference)
        {
            cachedFinalHeadingReference = finalHeadingReference;
            finalReferenceBaseRotationZ = GetRotationZ(finalHeadingReference);
        }
    }

    private static void ApplyReferenceCompensation(
        RectTransform headingReference,
        float baseRotationZ,
        float magneticHeadingDeg)
    {
        if (headingReference == null)
        {
            return;
        }

        // 辅助线挂在旋转盘下面，因此施加等量反向补偿，使其在屏幕上保持固定。
        Vector3 localEulerAngles = headingReference.localEulerAngles;
        localEulerAngles.z = baseRotationZ + Mathf.Repeat(magneticHeadingDeg, 360f);
        headingReference.localEulerAngles = localEulerAngles;
    }

    private static void ApplyHeading(
        RectTransform headingRose,
        float baseRotationZ,
        float magneticHeadingDeg)
    {
        if (headingRose == null)
        {
            return;
        }

        Vector3 localEulerAngles = headingRose.localEulerAngles;
        localEulerAngles.z = PFDHeadingRoseMath.CalculateRotationZ(
            baseRotationZ,
            magneticHeadingDeg);
        headingRose.localEulerAngles = localEulerAngles;
    }

    private static float GetRotationZ(RectTransform headingRose)
    {
        return headingRose == null ? 0f : headingRose.localEulerAngles.z;
    }
}

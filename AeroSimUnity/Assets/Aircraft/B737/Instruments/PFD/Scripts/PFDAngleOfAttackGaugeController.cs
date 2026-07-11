using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class PFDAngleOfAttackGaugeController : MonoBehaviour
{
    [Header("迎角有效范围")]
    [SerializeField] private float minimumAoaDeg;
    [SerializeField] private float maximumAoaDeg = 15f;

    [Header("指针扇形范围")]
    [SerializeField] private float minimumPointerRotationZ = 44.447f;
    [SerializeField] private float maximumPointerRotationZ = -178.915f;

    [Header("界面引用")]
    [SerializeField] private RectTransform guideAoaPointer;
    [SerializeField] private RectTransform finalAoaPointer;
    [SerializeField] private Text guideAoaValue;
    [SerializeField] private Text finalAoaValue;

    /// <summary>
    /// 设置当前迎角，单位为度。后续可直接传入 JsbsimBridge 的迎角公开属性。
    /// </summary>
    public void SetAngleOfAttack(float angleOfAttackDeg)
    {
        EnsureBindings();

        float displayedAoa = PFDAngleOfAttackGaugeMath.ClampAngleOfAttack(
            angleOfAttackDeg,
            minimumAoaDeg,
            maximumAoaDeg);
        float rotationZ = PFDAngleOfAttackGaugeMath.CalculatePointerRotationZ(
            displayedAoa,
            minimumAoaDeg,
            maximumAoaDeg,
            minimumPointerRotationZ,
            maximumPointerRotationZ);

        ApplyPointerRotation(guideAoaPointer, rotationZ);
        ApplyPointerRotation(finalAoaPointer, rotationZ);

        string valueText = displayedAoa.ToString("0.0", CultureInfo.InvariantCulture);
        ApplyValue(guideAoaValue, valueText);
        ApplyValue(finalAoaValue, valueText);
    }

    private void EnsureBindings()
    {
        if (guideAoaPointer == null
            || finalAoaPointer == null
            || guideAoaValue == null
            || finalAoaValue == null)
        {
            RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rectTransform in rectTransforms)
            {
                if (guideAoaPointer == null && rectTransform.name == "Guide_AoaPointer")
                {
                    guideAoaPointer = rectTransform;
                }
                else if (finalAoaPointer == null && rectTransform.name == "Final_AoaPointer")
                {
                    finalAoaPointer = rectTransform;
                }
            }

            Text[] texts = GetComponentsInChildren<Text>(true);
            foreach (Text text in texts)
            {
                if (guideAoaValue == null && text.name == "Guide_AoaValue")
                {
                    guideAoaValue = text;
                }
                else if (finalAoaValue == null && text.name == "Final_AoaValue")
                {
                    finalAoaValue = text;
                }
            }
        }
    }

    private static void ApplyPointerRotation(RectTransform pointer, float rotationZ)
    {
        if (pointer == null)
        {
            return;
        }

        Vector3 localEulerAngles = pointer.localEulerAngles;
        localEulerAngles.z = rotationZ;
        pointer.localEulerAngles = localEulerAngles;
    }

    private static void ApplyValue(Text value, string text)
    {
        if (value != null)
        {
            value.text = text;
        }
    }
}

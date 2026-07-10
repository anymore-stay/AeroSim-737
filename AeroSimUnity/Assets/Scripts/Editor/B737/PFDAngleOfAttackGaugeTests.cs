using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class PFDAngleOfAttackGaugeTests
{
    [Test]
    public void MinimumAndMaximumAoaMapToFanEndpoints()
    {
        Type mathType = GetRuntimeType("PFDAngleOfAttackGaugeMath");
        MethodInfo method = GetStaticMethod(mathType, "CalculatePointerRotationZ");

        float minimumRotation = InvokeFloat(method, 0f, 0f, 15f, 44.447f, -178.915f);
        float maximumRotation = InvokeFloat(method, 15f, 0f, 15f, 44.447f, -178.915f);

        Assert.That(minimumRotation, Is.EqualTo(44.447f).Within(0.001f));
        Assert.That(maximumRotation, Is.EqualTo(-178.915f).Within(0.001f));
    }

    [Test]
    public void AoaOutsideRangeIsClampedInsideFan()
    {
        Type mathType = GetRuntimeType("PFDAngleOfAttackGaugeMath");
        MethodInfo method = GetStaticMethod(mathType, "CalculatePointerRotationZ");

        float belowMinimum = InvokeFloat(method, -10f, 0f, 15f, 44.447f, -178.915f);
        float aboveMaximum = InvokeFloat(method, 30f, 0f, 15f, 44.447f, -178.915f);

        Assert.That(belowMinimum, Is.EqualTo(44.447f).Within(0.001f));
        Assert.That(aboveMaximum, Is.EqualTo(-178.915f).Within(0.001f));
    }

    [Test]
    public void ControllerDrivesGuideAndInactiveFinalPointerAndValueTogether()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            RectTransform guidePointer = CreateGauge(root.transform, "Guide", out Text guideValue);
            RectTransform finalPointer = CreateGauge(root.transform, "Final", out Text finalValue);
            finalPointer.parent.gameObject.SetActive(false);

            Type controllerType = GetRuntimeType("PFDAngleOfAttackGaugeController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setMethod = controllerType.GetMethod(
                "SetAngleOfAttack",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(setMethod, Is.Not.Null, "SetAngleOfAttack 公开实例方法不存在。");
            setMethod.Invoke(controller, new object[] { 7.5f });

            Assert.That(Mathf.DeltaAngle(-67.234f, guidePointer.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(-67.234f, finalPointer.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
            Assert.That(guideValue.text, Is.EqualTo("7.5"));
            Assert.That(finalValue.text, Is.EqualTo("7.5"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void AutomaticSimulatorStartsAtZeroAndReachesMaximumHalfway()
    {
        Type simulatorType = GetRuntimeType("PFDAngleOfAttackGaugeSimulator");
        MethodInfo method = GetStaticMethod(simulatorType, "EvaluateAutomaticAoa");

        float start = InvokeFloat(method, 0f, 0f, 15f, 20f);
        float maximum = InvokeFloat(method, 10f, 0f, 15f, 20f);
        float completed = InvokeFloat(method, 20f, 0f, 15f, 20f);

        Assert.That(start, Is.EqualTo(0f).Within(0.001f));
        Assert.That(maximum, Is.EqualTo(15f).Within(0.001f));
        Assert.That(completed, Is.EqualTo(0f).Within(0.001f));
    }

    private static RectTransform CreateGauge(Transform parent, string prefix, out Text valueText)
    {
        GameObject gaugeObject = new GameObject(prefix + "_aoa_gauge", typeof(RectTransform));
        RectTransform gauge = gaugeObject.GetComponent<RectTransform>();
        gauge.SetParent(parent, false);

        GameObject pointerObject = new GameObject(prefix + "_AoaPointer", typeof(RectTransform));
        RectTransform pointer = pointerObject.GetComponent<RectTransform>();
        pointer.SetParent(gauge, false);

        GameObject valueObject = new GameObject(prefix + "_AoaValue", typeof(RectTransform), typeof(Text));
        valueObject.transform.SetParent(gauge, false);
        valueText = valueObject.GetComponent<Text>();
        return pointer;
    }

    private static Type GetRuntimeType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName + " 尚未实现或尚未编译。");
        return type;
    }

    private static MethodInfo GetStaticMethod(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, methodName + " 公开静态方法不存在。");
        return method;
    }

    private static float InvokeFloat(MethodInfo method, params object[] arguments)
    {
        return (float)method.Invoke(null, arguments);
    }
}

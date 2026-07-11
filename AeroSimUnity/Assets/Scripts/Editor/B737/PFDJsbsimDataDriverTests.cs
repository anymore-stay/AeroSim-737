using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PFDJsbsimDataDriverTests
{
    [Test]
    public void TrueHeadingSubtractsEastMagneticVariationAndWrapsToZeroToThreeSixty()
    {
        Type mathType = GetRuntimeType("PFDJsbsimDataMath");
        MethodInfo method = GetStaticMethod(mathType, "CalculateMagneticHeading");

        float heading = (float)method.Invoke(null, new object[] { 10f, 20f });

        Assert.That(heading, Is.EqualTo(350f).Within(0.001f));
    }

    [Test]
    public void VerticalSpeedConvertsFeetPerSecondToFeetPerMinute()
    {
        Type mathType = GetRuntimeType("PFDJsbsimDataMath");
        MethodInfo method = GetStaticMethod(mathType, "ConvertVerticalSpeedToFpm");

        float verticalSpeedFpm = (float)method.Invoke(null, new object[] { -12.5f });

        Assert.That(verticalSpeedFpm, Is.EqualTo(-750f).Within(0.001f));
    }

    [Test]
    public void JsbsimBridgeExposesAngleOfAttackInDegrees()
    {
        Type bridgeType = GetRuntimeType("JsbsimBridge");
        PropertyInfo property = bridgeType.GetProperty(
            "AngleOfAttackDeg",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.That(property, Is.Not.Null, "JsbsimBridge 缺少 AngleOfAttackDeg 只读属性。");
        Assert.That(property.CanRead, Is.True);
        Assert.That(property.SetMethod, Is.Not.Null);
        Assert.That(property.SetMethod.IsPublic, Is.False, "AngleOfAttackDeg 不应允许外部写入。");
    }

    [Test]
    public void JsbsimBridgeTargetPositionAppliesAltitudeOffsetOnlyToVerticalAxis()
    {
        Type bridgeType = GetRuntimeType("JsbsimBridge");
        MethodInfo method = bridgeType.GetMethod(
            "CalculateTargetPosition",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null, "JsbsimBridge 缺少高度偏移位置计算方法。");

        Vector3 targetPosition = (Vector3)method.Invoke(null, new object[]
        {
            new Vector3(10f, 20f, 30f),
            new Vector3(5f, 0f, 7f),
            2f,
            -3f
        });

        Assert.That(targetPosition, Is.EqualTo(new Vector3(15f, 19f, 37f)));
    }

    [Test]
    public void RuntimeDriverTypeExists()
    {
        Assert.That(GetRuntimeType("PFDJsbsimDataDriver"), Is.Not.Null);
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
}

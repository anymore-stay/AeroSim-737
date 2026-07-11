using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PFDHeadingRoseTests
{
    [Test]
    public void ZeroHeadingKeepsBaseRotation()
    {
        Type mathType = GetRuntimeType("PFDHeadingRoseMath");
        MethodInfo method = GetStaticMethod(mathType, "CalculateRotationZ");

        float rotation = (float)method.Invoke(null, new object[] { 12f, 0f });

        Assert.That(Mathf.DeltaAngle(12f, rotation), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void PositiveHeadingRotatesRoseInOppositeDirection()
    {
        Type mathType = GetRuntimeType("PFDHeadingRoseMath");
        MethodInfo method = GetStaticMethod(mathType, "CalculateRotationZ");

        float rotation = (float)method.Invoke(null, new object[] { 12f, 90f });

        Assert.That(Mathf.DeltaAngle(-78f, rotation), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ControllerDrivesGuideAndInactiveFinalTogether()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            RectTransform guide = CreateRose(root.transform, "Guide_heading_rose", 5f);
            RectTransform final = CreateRose(root.transform, "Final_heading_rose", -10f);
            final.gameObject.SetActive(false);

            Type controllerType = GetRuntimeType("PFDHeadingRoseController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setHeadingMethod = controllerType.GetMethod(
                "SetMagneticHeading",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(setHeadingMethod, Is.Not.Null, "SetMagneticHeading 公开实例方法不存在。");
            setHeadingMethod.Invoke(controller, new object[] { 90f });

            Assert.That(Mathf.DeltaAngle(-85f, guide.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(-100f, final.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ReferenceLineStaysFixedWhileItsHeadingRoseParentRotates()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            RectTransform guide = CreateRose(root.transform, "Guide_heading_rose", 0f);
            RectTransform reference = CreateRose(guide, "Guide_HeadingReference", 0f);

            Type controllerType = GetRuntimeType("PFDHeadingRoseController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setHeadingMethod = controllerType.GetMethod(
                "SetMagneticHeading",
                BindingFlags.Public | BindingFlags.Instance);

            setHeadingMethod.Invoke(controller, new object[] { 90f });

            Assert.That(Mathf.DeltaAngle(-90f, guide.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(90f, reference.localEulerAngles.z), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(0f, reference.eulerAngles.z), Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void AutomaticSimulatorStartsAtZeroAndCompletesOneTurn()
    {
        Type simulatorType = GetRuntimeType("PFDHeadingRoseSimulator");
        MethodInfo method = GetStaticMethod(simulatorType, "EvaluateAutomaticHeading");

        float start = (float)method.Invoke(null, new object[] { 0f, 120f });
        float quarter = (float)method.Invoke(null, new object[] { 30f, 120f });
        float completed = (float)method.Invoke(null, new object[] { 120f, 120f });

        Assert.That(start, Is.EqualTo(0f).Within(0.001f));
        Assert.That(quarter, Is.EqualTo(90f).Within(0.001f));
        Assert.That(completed, Is.EqualTo(0f).Within(0.001f));
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

    private static RectTransform CreateRose(Transform parent, string name, float rotationZ)
    {
        GameObject roseObject = new GameObject(name, typeof(RectTransform));
        RectTransform rose = roseObject.GetComponent<RectTransform>();
        rose.SetParent(parent, false);
        rose.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        return rose;
    }
}

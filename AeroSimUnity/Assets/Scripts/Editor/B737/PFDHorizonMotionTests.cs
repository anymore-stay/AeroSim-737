using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PFDHorizonMotionTests
{
    [Test]
    public void ZeroAttitudeKeepsBasePose()
    {
        Type mathType = GetRuntimeType("PFDHorizonMath");
        MethodInfo positionMethod = GetStaticMethod(mathType, "CalculateAnchoredPosition");
        MethodInfo rotationMethod = GetStaticMethod(mathType, "CalculateRotationZ");

        Vector2 basePosition = new Vector2(-35f, -29.7f);
        Vector2 position = InvokeVector2(
            positionMethod,
            basePosition,
            0f,
            0f,
            5.2f,
            false,
            false);
        float rotation = InvokeFloat(rotationMethod, -90f, 0f, false);

        Assert.That(position.x, Is.EqualTo(basePosition.x).Within(0.001f));
        Assert.That(position.y, Is.EqualTo(basePosition.y).Within(0.001f));
        Assert.That(rotation, Is.EqualTo(-90f).Within(0.001f));
    }

    [Test]
    public void PositivePitchMovesHorizonDownByTextureScale()
    {
        Type mathType = GetRuntimeType("PFDHorizonMath");
        MethodInfo positionMethod = GetStaticMethod(mathType, "CalculateAnchoredPosition");

        Vector2 position = InvokeVector2(
            positionMethod,
            Vector2.zero,
            10f,
            0f,
            5.2f,
            false,
            false);

        Assert.That(position.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(position.y, Is.EqualTo(-52f).Within(0.001f));
    }

    [Test]
    public void PositiveRollRotatesHorizonCounterClockwise()
    {
        Type mathType = GetRuntimeType("PFDHorizonMath");
        MethodInfo rotationMethod = GetStaticMethod(mathType, "CalculateRotationZ");

        float rotation = InvokeFloat(rotationMethod, -90f, 30f, false);

        Assert.That(rotation, Is.EqualTo(-60f).Within(0.001f));
    }

    [Test]
    public void PitchOffsetFollowsRollDirection()
    {
        Type mathType = GetRuntimeType("PFDHorizonMath");
        MethodInfo positionMethod = GetStaticMethod(mathType, "CalculateAnchoredPosition");

        Vector2 position = InvokeVector2(
            positionMethod,
            Vector2.zero,
            10f,
            30f,
            5.2f,
            false,
            false);

        Assert.That(position.x, Is.EqualTo(26f).Within(0.01f));
        Assert.That(position.y, Is.EqualTo(-45.033f).Within(0.01f));
    }

    [Test]
    public void BankDiamondRotatesAroundOverlayCenterInHorizonDirection()
    {
        Type mathType = GetRuntimeType("PFDHorizonMath");
        MethodInfo orbitMethod = GetStaticMethod(mathType, "RotatePointAroundCenter");

        Vector2 center = new Vector2(-35f, -26.7f);
        Vector2 position = InvokeVector2(
            orbitMethod,
            new Vector2(-35.2f, 85.8f),
            center,
            30f,
            false);

        Assert.That(position.x, Is.EqualTo(-91.423f).Within(0.01f));
        Assert.That(position.y, Is.EqualTo(70.628f).Within(0.01f));
    }

    [Test]
    public void ControllerDrivesGuideAndInactiveFinalBankDiamondsTogether()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            CreateHorizon(root.transform, "Guide_Horizon");
            CreateHorizon(root.transform, "Final_Horizon");
            RectTransform guideOverlay = CreateRect(root.transform, "Guide_HorizonOverlay", new Vector2(-35f, -26.7f));
            RectTransform finalOverlay = CreateRect(root.transform, "Final_HorizonOverlay", new Vector2(-35f, -26.7f));
            RectTransform guideDiamond = CreateRect(root.transform, "Guide_bank_diamond", new Vector2(-35.2f, 85.8f));
            RectTransform finalDiamond = CreateRect(root.transform, "Final_bank_diamond", new Vector2(-35.2f, 85.8f));
            finalOverlay.gameObject.SetActive(false);
            finalDiamond.gameObject.SetActive(false);

            Type controllerType = GetRuntimeType("PFDHorizonController");
            MethodInfo setAttitudeMethod = controllerType.GetMethod(
                "SetAttitude",
                BindingFlags.Public | BindingFlags.Instance);
            Component controller = root.AddComponent(controllerType);

            setAttitudeMethod.Invoke(controller, new object[] { 0f, 30f });

            Vector2 expectedPosition = new Vector2(-91.423f, 70.628f);
            Assert.That(guideDiamond.anchoredPosition.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
            Assert.That(guideDiamond.anchoredPosition.y, Is.EqualTo(expectedPosition.y).Within(0.01f));
            Assert.That(finalDiamond.anchoredPosition.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
            Assert.That(finalDiamond.anchoredPosition.y, Is.EqualTo(expectedPosition.y).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(30f, guideDiamond.localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(30f, finalDiamond.localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ControllerDrivesGuideAndInactiveFinalHorizonTogether()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            RectTransform guideHorizon = CreateHorizon(root.transform, "Guide_Horizon");
            RectTransform finalHorizon = CreateHorizon(root.transform, "Final_Horizon");
            finalHorizon.gameObject.SetActive(false);

            Type controllerType = GetRuntimeType("PFDHorizonController");
            MethodInfo setAttitudeMethod = controllerType.GetMethod(
                "SetAttitude",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(setAttitudeMethod, Is.Not.Null, "SetAttitude 公开实例方法不存在。");

            Component controller = root.AddComponent(controllerType);
            setAttitudeMethod.Invoke(controller, new object[] { 10f, 30f });

            Vector2 expectedPosition = new Vector2(-9f, -74.733f);
            Assert.That(guideHorizon.anchoredPosition.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
            Assert.That(guideHorizon.anchoredPosition.y, Is.EqualTo(expectedPosition.y).Within(0.01f));
            Assert.That(finalHorizon.anchoredPosition.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
            Assert.That(finalHorizon.anchoredPosition.y, Is.EqualTo(expectedPosition.y).Within(0.01f));
            Assert.That(finalHorizon.anchoredPosition, Is.EqualTo(guideHorizon.anchoredPosition));
            Assert.That(Mathf.DeltaAngle(-60f, guideHorizon.localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(-60f, finalHorizon.localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void AutomaticSimulatorStartsAtZeroAndReachesPositivePeak()
    {
        Type simulatorType = GetRuntimeType("PFDAttitudeSimulator");
        MethodInfo evaluateMethod = GetStaticMethod(simulatorType, "EvaluateAutomaticAttitude");

        Vector2 startAttitude = InvokeVector2(evaluateMethod, 0f, 20f, 4f, 30f, 4f);
        Vector2 peakAttitude = InvokeVector2(evaluateMethod, 1f, 20f, 4f, 30f, 4f);

        Assert.That(startAttitude.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(startAttitude.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(peakAttitude.x, Is.EqualTo(20f).Within(0.001f));
        Assert.That(peakAttitude.y, Is.EqualTo(30f).Within(0.001f));
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

    private static RectTransform CreateHorizon(Transform parent, string name)
    {
        GameObject horizonObject = new GameObject(name, typeof(RectTransform));
        RectTransform horizon = horizonObject.GetComponent<RectTransform>();
        horizon.SetParent(parent, false);
        horizon.anchoredPosition = new Vector2(-35f, -29.7f);
        horizon.localEulerAngles = new Vector3(0f, 0f, -90f);
        return horizon;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchoredPosition)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private static Vector2 InvokeVector2(MethodInfo method, params object[] arguments)
    {
        return (Vector2)method.Invoke(null, arguments);
    }

    private static float InvokeFloat(MethodInfo method, params object[] arguments)
    {
        return (float)method.Invoke(null, arguments);
    }
}

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PFDVerticalSpeedIndicatorTests
{
    [Test]
    public void VerticalSpeedMapsContinuouslyBetweenMajorTicks()
    {
        Type mathType = GetRuntimeType("PFDVerticalSpeedIndicatorMath");
        MethodInfo method = GetStaticMethod(mathType, "CalculateScaleY");

        float yAt2800Fpm = InvokeFloat(method, 2800f, 68f, 118f, 156f, 182f, 18f, -20f, -46f);

        Assert.That(yAt2800Fpm, Is.EqualTo(161.2f).Within(0.001f));
    }

    [Test]
    public void VerticalSpeedIsClampedToTopAndBottomScaleBounds()
    {
        Type mathType = GetRuntimeType("PFDVerticalSpeedIndicatorMath");
        MethodInfo method = GetStaticMethod(mathType, "CalculateScaleY");

        float top = InvokeFloat(method, 9000f, 68f, 118f, 156f, 182f, 18f, -20f, -46f);
        float bottom = InvokeFloat(method, -9000f, 68f, 118f, 156f, 182f, 18f, -20f, -46f);

        Assert.That(top, Is.EqualTo(182f).Within(0.001f));
        Assert.That(bottom, Is.EqualTo(-46f).Within(0.001f));
    }

    [Test]
    public void PointerUsesScaleLineAsItsLeftEndpoint()
    {
        Type mathType = GetRuntimeType("PFDVerticalSpeedIndicatorMath");
        MethodInfo centerMethod = GetStaticMethod(mathType, "CalculateLineCenter");
        MethodInfo lengthMethod = GetStaticMethod(mathType, "CalculateLineLength");

        Vector2 origin = new Vector2(188f, 68f);
        Vector2 scaleEndpoint = new Vector2(132f, 161.2f);
        Vector2 center = InvokeVector2(centerMethod, origin, scaleEndpoint);
        float length = InvokeFloat(lengthMethod, origin, scaleEndpoint);

        Assert.That(center.x, Is.EqualTo(160f).Within(0.001f));
        Assert.That(center.y, Is.EqualTo(114.6f).Within(0.001f));
        Assert.That(length, Is.EqualTo(108.73f).Within(0.01f));
    }

    [Test]
    public void ControllerDrivesGuideAndInactiveFinalPointerAndValueTogether()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            RectTransform guidePointer = CreateIndicator(root.transform, "Guide", out Text guideValue);
            RectTransform finalPointer = CreateIndicator(root.transform, "Final", out Text finalValue);
            finalPointer.parent.gameObject.SetActive(false);

            Type controllerType = GetRuntimeType("PFDVerticalSpeedIndicatorController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setMethod = controllerType.GetMethod(
                "SetVerticalSpeedFpm",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(setMethod, Is.Not.Null, "SetVerticalSpeedFpm 公开实例方法不存在。");
            setMethod.Invoke(controller, new object[] { 2800f });

            Assert.That(guidePointer.anchoredPosition.x, Is.EqualTo(160f).Within(0.001f));
            Assert.That(guidePointer.anchoredPosition.y, Is.EqualTo(114.6f).Within(0.001f));
            Assert.That(finalPointer.anchoredPosition, Is.EqualTo(guidePointer.anchoredPosition));
            Assert.That(guideValue.text, Is.EqualTo("2800"));
            Assert.That(finalValue.text, Is.EqualTo("2800"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ControllerHidesGuideAndFinalValuesWhenThereIsNoDataInput()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            CreateIndicator(root.transform, "Guide", out Text guideValue);
            CreateIndicator(root.transform, "Final", out Text finalValue);
            guideValue.text = "未初始化";
            finalValue.text = "未初始化";

            Type controllerType = GetRuntimeType("PFDVerticalSpeedIndicatorController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo onEnableMethod = controllerType.GetMethod(
                "OnEnable",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(onEnableMethod, Is.Not.Null, "控制器缺少初始化显示的 OnEnable 方法。");
            onEnableMethod.Invoke(controller, null);

            Assert.That(guideValue.enabled, Is.False);
            Assert.That(finalValue.enabled, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ValueHidesBelowFiveHundredAndUsesDirectionSpecificRoundedReadout()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            CreateIndicator(root.transform, "Guide", out Text guideValue);
            CreateIndicator(root.transform, "Final", out Text finalValue);
            guideValue.rectTransform.anchoredPosition = new Vector2(138f, 212f);
            finalValue.rectTransform.anchoredPosition = new Vector2(138f, 212f);

            Type controllerType = GetRuntimeType("PFDVerticalSpeedIndicatorController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setMethod = controllerType.GetMethod(
                "SetVerticalSpeedFpm",
                BindingFlags.Public | BindingFlags.Instance);

            setMethod.Invoke(controller, new object[] { 499f });
            Assert.That(guideValue.enabled, Is.False);
            Assert.That(finalValue.enabled, Is.False);

            setMethod.Invoke(controller, new object[] { 2817f });
            Assert.That(guideValue.enabled, Is.True);
            Assert.That(guideValue.text, Is.EqualTo("2800"));
            Assert.That(guideValue.alignment, Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(guideValue.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(138f, 212f)));

            setMethod.Invoke(controller, new object[] { -2826f });
            Assert.That(finalValue.enabled, Is.True);
            Assert.That(finalValue.text, Is.EqualTo("2850"));
            Assert.That(finalValue.alignment, Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(finalValue.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(138f, -79f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ValueContinuesPastSixThousandWhilePointerStopsAndCapsAtNineThousandNineHundredNinetyNine()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            RectTransform guidePointer = CreateIndicator(root.transform, "Guide", out Text guideValue);
            CreateIndicator(root.transform, "Final", out Text finalValue);

            Type controllerType = GetRuntimeType("PFDVerticalSpeedIndicatorController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setMethod = controllerType.GetMethod(
                "SetVerticalSpeedFpm",
                BindingFlags.Public | BindingFlags.Instance);

            setMethod.Invoke(controller, new object[] { 7150f });
            Assert.That(guideValue.enabled, Is.True);
            Assert.That(guideValue.text, Is.EqualTo("7150"));
            Assert.That(finalValue.text, Is.EqualTo("7150"));
            Assert.That(guidePointer.anchoredPosition.y, Is.EqualTo(125f).Within(0.001f));

            setMethod.Invoke(controller, new object[] { 10000f });
            Assert.That(guideValue.text, Is.EqualTo("9999"));
            Assert.That(finalValue.text, Is.EqualTo("9999"));
            Assert.That(guidePointer.anchoredPosition.y, Is.EqualTo(125f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void FinalVerticalSpeedValueAllowsFourDigitTextWithoutTruncation()
    {
#if UNITY_EDITOR
        const string prefabPath = "Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Text value = FindText(root, "Final_VerticalSpeedValue");

            Assert.That(value, Is.Not.Null, "未找到 Final_VerticalSpeedValue。");
            Assert.That(value.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Overflow));
            Assert.That(value.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
#else
        Assert.Ignore("该测试需要在 Unity 编辑器模式运行。");
#endif
    }

    // 编辑器未播放时也不得预先显示垂直速度零值。
    [Test]
    public void VerticalSpeedValuesStartEmptySoZeroIsNotVisibleBeforePlayMode()
    {
#if UNITY_EDITOR
        const string prefabPath = "Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Text guideValue = FindText(root, "Guide_VerticalSpeedValue");
            Text finalValue = FindText(root, "Final_VerticalSpeedValue");

            Assert.That(guideValue, Is.Not.Null, "未找到 Guide_VerticalSpeedValue。");
            Assert.That(finalValue, Is.Not.Null, "未找到 Final_VerticalSpeedValue。");
            Assert.That(guideValue.text, Is.Empty);
            Assert.That(finalValue.text, Is.Empty);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
#else
        Assert.Ignore("该测试需要在 Unity 编辑器模式运行。");
#endif
    }

    [Test]
    public void SimulatorHasNoSixThousandLimitAndDefaultsPastPointerScale()
    {
        Type simulatorType = GetRuntimeType("PFDVerticalSpeedIndicatorSimulator");
        FieldInfo manualSpeedField = simulatorType.GetField(
            "simulatedVerticalSpeedFpm",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo maximumSpeedField = simulatorType.GetField(
            "automaticMaximumFpm",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(manualSpeedField, Is.Not.Null);
        Assert.That(maximumSpeedField, Is.Not.Null);
        Assert.That(manualSpeedField.GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false), Is.Empty);
        Assert.That(maximumSpeedField.GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false), Is.Empty);

        GameObject simulatorObject = new GameObject("VerticalSpeedSimulator");

        try
        {
            Component simulator = simulatorObject.AddComponent(simulatorType);
            float defaultMaximumFpm = (float)maximumSpeedField.GetValue(simulator);

            Assert.That(defaultMaximumFpm, Is.GreaterThan(6000f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(simulatorObject);
        }
    }

    [Test]
    public void AutomaticSimulatorStartsAtZeroAndReachesPositiveMaximumQuarterCycle()
    {
        Type simulatorType = GetRuntimeType("PFDVerticalSpeedIndicatorSimulator");
        MethodInfo method = GetStaticMethod(simulatorType, "EvaluateAutomaticVerticalSpeed");

        float start = InvokeFloat(method, 0f, 6000f, 24f);
        float maximum = InvokeFloat(method, 6f, 6000f, 24f);
        float minimum = InvokeFloat(method, 18f, 6000f, 24f);

        Assert.That(start, Is.EqualTo(0f).Within(0.001f));
        Assert.That(maximum, Is.EqualTo(6000f).Within(0.001f));
        Assert.That(minimum, Is.EqualTo(-6000f).Within(0.001f));
    }

    private static RectTransform CreateIndicator(Transform parent, string prefix, out Text valueText)
    {
        GameObject indicatorObject = new GameObject(prefix + "_overlay2_1", typeof(RectTransform));
        RectTransform indicator = indicatorObject.GetComponent<RectTransform>();
        indicator.SetParent(parent, false);

        GameObject pointerObject = new GameObject(prefix + "_VerticalSpeedPointer", typeof(RectTransform));
        RectTransform pointer = pointerObject.GetComponent<RectTransform>();
        pointer.SetParent(indicator, false);

        GameObject valueObject = new GameObject(prefix + "_VerticalSpeedValue", typeof(RectTransform), typeof(Text));
        valueObject.transform.SetParent(indicator, false);
        valueText = valueObject.GetComponent<Text>();
        return pointer;
    }

    private static Text FindText(GameObject root, string name)
    {
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.name == name)
            {
                return text;
            }
        }

        return null;
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

    private static Vector2 InvokeVector2(MethodInfo method, params object[] arguments)
    {
        return (Vector2)method.Invoke(null, arguments);
    }
}

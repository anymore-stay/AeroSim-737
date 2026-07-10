using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PFDAltitudeTapeTests
{
    [Test]
    public void 高度会被限制在贴图范围内()
    {
        Type mathType = 获取运行时类型("PFDAltitudeTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "ClampAltitude");

        Assert.That((float)method.Invoke(null, new object[] { -2000f, -1000f, 50000f }), Is.EqualTo(-1000f));
        Assert.That((float)method.Invoke(null, new object[] { 51000f, -1000f, 50000f }), Is.EqualTo(50000f));
        Assert.That((float)method.Invoke(null, new object[] { 3600f, -1000f, 50000f }), Is.EqualTo(3600f));
    }

    [Test]
    public void 高度差会换算为内容层纵向位置()
    {
        Type mathType = 获取运行时类型("PFDAltitudeTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateContentY");

        float result = (float)method.Invoke(
            null,
            new object[] { 1000f, -1000f, 50000f, 0.44f, 0f, 12f, false });

        Assert.That(result, Is.EqualTo(452f).Within(0.001f));
    }

    [Test]
    public void 可以反转高度带滚动方向()
    {
        Type mathType = 获取运行时类型("PFDAltitudeTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateContentY");

        float result = (float)method.Invoke(
            null,
            new object[] { 1000f, -1000f, 50000f, 0.44f, 0f, 12f, true });

        Assert.That(result, Is.EqualTo(-428f).Within(0.001f));
    }

    [Test]
    public void 控制器会同步移动预览层和最终层()
    {
        GameObject root = new GameObject("PFD_Root");
        GameObject guideObject = new GameObject("Guide_AltitudeTapeContent", typeof(RectTransform));
        GameObject finalObject = new GameObject("Final_AltitudeTapeContent", typeof(RectTransform));

        try
        {
            guideObject.transform.SetParent(root.transform, false);
            finalObject.transform.SetParent(root.transform, false);

            Type controllerType = 获取运行时类型("PFDAltitudeTapeController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setAltitude = controllerType.GetMethod(
                "SetAltitude",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(setAltitude, Is.Not.Null, "SetAltitude 公开实例方法不存在。");

            setAltitude.Invoke(controller, new object[] { 1000f });

            RectTransform guide = guideObject.GetComponent<RectTransform>();
            RectTransform final = finalObject.GetComponent<RectTransform>();
            Assert.That(guide.anchoredPosition.y, Is.EqualTo(final.anchoredPosition.y).Within(0.001f));
            Assert.That(guide.anchoredPosition.y, Is.Not.EqualTo(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void 零高度会保留Prefab中校准好的内容层位置()
    {
        GameObject root = new GameObject("PFD_Root");
        GameObject guideObject = new GameObject("Guide_AltitudeTapeContent", typeof(RectTransform));
        GameObject finalObject = new GameObject("Final_AltitudeTapeContent", typeof(RectTransform));

        try
        {
            guideObject.transform.SetParent(root.transform, false);
            finalObject.transform.SetParent(root.transform, false);

            RectTransform guide = guideObject.GetComponent<RectTransform>();
            RectTransform final = finalObject.GetComponent<RectTransform>();
            guide.anchoredPosition = new Vector2(3f, -452.8f);
            final.anchoredPosition = new Vector2(-2f, -452.8f);

            Type controllerType = 获取运行时类型("PFDAltitudeTapeController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setAltitude = controllerType.GetMethod(
                "SetAltitude",
                BindingFlags.Public | BindingFlags.Instance);

            setAltitude.Invoke(controller, new object[] { 0f });

            Assert.That(guide.anchoredPosition.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(final.anchoredPosition.x, Is.EqualTo(-2f).Within(0.001f));
            Assert.That(guide.anchoredPosition.y, Is.EqualTo(-452.8f).Within(0.001f));
            Assert.That(final.anchoredPosition.y, Is.EqualTo(-452.8f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void 自动模拟会在一个周期内往返最低和最高高度()
    {
        Type simulatorType = 获取运行时类型("PFDAltitudeTapeSimulator");
        MethodInfo method = 获取公开静态方法(simulatorType, "EvaluateAutomaticAltitude");

        float start = (float)method.Invoke(null, new object[] { 0f, 0f, 10000f, 8f });
        float peak = (float)method.Invoke(null, new object[] { 4f, 0f, 10000f, 8f });
        float end = (float)method.Invoke(null, new object[] { 8f, 0f, 10000f, 8f });

        Assert.That(start, Is.EqualTo(0f).Within(0.001f));
        Assert.That(peak, Is.EqualTo(10000f).Within(0.001f));
        Assert.That(end, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void 模拟器启用时会自动切换到自动模式并从零高度开始()
    {
        GameObject root = new GameObject("PFD_Root");
        GameObject guideObject = new GameObject("Guide_AltitudeTapeContent", typeof(RectTransform));

        try
        {
            guideObject.transform.SetParent(root.transform, false);
            RectTransform guide = guideObject.GetComponent<RectTransform>();
            guide.anchoredPosition = new Vector2(0f, -452.8f);

            Type controllerType = 获取运行时类型("PFDAltitudeTapeController");
            root.AddComponent(controllerType);

            Type simulatorType = 获取运行时类型("PFDAltitudeTapeSimulator");
            Component simulator = root.AddComponent(simulatorType);
            MethodInfo onEnable = simulatorType.GetMethod(
                "OnEnable",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(onEnable, Is.Not.Null, "OnEnable 方法不存在。");

            onEnable.Invoke(simulator, null);

            FieldInfo modeField = simulatorType.GetField(
                "mode",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(modeField.GetValue(simulator).ToString(), Is.EqualTo("Automatic"));
            Assert.That(guide.anchoredPosition.y, Is.EqualTo(-452.8f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Type 获取运行时类型(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName + " 尚未实现或尚未编译。");
        return type;
    }

    private static MethodInfo 获取公开静态方法(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, methodName + " 公开静态方法不存在。");
        return method;
    }
}

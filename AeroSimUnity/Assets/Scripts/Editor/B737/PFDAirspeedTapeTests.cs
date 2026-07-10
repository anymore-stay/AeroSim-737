using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PFDAirspeedTapeTests
{
    [Test]
    public void 空速会被限制在贴图支持范围内()
    {
        Type mathType = 获取运行时类型("PFDAirspeedTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "ClampAirspeed");

        Assert.That((float)method.Invoke(null, new object[] { 0f, 40f, 440f }), Is.EqualTo(40f));
        Assert.That((float)method.Invoke(null, new object[] { 500f, 40f, 440f }), Is.EqualTo(440f));
        Assert.That((float)method.Invoke(null, new object[] { 160f, 40f, 440f }), Is.EqualTo(160f));
    }

    [Test]
    public void 空速差会换算为内容层纵向偏移()
    {
        Type mathType = 获取运行时类型("PFDAirspeedTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateContentOffsetY");

        float result = (float)method.Invoke(
            null,
            new object[] { 160f, 40f, 440f, 3.05f, 120f, true });

        Assert.That(result, Is.EqualTo(-122f).Within(0.001f));
    }

    [Test]
    public void 控制器会把Prefab校准位置作为四十节基准同步滚动()
    {
        GameObject root = new GameObject("PFD_Root");
        GameObject guideObject = new GameObject("Guide_AirSpeedTapeContent", typeof(RectTransform));
        GameObject finalObject = new GameObject("Final_AirSpeedTapeContent", typeof(RectTransform));

        try
        {
            guideObject.transform.SetParent(root.transform, false);
            finalObject.transform.SetParent(root.transform, false);

            RectTransform guide = guideObject.GetComponent<RectTransform>();
            RectTransform final = finalObject.GetComponent<RectTransform>();
            guide.anchoredPosition = new Vector2(2f, -52f);
            final.anchoredPosition = new Vector2(-3f, -52f);

            Type controllerType = 获取运行时类型("PFDAirspeedTapeController");
            Component controller = root.AddComponent(controllerType);
            MethodInfo setAirspeed = controllerType.GetMethod(
                "SetAirspeed",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(setAirspeed, Is.Not.Null, "SetAirspeed 公开实例方法不存在。");

            setAirspeed.Invoke(controller, new object[] { 40f });

            Assert.That(guide.anchoredPosition.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(final.anchoredPosition.x, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(guide.anchoredPosition.y, Is.EqualTo(-52f).Within(0.001f));
            Assert.That(final.anchoredPosition.y, Is.EqualTo(-52f).Within(0.001f));

            setAirspeed.Invoke(controller, new object[] { 160f });

            Assert.That(guide.anchoredPosition.y, Is.EqualTo(-418f).Within(0.001f));
            Assert.That(final.anchoredPosition.y, Is.EqualTo(-418f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void 自动模拟会在设定空速范围内往返()
    {
        Type simulatorType = 获取运行时类型("PFDAirspeedTapeSimulator");
        MethodInfo method = 获取公开静态方法(simulatorType, "EvaluateAutomaticAirspeed");

        float start = (float)method.Invoke(null, new object[] { 0f, 40f, 200f, 120f });
        float peak = (float)method.Invoke(null, new object[] { 60f, 40f, 200f, 120f });
        float end = (float)method.Invoke(null, new object[] { 120f, 40f, 200f, 120f });

        Assert.That(start, Is.EqualTo(40f).Within(0.001f));
        Assert.That(peak, Is.EqualTo(200f).Within(0.001f));
        Assert.That(end, Is.EqualTo(40f).Within(0.001f));
    }

    [Test]
    public void 自动模拟器启用时会从最低空速开始()
    {
        GameObject root = new GameObject("PFD_Root");
        GameObject guideObject = new GameObject("Guide_AirSpeedTapeContent", typeof(RectTransform));

        try
        {
            guideObject.transform.SetParent(root.transform, false);
            RectTransform guide = guideObject.GetComponent<RectTransform>();
            guide.anchoredPosition = new Vector2(0f, -52f);

            Type controllerType = 获取运行时类型("PFDAirspeedTapeController");
            root.AddComponent(controllerType);

            Type simulatorType = 获取运行时类型("PFDAirspeedTapeSimulator");
            Component simulator = root.AddComponent(simulatorType);
            MethodInfo onEnable = simulatorType.GetMethod(
                "OnEnable",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(onEnable, Is.Not.Null, "OnEnable 方法不存在。");

            onEnable.Invoke(simulator, null);

            Assert.That(guide.anchoredPosition.y, Is.EqualTo(-52f).Within(0.001f));
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

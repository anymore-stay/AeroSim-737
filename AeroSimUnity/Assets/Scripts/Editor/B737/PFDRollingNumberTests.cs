using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PFDRollingNumberTests
{
    [Test]
    public void 个位数字会随输入值连续滚动()
    {
        Type mathType = 获取运行时类型("PFDRollingNumberMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateDecimalWheelValue");

        float result = (float)method.Invoke(null, new object[] { 119.5f, 1f, 1f });

        Assert.That(result, Is.EqualTo(9.5f).Within(0.001f));
    }

    [Test]
    public void 十位数字只在个位接近进位时滚动()
    {
        Type mathType = 获取运行时类型("PFDRollingNumberMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateDecimalWheelValue");

        float stable = (float)method.Invoke(null, new object[] { 118f, 10f, 1f });
        float rolling = (float)method.Invoke(null, new object[] { 119.5f, 10f, 1f });

        Assert.That(stable, Is.EqualTo(1f).Within(0.001f));
        Assert.That(rolling, Is.EqualTo(1.5f).Within(0.001f));
    }

    [Test]
    public void 海拔最后两位按二十英尺五档循环()
    {
        Type mathType = 获取运行时类型("PFDRollingNumberMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateAltitudeTwoDigitWheelValue");

        Assert.That((float)method.Invoke(null, new object[] { 21100f }), Is.EqualTo(0f).Within(0.001f));
        Assert.That((float)method.Invoke(null, new object[] { 21180f }), Is.EqualTo(4f).Within(0.001f));
        Assert.That((float)method.Invoke(null, new object[] { 21190f }), Is.EqualTo(4.5f).Within(0.001f));
        Assert.That((float)method.Invoke(null, new object[] { 21200f }), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void 空速三位数字轮会同步显示并平滑进位()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            Type controllerType = 获取运行时类型("PFDRollingNumberController");
            Component controller = root.AddComponent(controllerType);
            RectTransform[] guide = 创建数字轮(root.transform, "Guide_IAS", 3);
            RectTransform[] final = 创建数字轮(root.transform, "Final_IAS", 3);
            设置私有字段(controller, "guideAirspeedWheels", guide);
            设置私有字段(controller, "finalAirspeedWheels", final);

            MethodInfo method = controllerType.GetMethod("SetAirspeed", BindingFlags.Public | BindingFlags.Instance);
            method.Invoke(controller, new object[] { 119.5f });

            float[] expected = { -24f, -36f, -228f };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(guide[i].anchoredPosition.y, Is.EqualTo(expected[i]).Within(0.001f));
                Assert.That(final[i].anchoredPosition.y, Is.EqualTo(expected[i]).Within(0.001f));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void 海拔前三位和末两位五档滚轮会同步显示()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            Type controllerType = 获取运行时类型("PFDRollingNumberController");
            Component controller = root.AddComponent(controllerType);
            RectTransform[] guideMain = 创建数字轮(root.transform, "Guide_ALT", 3);
            RectTransform[] finalMain = 创建数字轮(root.transform, "Final_ALT", 3);
            RectTransform guidePair = 创建数字轮(root.transform, "Guide_ALT_Pair", 1)[0];
            RectTransform finalPair = 创建数字轮(root.transform, "Final_ALT_Pair", 1)[0];
            设置私有字段(controller, "guideAltitudeMainWheels", guideMain);
            设置私有字段(controller, "finalAltitudeMainWheels", finalMain);
            设置私有字段(controller, "guideAltitudeTwoDigitWheel", guidePair);
            设置私有字段(controller, "finalAltitudeTwoDigitWheel", finalPair);

            MethodInfo method = controllerType.GetMethod("SetAltitude", BindingFlags.Public | BindingFlags.Instance);
            method.Invoke(controller, new object[] { 21190f });

            float[] expectedMain = { -48f, -24f, -36f };
            for (int i = 0; i < expectedMain.Length; i++)
            {
                Assert.That(guideMain[i].anchoredPosition.y, Is.EqualTo(expectedMain[i]).Within(0.001f));
                Assert.That(finalMain[i].anchoredPosition.y, Is.EqualTo(expectedMain[i]).Within(0.001f));
            }

            Assert.That(guidePair.anchoredPosition.y, Is.EqualTo(-108f).Within(0.001f));
            Assert.That(finalPair.anchoredPosition.y, Is.EqualTo(-108f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void 空速带控制器会同步驱动数字滚轮()
    {
        GameObject root = new GameObject("PFD_Root");
        GameObject tapeContent = new GameObject("Guide_AirSpeedTapeContent", typeof(RectTransform));

        try
        {
            tapeContent.transform.SetParent(root.transform, false);
            Type rollingType = 获取运行时类型("PFDRollingNumberController");
            Component rolling = root.AddComponent(rollingType);
            RectTransform[] wheels = 创建数字轮(root.transform, "IAS", 3);
            设置私有字段(rolling, "guideAirspeedWheels", wheels);

            Type tapeType = 获取运行时类型("PFDAirspeedTapeController");
            Component tape = root.AddComponent(tapeType);
            设置私有字段(tape, "rollingNumberController", rolling);
            MethodInfo method = tapeType.GetMethod("SetAirspeed", BindingFlags.Public | BindingFlags.Instance);
            method.Invoke(tape, new object[] { 119.5f });

            Assert.That(wheels[2].anchoredPosition.y, Is.EqualTo(-228f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void 高度带控制器会同步驱动五档数字滚轮()
    {
        GameObject root = new GameObject("PFD_Root");
        GameObject tapeContent = new GameObject("Guide_AltitudeTapeContent", typeof(RectTransform));

        try
        {
            tapeContent.transform.SetParent(root.transform, false);
            Type rollingType = 获取运行时类型("PFDRollingNumberController");
            Component rolling = root.AddComponent(rollingType);
            RectTransform pairWheel = 创建数字轮(root.transform, "ALT_Pair", 1)[0];
            设置私有字段(rolling, "guideAltitudeTwoDigitWheel", pairWheel);

            Type tapeType = 获取运行时类型("PFDAltitudeTapeController");
            Component tape = root.AddComponent(tapeType);
            设置私有字段(tape, "rollingNumberController", rolling);
            MethodInfo method = tapeType.GetMethod("SetAltitude", BindingFlags.Public | BindingFlags.Instance);
            method.Invoke(tape, new object[] { 21180f });

            Assert.That(pairWheel.anchoredPosition.y, Is.EqualTo(-96f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void 数字控制器会自动绑定重新生成的Final滚轮()
    {
        GameObject root = new GameObject("PFD_Root");

        try
        {
            RectTransform[] finalAirspeed =
            {
                创建命名数字轮(root.transform, "Final_AirspeedWheel_0"),
                创建命名数字轮(root.transform, "Final_AirspeedWheel_1"),
                创建命名数字轮(root.transform, "Final_AirspeedWheel_2")
            };
            RectTransform[] finalAltitude =
            {
                创建命名数字轮(root.transform, "Final_AltitudeWheel_0"),
                创建命名数字轮(root.transform, "Final_AltitudeWheel_1"),
                创建命名数字轮(root.transform, "Final_AltitudeWheel_2")
            };
            RectTransform finalPair = 创建命名数字轮(
                root.transform,
                "Final_AltitudeTwoDigitWheel");

            Type controllerType = 获取运行时类型("PFDRollingNumberController");
            Component controller = root.AddComponent(controllerType);
            controllerType.GetMethod("SetAirspeed").Invoke(controller, new object[] { 119.5f });
            controllerType.GetMethod("SetAltitude").Invoke(controller, new object[] { 21180f });

            Assert.That(finalAirspeed[2].anchoredPosition.y, Is.EqualTo(-228f).Within(0.001f));
            Assert.That(finalAltitude[0].anchoredPosition.y, Is.EqualTo(-48f).Within(0.001f));
            Assert.That(finalPair.anchoredPosition.y, Is.EqualTo(-96f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static RectTransform 创建命名数字轮(Transform parent, string name)
    {
        GameObject wheelObject = new GameObject(name, typeof(RectTransform));
        wheelObject.transform.SetParent(parent, false);
        return wheelObject.GetComponent<RectTransform>();
    }

    private static RectTransform[] 创建数字轮(Transform parent, string prefix, int count)
    {
        RectTransform[] result = new RectTransform[count];
        for (int i = 0; i < count; i++)
        {
            GameObject wheelObject = new GameObject(prefix + i, typeof(RectTransform));
            wheelObject.transform.SetParent(parent, false);
            result[i] = wheelObject.GetComponent<RectTransform>();
        }

        return result;
    }

    private static void 设置私有字段(Component component, string fieldName, object value)
    {
        FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName + " 字段不存在。");
        field.SetValue(component, value);
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

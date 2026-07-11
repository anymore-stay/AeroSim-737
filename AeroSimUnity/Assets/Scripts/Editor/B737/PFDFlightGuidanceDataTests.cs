using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PFDFlightGuidanceDataTests
{
    [Test]
    public void FlightGuidanceControllerShowsDefaultsAndFormatsAllFourDataSources()
    {
        GameObject root = new GameObject("Guide_FlightGuidanceData", typeof(RectTransform));

        try
        {
            Text targetSpeed = CreateText(root.transform, "Guide_TargetSpeedValue");
            Text altitudeLeadingDigit = CreateText(root.transform, "Guide_SelectedAltitudeLeadingDigit");
            Text altitudeRemainingDigits = CreateText(root.transform, "Guide_SelectedAltitudeRemainingDigits");
            Text heading = CreateText(root.transform, "Guide_SelectedHeadingValue");
            Text barometricPressure = CreateText(root.transform, "Guide_BarometricPressureValue");
            Text barometricUnit = CreateText(root.transform, "Guide_BarometricPressureUnit");

            Type controllerType = GetRuntimeType("PFDFlightGuidanceDataController");
            Component controller = root.AddComponent(controllerType);

            Assert.That(targetSpeed.text, Is.EqualTo("40"));
            Assert.That(altitudeLeadingDigit.text, Is.EqualTo("0"));
            Assert.That(altitudeRemainingDigits.text, Is.EqualTo("000"));
            Assert.That(heading.text, Is.EqualTo("000"));
            Assert.That(barometricPressure.text, Is.EqualTo("29.91"));
            Assert.That(barometricUnit.text, Is.EqualTo("IN."));

            Invoke(controllerType, controller, "SetTargetSpeedKnots", 145);
            Invoke(controllerType, controller, "SetSelectedAltitudeFeet", 12500);
            Invoke(controllerType, controller, "SetSelectedMagneticHeading", 182.6f);
            Invoke(controllerType, controller, "SetBarometricPressureInHg", 30.12f);

            Assert.That(targetSpeed.text, Is.EqualTo("145"));
            Assert.That(altitudeLeadingDigit.text, Is.EqualTo("1"));
            Assert.That(altitudeRemainingDigits.text, Is.EqualTo("2500"));
            Assert.That(heading.text, Is.EqualTo("183"));
            Assert.That(barometricPressure.text, Is.EqualTo("30.12"));
            Assert.That(barometricUnit.text, Is.EqualTo("IN."));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PreviewGuideContainsFlightGuidanceTextsWithLargeAltitudeLeadingDigit()
    {
#if UNITY_EDITOR
        const string prefabPath = "Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Transform previewGuide = FindTransform(root.transform, "PFD_PreviewGuide");
            Transform dataRoot = FindTransform(root.transform, "Guide_FlightGuidanceData");

            Assert.That(previewGuide, Is.Not.Null);
            Assert.That(dataRoot, Is.Not.Null);
            Assert.That(dataRoot.parent, Is.EqualTo(previewGuide));

            Text targetSpeed = FindText(dataRoot, "Guide_TargetSpeedValue");
            Text altitudeLeadingDigit = FindText(dataRoot, "Guide_SelectedAltitudeLeadingDigit");
            Text altitudeRemainingDigits = FindText(dataRoot, "Guide_SelectedAltitudeRemainingDigits");
            Text heading = FindText(dataRoot, "Guide_SelectedHeadingValue");
            Text barometricPressure = FindText(dataRoot, "Guide_BarometricPressureValue");
            Text barometricUnit = FindText(dataRoot, "Guide_BarometricPressureUnit");

            Assert.That(targetSpeed, Is.Not.Null);
            Assert.That(altitudeLeadingDigit, Is.Not.Null);
            Assert.That(altitudeRemainingDigits, Is.Not.Null);
            Assert.That(heading, Is.Not.Null);
            Assert.That(barometricPressure, Is.Not.Null);
            Assert.That(barometricUnit, Is.Not.Null);
            Assert.That(altitudeLeadingDigit.fontSize, Is.GreaterThan(altitudeRemainingDigits.fontSize));
            Assert.That(targetSpeed.color.r, Is.GreaterThan(targetSpeed.color.g));
            Assert.That(barometricPressure.color.g, Is.GreaterThan(barometricPressure.color.r));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
#else
        Assert.Ignore("该测试需要在 Unity 编辑器模式运行。");
#endif
    }

    private static Text CreateText(Transform parent, string name)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<Text>();
    }

    private static Transform FindTransform(Transform root, string name)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == name)
            {
                return transform;
            }
        }

        return null;
    }

    private static Text FindText(Transform root, string name)
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

    private static void Invoke(Type type, Component controller, string methodName, object argument)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, methodName + " 公开实例方法不存在。");
        method.Invoke(controller, new[] { argument });
    }
}

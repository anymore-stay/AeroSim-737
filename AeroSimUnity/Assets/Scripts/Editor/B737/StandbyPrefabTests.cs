using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class StandbyPrefabTests
{
    private const string PrefabPath =
        "Assets/Aircraft/B737/Instruments/Standby/Prefab/Standby.prefab";

    [Test]
    public void PrefabContainsRequiredViewportsAndMasks()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        string[] viewportNames =
        {
            "HorizonViewport",
            "SpeedTapeViewport",
            "AltitudeTapeViewport",
            "SpeedValueViewport",
            "AltitudeValueViewport"
        };

        foreach (string viewportName in viewportNames)
        {
            Transform viewport = FindDescendant(prefab.transform, viewportName);
            Assert.That(viewport, Is.Not.Null, viewportName + " 不存在");
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null, viewportName + " 缺少 RectMask2D");
        }
    }

    [Test]
    public void AltitudeSegmentsUseCorrectOverlapSpacing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform content = FindDescendant(prefab.transform, "AltitudeTapeContent");
        Assert.That(content, Is.Not.Null);
        Assert.That(content.childCount, Is.EqualTo(7));

        for (int i = 1; i < content.childCount; i++)
        {
            RectTransform previous = content.GetChild(i - 1) as RectTransform;
            RectTransform current = content.GetChild(i) as RectTransform;
            Assert.That(
                current.anchoredPosition.y - previous.anchoredPosition.y,
                Is.EqualTo(1960f).Within(0.001f));
        }
    }

    [Test]
    public void ControllerHasAllRequiredReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        StandbyDisplayController controller = prefab.GetComponent<StandbyDisplayController>();
        Assert.That(controller, Is.Not.Null);

        SerializedObject serialized = new SerializedObject(controller);
        AssertReference(serialized, "speedTapeContent");
        AssertReference(serialized, "altitudeTapeContent");
        AssertReference(serialized, "attitudeRollGroup");
        AssertReference(serialized, "horizonContent");
        AssertReference(serialized, "altitudePairWheel");
        Assert.That(serialized.FindProperty("airspeedDigitWheels").arraySize, Is.EqualTo(3));
        Assert.That(serialized.FindProperty("altitudeMainDigitWheels").arraySize, Is.EqualTo(3));
    }

    private static void AssertReference(SerializedObject serialized, string propertyName)
    {
        Assert.That(
            serialized.FindProperty(propertyName).objectReferenceValue,
            Is.Not.Null,
            propertyName + " 未绑定");
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant.name == objectName)
            {
                return descendant;
            }
        }

        return null;
    }
}

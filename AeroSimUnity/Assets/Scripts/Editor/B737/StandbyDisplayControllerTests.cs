using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class StandbyDisplayControllerTests
{
    private GameObject root;
    private StandbyDisplayController controller;
    private RectTransform speedTape;
    private RectTransform altitudeTape;
    private RectTransform horizon;
    private RawImage[] speedWheels;
    private RawImage[] altitudeWheels;
    private RawImage altitudePairWheel;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Standby控制器测试");
        controller = root.AddComponent<StandbyDisplayController>();
        speedTape = CreateRect("速度带");
        altitudeTape = CreateRect("高度带");
        horizon = CreateRect("地平线");
        speedWheels = CreateRawImages("空速", 3);
        altitudeWheels = CreateRawImages("高度", 3);
        altitudePairWheel = CreateRawImage("高度末两位");

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("speedTapeContent").objectReferenceValue = speedTape;
        serialized.FindProperty("altitudeTapeContent").objectReferenceValue = altitudeTape;
        serialized.FindProperty("horizonContent").objectReferenceValue = horizon;
        SetObjectArray(serialized.FindProperty("airspeedDigitWheels"), speedWheels);
        SetObjectArray(serialized.FindProperty("altitudeMainDigitWheels"), altitudeWheels);
        serialized.FindProperty("altitudePairWheel").objectReferenceValue = altitudePairWheel;
        serialized.FindProperty("speedReferenceKnots").floatValue = 40f;
        serialized.FindProperty("speedPixelsPerKnot").floatValue = 2.8f;
        serialized.FindProperty("altitudeReferenceFeet").floatValue = 0f;
        serialized.FindProperty("altitudePixelsPerFoot").floatValue = 0.28f;
        serialized.FindProperty("pitchPixelsPerDegree").floatValue = 4.8f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        controller.RebuildBasePose();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void SetAirspeedMovesTapeAndUpdatesAllThreeDigits()
    {
        controller.SetAirspeedKnots(145f);

        Assert.That(speedTape.anchoredPosition.y, Is.EqualTo(294f).Within(0.001f));
        Assert.That(speedWheels[0].uvRect.y, Is.Not.EqualTo(speedWheels[1].uvRect.y));
        Assert.That(speedWheels[1].uvRect.y, Is.Not.EqualTo(speedWheels[2].uvRect.y));
    }

    [Test]
    public void SetAltitudeMovesTapeAndUpdatesMainAndPairWheels()
    {
        controller.SetAltitudeFeet(1280f);

        Assert.That(altitudeTape.anchoredPosition.y, Is.EqualTo(358.4f).Within(0.001f));
        Rect expectedPairUv = StandbyDisplayMath.CalculateAltitudePairUv(4f, 37f, 136f);
        Assert.That(altitudePairWheel.uvRect.y, Is.EqualTo(expectedPairUv.y).Within(0.001f));
    }

    [Test]
    public void SetAttitudeMovesAndRotatesHorizon()
    {
        controller.SetAttitudeDegrees(10f, 20f);

        Vector2 expectedPosition = StandbyDisplayMath.CalculateHorizonPosition(
            Vector2.zero,
            10f,
            20f,
            4.8f,
            false,
            false);
        Assert.That(horizon.anchoredPosition.x, Is.EqualTo(expectedPosition.x).Within(0.001f));
        Assert.That(horizon.anchoredPosition.y, Is.EqualTo(expectedPosition.y).Within(0.001f));
        Assert.That(horizon.localEulerAngles.z, Is.EqualTo(20f).Within(0.001f));
    }

    private RectTransform CreateRect(string objectName)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(root.transform, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private RawImage CreateRawImage(string objectName)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        gameObject.transform.SetParent(root.transform, false);
        return gameObject.GetComponent<RawImage>();
    }

    private RawImage[] CreateRawImages(string prefix, int count)
    {
        RawImage[] result = new RawImage[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = CreateRawImage(prefix + i);
        }

        return result;
    }

    private static void SetObjectArray(SerializedProperty property, RawImage[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}

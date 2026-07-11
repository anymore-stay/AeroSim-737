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
    private RectTransform bankPointerGroup;
    private RectTransform headingRose;
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
        bankPointerGroup = CreateRect("滚转指针组");
        headingRose = CreateRect("航向盘");
        speedWheels = CreateRawImages("空速", 3);
        altitudeWheels = CreateRawImages("高度", 3);
        altitudePairWheel = CreateRawImage("高度末两位");

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("speedTapeContent").objectReferenceValue = speedTape;
        serialized.FindProperty("altitudeTapeContent").objectReferenceValue = altitudeTape;
        serialized.FindProperty("horizonContent").objectReferenceValue = horizon;
        serialized.FindProperty("bankPointerGroup").objectReferenceValue = bankPointerGroup;
        serialized.FindProperty("headingRose").objectReferenceValue = headingRose;
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
    public void SetAirspeedAlignsTapeOffsetToWholePixels()
    {
        controller.SetAirspeedKnots(40.4f);

        Assert.That(speedTape.anchoredPosition.y, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void AirspeedTapeStaysAtBaselineUntilSpeedExceedsFortyKnots()
    {
        controller.SetAirspeedKnots(0f);
        Assert.That(controller.AirspeedKnots, Is.EqualTo(0f));
        Assert.That(speedTape.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));

        controller.SetAirspeedKnots(40f);
        Assert.That(speedTape.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));

        controller.SetAirspeedKnots(41f);
        Assert.That(speedTape.anchoredPosition.y, Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void DemoDataSourceStartsAtZeroKnots()
    {
        GameObject dataSourceObject = new GameObject("Standby模拟数据测试");
        try
        {
            StandbyDemoDataSource dataSource = dataSourceObject.AddComponent<StandbyDemoDataSource>();
            SerializedObject serialized = new SerializedObject(dataSource);

            Assert.That(serialized.FindProperty("speedMinimumKnots").floatValue, Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(dataSourceObject);
        }
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
    public void ZeroAltitudeUsesCalibratedHundredsWheelUvOrigin()
    {
        controller.SetAltitudeFeet(0f);

        Assert.That(
            altitudeWheels[2].uvRect.y,
            Is.EqualTo(0.04881633f).Within(0.000001f));
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
        Assert.That(bankPointerGroup.localEulerAngles.z, Is.EqualTo(20f).Within(0.001f));
    }

    [Test]
    public void SetMagneticHeadingRotatesRoseCounterClockwise()
    {
        controller.SetMagneticHeadingDegrees(90f);

        Assert.That(Mathf.DeltaAngle(0f, headingRose.localEulerAngles.z), Is.EqualTo(-90f).Within(0.001f));
        Assert.That(controller.MagneticHeadingDegrees, Is.EqualTo(90f).Within(0.001f));
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

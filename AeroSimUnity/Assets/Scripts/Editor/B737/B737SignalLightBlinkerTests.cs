using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class B737SignalLightBlinkerTests
{
    [Test]
    public void DefaultBeaconCycleStaysWithinAnticollisionFlashRate()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        FieldInfo cycleField = blinkerType.GetField(
            "DefaultCycleSeconds",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(cycleField, Is.Not.Null);

        float flashesPerMinute = 60f / (float)cycleField.GetValue(null);
        Assert.That(flashesPerMinute, Is.InRange(40f, 100f));
        Assert.That(flashesPerMinute, Is.EqualTo(60f).Within(0.001f));
    }

    [Test]
    public void EvaluateBeaconIntensityUsesShortPulseInsteadOfSquareWave()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        MethodInfo method = blinkerType.GetMethod(
            "EvaluateBeaconIntensity",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);

        Assert.That((float)method.Invoke(null, new object[] { 0.03f, 1.00f, 0.11f, 0.02f }), Is.EqualTo(1f).Within(0.001f));
        Assert.That((float)method.Invoke(null, new object[] { 0.20f, 1.00f, 0.11f, 0.02f }), Is.EqualTo(0f).Within(0.001f));
        Assert.That((float)method.Invoke(null, new object[] { 1.03f, 1.00f, 0.11f, 0.02f }), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void DefaultPeakEmissionIsStrongEnoughForBeaconVisibility()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        FieldInfo emissionField = blinkerType.GetField(
            "DefaultEmissionIntensity",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(emissionField, Is.Not.Null);
        Assert.That((float)emissionField.GetValue(null), Is.GreaterThanOrEqualTo(12f));
    }

    [Test]
    public void DefaultAutoVisualIsSmallEnoughToAvoidCoveringBeaconLens()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        FieldInfo visualScaleField = blinkerType.GetField(
            "DefaultVisualScale",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(visualScaleField, Is.Not.Null);
        Assert.That((float)visualScaleField.GetValue(null), Is.LessThanOrEqualTo(0.18f));
    }

    [Test]
    public void VisualOverlayColorKeepsOriginalLensVisible()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        MethodInfo method = blinkerType.GetMethod(
            "EvaluateVisualOverlayColor",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);

        Color offColor = (Color)method.Invoke(null, new object[] { Color.red, 0f, 0.65f });
        Color onColor = (Color)method.Invoke(null, new object[] { Color.red, 1f, 0.65f });

        Assert.That(offColor.a, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(onColor.a, Is.EqualTo(0.65f).Within(0.0001f));
        Assert.That(onColor.a, Is.LessThan(1f));
    }

    [Test]
    public void PulseLightIntensityFollowsBeaconIntensity()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        MethodInfo method = blinkerType.GetMethod(
            "EvaluatePulseLightIntensity",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        Assert.That((float)method.Invoke(null, new object[] { 1f, 25f }), Is.EqualTo(25f).Within(0.001f));
        Assert.That((float)method.Invoke(null, new object[] { 0.25f, 25f }), Is.EqualTo(6.25f).Within(0.001f));
        Assert.That((float)method.Invoke(null, new object[] { 0f, 25f }), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void DefaultsDoNotTintSharedAircraftGlassOrCastSceneLight()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        Assert.That(GetPublicStaticBool(blinkerType, "DefaultAffectTargetRenderer"), Is.False);
        Assert.That(GetPublicStaticBool(blinkerType, "DefaultUsePulseLight"), Is.False);
        Assert.That(GetPublicStaticBool(blinkerType, "DefaultAutoCreateVisual"), Is.True);
    }

    [Test]
    public void AutoVisualUsesRendererLocalBoundsExtremesForPairedBeacons()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        MethodInfo method = blinkerType.GetMethod(
            "GetAutoVisualLocalPositions",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Bounds) },
            null);

        Assert.That(method, Is.Not.Null);

        Bounds localBounds = new Bounds(
            new Vector3(-0.05845f, -17.32705f, -0.03f),
            new Vector3(0.2313f, 2.3781f, 4.29f));

        Vector3[] results = (Vector3[])method.Invoke(null, new object[] { localBounds });

        Assert.That(results, Has.Length.EqualTo(2));
        Assert.That(results[0].x, Is.EqualTo(localBounds.center.x).Within(0.0001f));
        Assert.That(results[0].y, Is.EqualTo(localBounds.center.y).Within(0.0001f));
        Assert.That(results[0].z, Is.EqualTo(localBounds.min.z).Within(0.0001f));
        Assert.That(results[1].x, Is.EqualTo(localBounds.center.x).Within(0.0001f));
        Assert.That(results[1].y, Is.EqualTo(localBounds.center.y).Within(0.0001f));
        Assert.That(results[1].z, Is.EqualTo(localBounds.max.z).Within(0.0001f));
    }

    [Test]
    public void AutoVisualUsesEachBeaconClusterCenterWhenBeaconsAreOffset()
    {
        Type blinkerType = Type.GetType("B737SignalLightBlinker, Assembly-CSharp");
        Assert.That(blinkerType, Is.Not.Null);

        MethodInfo method = blinkerType.GetMethod(
            "GetAutoVisualLocalPositions",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Bounds), typeof(Vector3[]) },
            null);

        Assert.That(method, Is.Not.Null);

        Bounds localBounds = new Bounds(
            Vector3.zero,
            new Vector3(2f, 2f, 10f));

        Vector3[] vertices =
        {
            new Vector3(-0.8f, -0.4f, -5f),
            new Vector3(-0.4f, 0.2f, -4f),
            new Vector3(0.6f, -0.1f, 4f),
            new Vector3(1.0f, 0.5f, 5f)
        };

        Vector3[] results = (Vector3[])method.Invoke(null, new object[] { localBounds, vertices });

        Assert.That(results, Has.Length.EqualTo(2));
        Assert.That(results[0].x, Is.EqualTo(-0.6f).Within(0.0001f));
        Assert.That(results[0].y, Is.EqualTo(-0.1f).Within(0.0001f));
        Assert.That(results[0].z, Is.EqualTo(-4.5f).Within(0.0001f));
        Assert.That(results[1].x, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(results[1].y, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(results[1].z, Is.EqualTo(4.5f).Within(0.0001f));
    }

    private static bool GetPublicStaticBool(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);
        return (bool)field.GetValue(null);
    }
}

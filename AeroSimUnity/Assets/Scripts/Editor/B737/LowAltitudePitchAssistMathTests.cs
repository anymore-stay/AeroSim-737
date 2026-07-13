using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LowAltitudePitchAssistMathTests
{
    [Test]
    public void IsDisabledOnGround()
    {
        Assert.That(LowAltitudePitchAssistMath.CalculateBlend(0f, 20f, 1500f), Is.Zero);
    }

    [Test]
    public void FadesInAfterTakeoffThenWeakensWithAltitude()
    {
        float minimum = LowAltitudePitchAssistMath.CalculateBlend(20f, 20f, 1500f);
        float low = LowAltitudePitchAssistMath.CalculateBlend(120f, 20f, 1500f);
        float high = LowAltitudePitchAssistMath.CalculateBlend(1000f, 20f, 1500f);

        Assert.That(minimum, Is.Zero);
        Assert.That(low, Is.GreaterThan(high));
    }

    [Test]
    public void FadesOutAtCeiling()
    {
        Assert.That(LowAltitudePitchAssistMath.CalculateBlend(1500f, 20f, 1500f), Is.Zero);
    }

    [Test]
    public void ManualElevatorInputDisablesPitchAssist()
    {
        var gameObject = new GameObject("FlightInputPitchAssistTest");
        try
        {
            var bridge = gameObject.AddComponent<JsbsimBridge>();
            var input = gameObject.AddComponent<FlightInput>();
            SetField(input, "bridge", bridge);
            SetField(input, "elevator", -1f);
            SetAutoProperty(bridge, "HasState", true);
            SetAutoProperty(bridge, "PitchDeg", 15f);

            MethodInfo calculatePitchAssist = typeof(FlightInput).GetMethod(
                "CalculatePitchAssist",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(calculatePitchAssist, Is.Not.Null);

            float assist = (float)calculatePitchAssist.Invoke(input, null);
            Assert.That(assist, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    private static void SetAutoProperty(object target, string name, object value)
    {
        SetField(target, "<" + name + ">k__BackingField", value);
    }
}

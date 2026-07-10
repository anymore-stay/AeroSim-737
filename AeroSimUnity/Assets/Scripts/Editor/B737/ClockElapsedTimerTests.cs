using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ClockElapsedTimerTests
{
    [TestCase(99.0, 100.0, 0)]
    [TestCase(100.0, 100.0, 0)]
    [TestCase(100.999, 100.0, 0)]
    [TestCase(101.0, 100.0, 1)]
    [TestCase(161.75, 100.0, 61)]
    public void GetElapsedSecondsUsesWholeRealtimeSecondsSincePlayStarted(
        double realtimeNow,
        double playStartedAt,
        int expectedElapsedSeconds)
    {
        Type timerType = GetTimerType();
        MethodInfo method = timerType.GetMethod(
            "GetElapsedSeconds",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(double), typeof(double) },
            null);

        Assert.That(method, Is.Not.Null);
        Assert.That(
            (int)method.Invoke(null, new object[] { realtimeNow, playStartedAt }),
            Is.EqualTo(expectedElapsedSeconds));
    }

    [TestCase(-1, 0, 0, 0)]
    [TestCase(0, 0, 0, 0)]
    [TestCase(59, 0, 0, 0)]
    [TestCase(60, 0, 0, 1)]
    [TestCase(3599, 0, 5, 9)]
    [TestCase(3600, 1, 0, 0)]
    [TestCase(35940, 9, 5, 9)]
    [TestCase(36000, 9, 5, 9)]
    public void GetDisplayDigitsFormatsHoursAndMinutesAndHoldsAtNineFiftyNine(
        int elapsedSeconds,
        int expectedHours,
        int expectedTensMinutes,
        int expectedOnesMinutes)
    {
        Type timerType = GetTimerType();
        MethodInfo method = timerType.GetMethod(
            "GetDisplayDigits",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);

        int[] digits = (int[])method.Invoke(null, new object[] { elapsedSeconds });

        Assert.That(digits, Is.EqualTo(new[]
        {
            expectedHours,
            expectedTensMinutes,
            expectedOnesMinutes
        }));
    }

    [TestCase(0, 5, 0f, 0f)]
    [TestCase(7, 5, 0.4375f, 0f)]
    [TestCase(9, 2, 0.5625f, 0.5f)]
    public void GetDigitUvRectSelectsTheExpectedAtlasCell(
        int digit,
        int row,
        float expectedX,
        float expectedY)
    {
        Type timerType = GetTimerType();
        MethodInfo method = timerType.GetMethod(
            "GetDigitUvRect",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);

        Rect uv = (Rect)method.Invoke(null, new object[] { digit, row });

        Assert.That(uv.x, Is.EqualTo(expectedX).Within(0.000001f));
        Assert.That(uv.y, Is.EqualTo(expectedY).Within(0.000001f));
        Assert.That(uv.width, Is.EqualTo(1f / 16f).Within(0.000001f));
        Assert.That(uv.height, Is.EqualTo(1f / 6f).Within(0.000001f));
    }

    [Test]
    public void GetStateUvRectSwitchesFromHoldToRun()
    {
        Type timerType = GetTimerType();
        MethodInfo method = timerType.GetMethod(
            "GetStateUvRect",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);

        Rect hold = (Rect)method.Invoke(null, new object[] { false });
        Rect run = (Rect)method.Invoke(null, new object[] { true });

        Assert.That(hold.x, Is.EqualTo(4f / 52f).Within(0.000001f));
        Assert.That(run.x, Is.EqualTo(28f / 52f).Within(0.000001f));
        Assert.That(run.x, Is.GreaterThan(hold.x));
    }

    private static Type GetTimerType()
    {
        Type timerType = Type.GetType("ClockElapsedTimer, Assembly-CSharp");
        Assert.That(timerType, Is.Not.Null);
        return timerType;
    }
}

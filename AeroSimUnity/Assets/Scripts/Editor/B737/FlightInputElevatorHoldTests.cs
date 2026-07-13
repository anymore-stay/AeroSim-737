using System.Reflection;
using NUnit.Framework;

public class FlightInputElevatorHoldTests
{
    [Test]
    public void ReleasedKeysKeepCurrentElevatorState()
    {
        float result = InvokeStepHeldAxis(0.42f, 0f, 0.8f, 1f);

        Assert.That(result, Is.EqualTo(0.42f).Within(0.0001f));
    }

    [Test]
    public void HeldKeyChangesElevatorFromCurrentState()
    {
        float result = InvokeStepHeldAxis(0.2f, -1f, 0.8f, 0.5f);

        Assert.That(result, Is.EqualTo(-0.2f).Within(0.0001f));
    }

    [TestCase(0.9f, 1f, 1f)]
    [TestCase(-0.9f, -1f, -1f)]
    public void HeldElevatorStateRemainsNormalized(float current, float input, float expected)
    {
        float result = InvokeStepHeldAxis(current, input, 0.8f, 1f);

        Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void ReleasedTrimKeysKeepCurrentTrimState()
    {
        float result = InvokeStepPitchTrim(0.35f, false, false, 0.25f, 1f);

        Assert.That(result, Is.EqualTo(0.35f).Within(0.0001f));
    }

    [Test]
    public void NoseUpAndNoseDownTrimUseOppositeDirections()
    {
        float noseUp = InvokeStepPitchTrim(0f, true, false, 0.25f, 1f);
        float noseDown = InvokeStepPitchTrim(0f, false, true, 0.25f, 1f);

        Assert.That(noseUp, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(noseDown, Is.EqualTo(-0.25f).Within(0.0001f));
    }

    [TestCase(0.9f, true, false, 1f)]
    [TestCase(-0.9f, false, true, -1f)]
    public void PitchTrimRemainsNormalized(float current, bool noseUp, bool noseDown, float expected)
    {
        float result = InvokeStepPitchTrim(current, noseUp, noseDown, 0.25f, 1f);

        Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
    }

    private static float InvokeStepHeldAxis(float current, float input, float rate, float deltaTime)
    {
        MethodInfo method = typeof(FlightInput).GetMethod(
            "StepHeldAxis",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        return (float)method.Invoke(null, new object[] { current, input, rate, deltaTime });
    }

    private static float InvokeStepPitchTrim(
        float current,
        bool noseUp,
        bool noseDown,
        float rate,
        float deltaTime)
    {
        MethodInfo method = typeof(FlightInput).GetMethod(
            "StepPitchTrim",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        return (float)method.Invoke(null, new object[] { current, noseUp, noseDown, rate, deltaTime });
    }
}

using NUnit.Framework;

public class ReverseThrustMathTests
{
    [Test]
    public void CombinedKeysMoveThrottleThroughIdleIntoReverse()
    {
        float fromForward = ReverseThrustMath.UpdateSignedThrottle(
            0.25f, true, true, true, 0.5f, 0.5f);
        float intoReverse = ReverseThrustMath.UpdateSignedThrottle(
            fromForward, true, true, true, 0.5f, 0.5f);

        Assert.That(fromForward, Is.Zero.Within(0.0001f));
        Assert.That(intoReverse, Is.EqualTo(-0.25f).Within(0.0001f));
    }

    [Test]
    public void CombinedKeysCannotEnterReverseInFlight()
    {
        float result = ReverseThrustMath.UpdateSignedThrottle(
            0f, true, true, false, 0.5f, 1f);

        Assert.That(result, Is.Zero);
    }

    [Test]
    public void ControlAloneReturnsReverseThrottleToIdle()
    {
        float result = ReverseThrustMath.UpdateSignedThrottle(
            -0.75f, false, true, true, 0.5f, 1f);

        Assert.That(result, Is.EqualTo(-0.25f).Within(0.0001f));
    }

    [Test]
    public void NegativeThrottleProducesReverseEngineCommands()
    {
        ReverseThrustMath.CalculateEngineCommands(
            -0.6f, true, 2f, out float throttle, out float angle);

        Assert.That(throttle, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(angle, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void AirborneReverseDemandProducesIdleForwardConfiguration()
    {
        ReverseThrustMath.CalculateEngineCommands(
            -0.6f, false, 2f, out float throttle, out float angle);

        Assert.That(throttle, Is.Zero);
        Assert.That(angle, Is.Zero);
    }
}

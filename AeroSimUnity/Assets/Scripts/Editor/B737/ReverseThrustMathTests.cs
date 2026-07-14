using NUnit.Framework;

public class ReverseThrustMathTests
{
    [Test]
    public void CombinedKeysImmediatelySelectFullReverse()
    {
        float result = ReverseThrustMath.UpdateSignedThrottle(
            0.25f, true, true, true, 0.5f, 0.5f);

        Assert.That(result, Is.EqualTo(-1f).Within(0.0001f));
    }

    [Test]
    public void CombinedKeysCannotEnterReverseInFlight()
    {
        float result = ReverseThrustMath.UpdateSignedThrottle(
            0f, true, true, false, 0.5f, 1f);

        Assert.That(result, Is.Zero);
    }

    [Test]
    public void ControlAloneKeepsFullReverse()
    {
        float result = ReverseThrustMath.UpdateSignedThrottle(
            -0.75f, false, true, true, 0.5f, 1f);

        Assert.That(result, Is.EqualTo(-1f).Within(0.0001f));
    }

    [Test]
    public void ShiftAloneReturnsReverseThrottleDirectlyToIdle()
    {
        float result = ReverseThrustMath.UpdateSignedThrottle(
            -1f, true, false, true, 0.5f, 0.1f);

        Assert.That(result, Is.Zero);
    }

    [Test]
    public void NegativeThrottleProducesReverseEngineCommands()
    {
        ReverseThrustMath.CalculateEngineCommands(
            -0.6f, true, 2f, out float throttle, out float angle);

        Assert.That(throttle, Is.EqualTo(1f).Within(0.0001f));
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

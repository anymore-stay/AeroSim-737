using NUnit.Framework;

public class B737StrobeLightsTests
{
    [Test]
    public void StrobePatternUsesTwoInstantPulses()
    {
        Assert.That(B737StrobeLights.EvaluatePulse(0.025f, 0.05f, 0.05f, 1f), Is.True);
        Assert.That(B737StrobeLights.EvaluatePulse(0.075f, 0.05f, 0.05f, 1f), Is.False);
        Assert.That(B737StrobeLights.EvaluatePulse(0.125f, 0.05f, 0.05f, 1f), Is.True);
        Assert.That(B737StrobeLights.EvaluatePulse(0.5f, 0.05f, 0.05f, 1f), Is.False);
        Assert.That(B737StrobeLights.EvaluatePulse(1.175f, 0.05f, 0.05f, 1f), Is.True);
    }

    [Test]
    public void StrobeModeCyclesThroughOffArmedOn()
    {
        Assert.That(B737StrobeLights.NextMode(B737StrobeLights.StrobeMode.Off), Is.EqualTo(B737StrobeLights.StrobeMode.Armed));
        Assert.That(B737StrobeLights.NextMode(B737StrobeLights.StrobeMode.Armed), Is.EqualTo(B737StrobeLights.StrobeMode.On));
        Assert.That(B737StrobeLights.NextMode(B737StrobeLights.StrobeMode.On), Is.EqualTo(B737StrobeLights.StrobeMode.Off));
    }

    [Test]
    public void FlightStateCanForceStrobesWhenMovingOrAirborne()
    {
        Assert.That(B737StrobeLights.ShouldForceFromFlightState(false, 100f, 100f, 10f, 5f), Is.False);
        Assert.That(B737StrobeLights.ShouldForceFromFlightState(true, 0f, 4.9f, 10f, 5f), Is.False);
        Assert.That(B737StrobeLights.ShouldForceFromFlightState(true, 0f, 5f, 10f, 5f), Is.True);
        Assert.That(B737StrobeLights.ShouldForceFromFlightState(true, 10f, 0f, 10f, 5f), Is.True);
    }
}

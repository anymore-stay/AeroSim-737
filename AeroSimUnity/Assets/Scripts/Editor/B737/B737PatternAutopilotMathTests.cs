using NUnit.Framework;
using UnityEngine;

public class B737PatternAutopilotMathTests
{
    [TestCase(-10f, 350f)]
    [TestCase(370f, 10f)]
    [TestCase(720f, 0f)]
    public void NormalizeHeading_WrapsIntoCompassRange(float input, float expected)
    {
        Assert.That(B737PatternAutopilotMath.NormalizeHeading(input), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void GetPatternCoordinates_UsesRunwayAlignedAxes()
    {
        Vector2 coordinates = B737PatternAutopilotMath.GetPatternCoordinates(
            100f,
            250f,
            100f,
            50f,
            90f);

        Assert.That(coordinates.x, Is.EqualTo(200f).Within(0.001f));
        Assert.That(coordinates.y, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void GetLineTrackHeading_CommandsBackTowardCenterline()
    {
        float heading = B737PatternAutopilotMath.GetLineTrackHeading(0f, 500f, 1000f, 25f);

        Assert.That(Mathf.DeltaAngle(0f, heading), Is.LessThan(0f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, heading)), Is.LessThanOrEqualTo(25f));
    }

    [Test]
    public void CalculateAileronCommand_UsesShortestHeadingDirectionAcrossNorth()
    {
        float command = B737PatternAutopilotMath.CalculateAileronCommand(
            350f,
            10f,
            0f,
            0f,
            0.75f,
            22f,
            0.04f,
            0.7f,
            0.28f);

        Assert.That(command, Is.GreaterThan(0f));
        Assert.That(command, Is.LessThanOrEqualTo(0.28f));
    }

    [Test]
    public void CalculateElevatorCommand_NoseUpTargetProducesNegativeJsbsimCommand()
    {
        float command = B737PatternAutopilotMath.CalculateElevatorCommand(
            6f,
            0f,
            0f,
            0.06f,
            1.1f,
            0.45f,
            0.25f);

        Assert.That(command, Is.LessThan(0f));
        Assert.That(command, Is.GreaterThanOrEqualTo(-0.45f));
    }

    [Test]
    public void GetDynamicTurnLeadM_RespectsConfiguredBounds()
    {
        float lead = B737PatternAutopilotMath.GetDynamicTurnLeadM(
            180f,
            22f,
            1.05f,
            700f,
            3500f);

        Assert.That(lead, Is.InRange(700f, 3500f));
    }
}

using NUnit.Framework;

public class SideSlipProtectionMathTests
{
    [Test]
    public void LeavesCommandUnchangedInsideSoftLimit()
    {
        float result = SideSlipProtectionMath.CalculateRudderCommand(
            -0.2f, 5f, 8f, 2f, 0.06f, 0.45f);

        Assert.That(result, Is.EqualTo(-0.2f).Within(0.0001f));
    }

    [Test]
    public void RemovesCommandThatWorsensSideSlipAtHardLimit()
    {
        float result = SideSlipProtectionMath.CalculateRudderCommand(
            -0.3f, 8f, 8f, 2f, 0.06f, 0.45f);

        Assert.That(result, Is.GreaterThanOrEqualTo(0f));
    }

    [TestCase(10f, 1f)]
    [TestCase(-10f, -1f)]
    public void AppliesCorrectionTowardCoordinatedFlight(float sideSlipDeg, float expectedSign)
    {
        float result = SideSlipProtectionMath.CalculateRudderCommand(
            0f, sideSlipDeg, 8f, 2f, 0.06f, 0.45f);

        Assert.That(result * expectedSign, Is.GreaterThan(0f));
    }

    [Test]
    public void IgnoresInvalidSideSlipData()
    {
        float result = SideSlipProtectionMath.CalculateRudderCommand(
            0.2f, float.NaN, 8f, 2f, 0.06f, 0.45f);

        Assert.That(result, Is.EqualTo(0.2f).Within(0.0001f));
    }
}

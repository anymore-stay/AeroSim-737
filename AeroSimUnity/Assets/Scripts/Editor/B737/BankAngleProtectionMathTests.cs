using NUnit.Framework;

public class BankAngleProtectionMathTests
{
    [Test]
    public void LeavesAileronUnchangedInsideSoftLimit()
    {
        float result = BankAngleProtectionMath.CalculateAileronCommand(
            0.4f, 45f, 65f, 10f, 0.05f, 0.65f);

        Assert.That(result, Is.EqualTo(0.4f).Within(0.0001f));
    }

    [TestCase(65f, 0.8f, -1f)]
    [TestCase(-65f, -0.8f, 1f)]
    public void ReplacesWorseningInputWithLevelingCommand(
        float bankAngleDeg,
        float pilotCommand,
        float expectedSign)
    {
        float result = BankAngleProtectionMath.CalculateAileronCommand(
            pilotCommand, bankAngleDeg, 65f, 10f, 0.05f, 0.65f);

        Assert.That(result * expectedSign, Is.GreaterThan(0f));
    }

    [TestCase(70f, 0.8f)]
    [TestCase(-70f, -0.8f)]
    public void NeverAllowsFurtherRollBeyondHardLimit(float bankAngleDeg, float pilotCommand)
    {
        float result = BankAngleProtectionMath.CalculateAileronCommand(
            pilotCommand, bankAngleDeg, 65f, 10f, 0.05f, 0.65f);

        Assert.That(result * bankAngleDeg, Is.LessThanOrEqualTo(0f));
    }
}

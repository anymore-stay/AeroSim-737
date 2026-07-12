using NUnit.Framework;

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
}

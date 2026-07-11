using NUnit.Framework;
using UnityEngine;

public class StandbyDisplayMathTests
{
    [Test]
    public void TapeOffsetUsesReferenceValueAndDirection()
    {
        Assert.That(
            StandbyDisplayMath.CalculateTapeOffset(100f, 40f, 4f, false),
            Is.EqualTo(240f).Within(0.001f));
        Assert.That(
            StandbyDisplayMath.CalculateTapeOffset(100f, 40f, 4f, true),
            Is.EqualTo(-240f).Within(0.001f));
    }

    [Test]
    public void HorizonPitchMovesAlongRolledAxis()
    {
        Vector2 result = StandbyDisplayMath.CalculateHorizonPosition(
            Vector2.zero,
            10f,
            0f,
            4.8f,
            false,
            false);

        Assert.That(result.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(-48f).Within(0.001f));
    }

    [Test]
    public void HorizonRollKeepsPitchOffsetAttachedToHorizon()
    {
        Vector2 result = StandbyDisplayMath.CalculateHorizonPosition(
            Vector2.zero,
            10f,
            90f,
            4.8f,
            false,
            false);

        Assert.That(result.x, Is.EqualTo(48f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(
            StandbyDisplayMath.CalculateHorizonRotation(5f, 30f, false),
            Is.EqualTo(35f).Within(0.001f));
    }

    [Test]
    public void DecimalWheelRollsOnlyNearHigherPlaceBoundary()
    {
        Assert.That(
            StandbyDisplayMath.CalculateDecimalWheelValue(245.5f, 100f, 1f),
            Is.EqualTo(2f).Within(0.001f));
        Assert.That(
            StandbyDisplayMath.CalculateDecimalWheelValue(299.5f, 100f, 1f),
            Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(
            StandbyDisplayMath.CalculateDecimalWheelValue(245.5f, 1f, 1f),
            Is.EqualTo(5.5f).Within(0.001f));
    }

    [Test]
    public void AltitudePairWheelUsesTwentyFootSteps()
    {
        Assert.That(
            StandbyDisplayMath.CalculateAltitudePairWheelValue(80f),
            Is.EqualTo(4f).Within(0.001f));
        Assert.That(
            StandbyDisplayMath.CalculateAltitudePairWheelValue(90f),
            Is.EqualTo(4.5f).Within(0.001f));
        Assert.That(
            StandbyDisplayMath.CalculateAltitudePairWheelValue(100f),
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void DigitStripUvCentersMatchPreparedTextureRows()
    {
        Rect digitUv = StandbyDisplayMath.CalculateDigitStripUv(4f, 1, 3, 39f, 294f);
        Assert.That(digitUv.x, Is.EqualTo(1f / 3f).Within(0.001f));
        Assert.That(digitUv.width, Is.EqualTo(1f / 3f).Within(0.001f));
        Assert.That(digitUv.height, Is.EqualTo(39f / 294f).Within(0.001f));

        Rect pairUv = StandbyDisplayMath.CalculateAltitudePairUv(4f, 37f, 136f);
        Assert.That(pairUv.height, Is.EqualTo(37f / 136f).Within(0.001f));
        Assert.That(pairUv.y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(pairUv.yMax, Is.LessThanOrEqualTo(1f));
    }
}

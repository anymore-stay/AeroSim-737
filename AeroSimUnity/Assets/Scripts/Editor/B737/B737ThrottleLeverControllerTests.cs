using NUnit.Framework;

public class B737ThrottleLeverControllerTests
{
    [Test]
    public void PositiveThrottleMapsLinearlyToNegativeXAngle()
    {
        Assert.That(B737ThrottleLeverController.CalculateLeverAngle(0f, 0f, -50f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(B737ThrottleLeverController.CalculateLeverAngle(0.5f, 0f, -50f), Is.EqualTo(-25f).Within(0.0001f));
        Assert.That(B737ThrottleLeverController.CalculateLeverAngle(1f, 0f, -50f), Is.EqualTo(-50f).Within(0.0001f));
    }

    [Test]
    public void SignedThrottleIsClampedToForwardRange()
    {
        Assert.That(B737ThrottleLeverController.CalculateLeverAngle(-0.4f, 0f, -50f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(B737ThrottleLeverController.CalculateLeverAngle(1.4f, 0f, -50f), Is.EqualTo(-50f).Within(0.0001f));
    }
}

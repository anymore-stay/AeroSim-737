using System.Reflection;
using NUnit.Framework;

public class ThrustmasterA320SidestickInputTests
{
    [TestCase(0.19f, 0.1f)]
    [TestCase(0.11f, 0.1f)]
    [TestCase(0.09f, 0f)]
    [TestCase(-0.19f, -0.1f)]
    [TestCase(-0.09f, 0f)]
    [TestCase(1f, 1f)]
    public void OutputIgnoresSecondDecimalPlace(float input, float expected)
    {
        float result = InvokeDiscardRemainder(input, 0.1f);

        Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
    }

    private static float InvokeDiscardRemainder(float value, float step)
    {
        MethodInfo method = typeof(ThrustmasterA320SidestickInput).GetMethod(
            "DiscardRemainder",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        return (float)method.Invoke(null, new object[] { value, step });
    }
}

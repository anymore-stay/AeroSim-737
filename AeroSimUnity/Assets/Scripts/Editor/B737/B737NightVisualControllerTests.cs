using NUnit.Framework;
using UnityEngine;

public class B737NightVisualControllerTests
{
    [TestCase(0f, 1f)]
    [TestCase(23f, 1f)]
    [TestCase(12f, 0f)]
    [TestCase(18.5f, 0f)]
    public void 夜晚混合值支持跨午夜时间段(float hour, float expectedBlend)
    {
        float blend = B737NightVisualController.CalculateNightBlend(hour, 19f, 5.5f, 0.75f);

        Assert.That(blend, Is.EqualTo(expectedBlend).Within(0.001f));
    }

    [Test]
    public void 夜晚开始时按过渡时间淡入()
    {
        float blend = B737NightVisualController.CalculateNightBlend(19.375f, 19f, 5.5f, 0.75f);

        Assert.That(blend, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void 夜晚结束前按过渡时间淡出()
    {
        float blend = B737NightVisualController.CalculateNightBlend(5.125f, 19f, 5.5f, 0.75f);

        Assert.That(blend, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void 世界颜色按亮度系数压暗但保留透明度()
    {
        Color original = new Color(0.5f, 0.25f, 0.1f, 0.6f);

        Color dimmed = B737NightVisualController.CalculateDimmedColor(original, 0.08f, 1f);

        Assert.That(dimmed.r, Is.EqualTo(0.04f).Within(0.0001f));
        Assert.That(dimmed.g, Is.EqualTo(0.02f).Within(0.0001f));
        Assert.That(dimmed.b, Is.EqualTo(0.008f).Within(0.0001f));
        Assert.That(dimmed.a, Is.EqualTo(0.6f).Within(0.0001f));
    }

    [Test]
    public void 月光强度在夜晚被限制到上限()
    {
        float intensity = B737NightVisualController.ClampNightLightIntensity(0.8f, 0.03f, 1f);

        Assert.That(intensity, Is.EqualTo(0.03f).Within(0.0001f));
    }

    [Test]
    public void 星星亮度可在夜晚增强()
    {
        Color result = B737NightVisualController.CalculateStarColor(new Color(0.4f, 0.4f, 0.4f, 0.5f), 1.35f, 1f);

        Assert.That(result.r, Is.EqualTo(0.54f).Within(0.0001f));
        Assert.That(result.a, Is.EqualTo(0.675f).Within(0.0001f));
    }

    [Test]
    public void 星星亮度不会基于上次脚本结果重复累乘()
    {
        Color source = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        Color applied = B737NightVisualController.CalculateStarColor(source, 1.35f, 1f);

        Color resolved = B737NightVisualController.ResolveStarSourceColor(applied, source, applied, true);
        Color nextApplied = B737NightVisualController.CalculateStarColor(resolved, 1.35f, 1f);

        Assert.That(resolved.r, Is.EqualTo(source.r).Within(0.0001f));
        Assert.That(nextApplied.r, Is.EqualTo(applied.r).Within(0.0001f));
    }

    [Test]
    public void 星星颜色被UniStorm刷新后使用新的来源色()
    {
        Color previousSource = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        Color previousApplied = B737NightVisualController.CalculateStarColor(previousSource, 1.35f, 1f);
        Color refreshedColor = new Color(0.2f, 0.25f, 0.3f, 0.5f);

        Color resolved = B737NightVisualController.ResolveStarSourceColor(refreshedColor, previousSource, previousApplied, true);

        Assert.That(resolved.r, Is.EqualTo(refreshedColor.r).Within(0.0001f));
        Assert.That(resolved.g, Is.EqualTo(refreshedColor.g).Within(0.0001f));
    }
}

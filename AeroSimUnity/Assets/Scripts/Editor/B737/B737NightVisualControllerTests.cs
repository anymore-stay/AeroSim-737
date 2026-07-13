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
    public void 实际夜晚视觉在太阳落山前开始过渡()
    {
        Assert.That(B737NightVisualController.CalculateNightVisualBlend(16.5f, 17f, 19f, 5.5f, 0.75f), Is.EqualTo(0f).Within(0.001f));
        Assert.That(B737NightVisualController.CalculateNightVisualBlend(18f, 17f, 19f, 5.5f, 0.75f), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(B737NightVisualController.CalculateNightVisualBlend(19f, 17f, 19f, 5.5f, 0.75f), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void 机场亮度在傍晚过渡中逐步接近目标值()
    {
        float blend = B737NightVisualController.CalculateNightVisualBlend(18f, 17f, 19f, 5.5f, 0.75f);
        float brightness = B737NightVisualController.CalculateSurfaceBrightness(blend, 0.8f);

        Assert.That(brightness, Is.EqualTo(0.9f).Within(0.001f));
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
    public void 地景亮度按昼夜混合到目标值()
    {
        Assert.That(B737NightVisualController.CalculateSurfaceBrightness(0f, 0.22f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(B737NightVisualController.CalculateSurfaceBrightness(0.5f, 0.22f), Is.EqualTo(0.61f).Within(0.0001f));
        Assert.That(B737NightVisualController.CalculateSurfaceBrightness(1f, 0.22f), Is.EqualTo(0.22f).Within(0.0001f));
    }

    [Test]
    public void 机场亮度过渡不会低于夜晚目标值()
    {
        float targetBrightness = 0.126f;
        float previous = 1f;

        for (int i = 0; i <= 10; i++)
        {
            float blend = i / 10f;
            float brightness = B737NightVisualController.CalculateSurfaceBrightness(blend, targetBrightness);

            Assert.That(brightness + 0.0001f, Is.GreaterThanOrEqualTo(targetBrightness));
            Assert.That(brightness - 0.0001f, Is.LessThanOrEqualTo(previous));
            previous = brightness;
        }
    }

    [Test]
    public void 夜间环境光过渡基于原始白天值()
    {
        float firstFrame = B737NightVisualController.CalculateNightEnvironmentValue(1f, 0.015f, 0.25f);
        float nextFrame = B737NightVisualController.CalculateNightEnvironmentValue(1f, 0.015f, 0.25f);

        Assert.That(firstFrame, Is.EqualTo(0.75375f).Within(0.0001f));
        Assert.That(nextFrame, Is.EqualTo(firstFrame).Within(0.0001f));
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

    [Test]
    public void 夜晚到达扫描时间才扫描世界Renderer()
    {
        bool tooEarly = B737NightVisualController.ShouldScanWorldRenderers(0.5f, 2f);
        bool due = B737NightVisualController.ShouldScanWorldRenderers(2f, 2f);

        Assert.That(tooEarly, Is.False);
        Assert.That(due, Is.True);
    }

    [Test]
    public void 白天仍按间隔刷新世界Renderer()
    {
        bool tooEarly = B737NightVisualController.ShouldScanWorldRenderers(0.5f, 2f);
        bool due = B737NightVisualController.ShouldScanWorldRenderers(2f, 2f);

        Assert.That(tooEarly, Is.False);
        Assert.That(due, Is.True);
    }

    [Test]
    public void 夜晚世界Renderer扫描保持低频兜底()
    {
        float nightInterval = B737NightVisualController.GetNextWorldRendererScanInterval(1f, 1f, 2f);
        float dayInterval = B737NightVisualController.GetNextWorldRendererScanInterval(0f, 1f, 2f);

        Assert.That(nightInterval, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(dayInterval, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Cesium亮度按原始颜色压暗并保留透明度()
    {
        Color original = new Color(0.6f, 0.4f, 0.2f, 0.75f);

        Color result = B737NightVisualController.CalculateCesiumRuntimeMaterialColor(original, 0.08f);

        Assert.That(result.r, Is.EqualTo(0.048f).Within(0.0001f));
        Assert.That(result.g, Is.EqualTo(0.032f).Within(0.0001f));
        Assert.That(result.b, Is.EqualTo(0.016f).Within(0.0001f));
        Assert.That(result.a, Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void Cesium不使用会破坏贴图链路的运行时材质切换()
    {
        Assert.That(B737NightVisualController.ShouldUseCesiumRuntimeMaterial(0f), Is.False);
        Assert.That(B737NightVisualController.ShouldUseCesiumRuntimeMaterial(0.01f), Is.False);
        Assert.That(B737NightVisualController.ShouldUseCesiumRuntimeMaterial(1f), Is.False);
    }

    [Test]
    public void 亮度变化足够明显才批量刷新Renderer()
    {
        Assert.That(B737NightVisualController.ShouldApplyBrightnessChange(-1f, 1f), Is.True);
        Assert.That(B737NightVisualController.ShouldApplyBrightnessChange(0.22f, 0.223f), Is.False);
        Assert.That(B737NightVisualController.ShouldApplyBrightnessChange(0.22f, 0.225f), Is.True);
    }

    [Test]
    public void 夜间航迹云被压暗并降低透明度()
    {
        Color result = B737ContrailController.CalculateNightSmokeColor(
            new Color(1f, 1f, 1f, 0.8f),
            new Color(0.04f, 0.045f, 0.05f, 1f),
            0.08f,
            0.18f,
            1f);

        Assert.That(result.r, Is.EqualTo(0.0032f).Within(0.0001f));
        Assert.That(result.g, Is.EqualTo(0.0036f).Within(0.0001f));
        Assert.That(result.b, Is.EqualTo(0.004f).Within(0.0001f));
        Assert.That(result.a, Is.EqualTo(0.144f).Within(0.0001f));
    }

    [Test]
    public void 白天航迹云保持原始颜色()
    {
        Color source = new Color(0.9f, 0.85f, 0.8f, 0.7f);

        Color result = B737ContrailController.CalculateNightSmokeColor(
            source,
            new Color(0.04f, 0.045f, 0.05f, 1f),
            0.08f,
            0.18f,
            0f);

        Assert.That(result.r, Is.EqualTo(source.r).Within(0.0001f));
        Assert.That(result.g, Is.EqualTo(source.g).Within(0.0001f));
        Assert.That(result.b, Is.EqualTo(source.b).Within(0.0001f));
        Assert.That(result.a, Is.EqualTo(source.a).Within(0.0001f));
    }
}

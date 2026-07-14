using System.Linq;
using NUnit.Framework;

public class AeroSimStartMenuControllerTests
{
    [Test]
    public void 默认画质选项包含4K最高画质()
    {
        AeroSimStartMenuController.GraphicsPreset[] presets =
            AeroSimStartMenuController.CreateDefaultPresets();

        AeroSimStartMenuController.GraphicsPreset preset4K = presets.Last();

        Assert.That(presets, Has.Length.EqualTo(3));
        Assert.That(preset4K.Width, Is.EqualTo(3840));
        Assert.That(preset4K.Height, Is.EqualTo(2160));
        Assert.That(preset4K.QualityName, Is.EqualTo("High Fidelity"));
        Assert.That(preset4K.Label, Does.Contain("4K"));
    }

    [Test]
    public void 找不到指定画质名称时回退到最高画质档位()
    {
        int qualityIndex = AeroSimStartMenuController.FindQualityIndex(
            "不存在的画质",
            new[] { "Performant", "Balanced", "High Fidelity" });

        Assert.That(qualityIndex, Is.EqualTo(2));
    }
}

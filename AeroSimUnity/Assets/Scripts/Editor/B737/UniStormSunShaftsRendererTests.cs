using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

public class UniStormSunShaftsRendererTests
{
    [Test]
    public void 太阳光轴最终画面使用高分辨率()
    {
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
            "Assets/Settings/URP-HighFidelity-Renderer.asset");
        UniStormSunShaftsFeature sunShafts = rendererData.rendererFeatures
            .OfType<UniStormSunShaftsFeature>()
            .First(feature => feature.name.StartsWith("Sun"));

        Assert.That(
            sunShafts.settings.resolution,
            Is.EqualTo(UniStormSunShaftsFeature.SunShaftsResolution.High));
    }
}

using System.IO;
using NUnit.Framework;

public class FpsCounterOverlayTests
{
    [Test]
    public void 默认不显示FPS计数器()
    {
        string source = File.ReadAllText("Assets/Scripts/World/FpsCounterOverlay.cs");

        Assert.That(source, Does.Contain("[SerializeField] private bool showFps;"));
    }

    [Test]
    public void 按下切换键时反转显示状态()
    {
        Assert.That(FpsCounterOverlay.ResolveVisibilityAfterToggle(false, true), Is.True);
        Assert.That(FpsCounterOverlay.ResolveVisibilityAfterToggle(true, true), Is.False);
        Assert.That(FpsCounterOverlay.ResolveVisibilityAfterToggle(true, false), Is.True);
    }
}

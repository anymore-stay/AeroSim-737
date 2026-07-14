using NUnit.Framework;

public class B737NavigationDisplayRigTests
{
    [Test]
    public void 播放模式不自动创建ND显示平面()
    {
        bool shouldCreate = B737NavigationDisplayRig.ShouldCreateDisplayPlane(
            applicationIsPlaying: true,
            hasDisplayPlaneRenderer: false,
            allowDisplayPlaneCreation: true);

        Assert.That(shouldCreate, Is.False);
    }

    [Test]
    public void 编辑模式仍允许工具生成ND显示平面()
    {
        bool shouldCreate = B737NavigationDisplayRig.ShouldCreateDisplayPlane(
            applicationIsPlaying: false,
            hasDisplayPlaneRenderer: false,
            allowDisplayPlaneCreation: true);

        Assert.That(shouldCreate, Is.True);
    }
}

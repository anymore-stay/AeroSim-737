using NUnit.Framework;
using UnityEngine;

public class B737UniStormSunDiscAnchorTests
{
    [Test]
    public void 太阳盘位置跟随当前相机和太阳方向()
    {
        Vector3 cameraPosition = new Vector3(100f, 20f, -30f);
        Vector3 sunDirection = new Vector3(0f, 0.5f, 1f).normalized;

        Vector3 position = B737UniStormSunDiscAnchor.CalculateSunDiscPosition(
            cameraPosition,
            sunDirection,
            2000f);

        Assert.That(position, Is.EqualTo(cameraPosition + sunDirection * 2000f));
    }

    [Test]
    public void 太阳盘距离不会超过当前相机远裁剪面()
    {
        float closeCameraDistance = B737UniStormSunDiscAnchor.CalculateEffectiveDistance(
            2000f,
            1000f,
            0.9f);
        float farCameraDistance = B737UniStormSunDiscAnchor.CalculateEffectiveDistance(
            2000f,
            30000f,
            0.9f);

        Assert.That(closeCameraDistance, Is.EqualTo(900f).Within(0.001f));
        Assert.That(farCameraDistance, Is.EqualTo(2000f).Within(0.001f));
    }

    [Test]
    public void 太阳盘拉近时按距离比例缩放以保持角大小()
    {
        Vector3 scale = B737UniStormSunDiscAnchor.CalculateSunDiscScale(
            new Vector3(6f, 6f, 6f),
            900f,
            2000f);

        Assert.That(scale, Is.EqualTo(new Vector3(2.7f, 2.7f, 2.7f)));
    }
}

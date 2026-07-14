using NUnit.Framework;
using UnityEngine;

public class FlightMapTrackRenderingTests
{
    [Test]
    public void 无限航迹配置追加时不裁剪历史点()
    {
        int nextCount = FlightMapOverlay.ResolveTrackPointCountAfterAppend(
            currentCount: 100000,
            maxTrackPoints: 0);

        Assert.That(nextCount, Is.EqualTo(100001));
    }

    [Test]
    public void 有航迹上限时仍按上限保留点数()
    {
        int nextCount = FlightMapOverlay.ResolveTrackPointCountAfterAppend(
            currentCount: 900,
            maxTrackPoints: 900);

        Assert.That(nextCount, Is.EqualTo(900));
    }

    [Test]
    public void 超长航迹绘制保留首尾并限制顶点数量()
    {
        Vector2[] points = new Vector2[100000];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2(i, Mathf.Sin(i * 0.01f) * 20f);
        }

        const int maxRenderableSegments = 1200;
        Vector2[] sampled = FlightMapGraphic.SampleTrackForRendering(points, maxRenderableSegments);
        int vertexCount = FlightMapGraphic.EstimateTrackVertexCount(points.Length, maxRenderableSegments);

        Assert.That(sampled[0], Is.EqualTo(points[0]));
        Assert.That(sampled[sampled.Length - 1], Is.EqualTo(points[points.Length - 1]));
        Assert.That(sampled.Length - 1, Is.LessThanOrEqualTo(maxRenderableSegments));
        Assert.That(vertexCount, Is.LessThan(65000));
    }
}

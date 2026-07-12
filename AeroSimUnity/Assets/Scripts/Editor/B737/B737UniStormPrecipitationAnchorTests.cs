using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class B737UniStormPrecipitationAnchorTests
{
    private static Type GetAnchorType()
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("B737UniStormPrecipitationAnchor"))
            .FirstOrDefault(candidate => candidate != null);

        Assert.That(type, Is.Not.Null, "尚未创建 UniStorm 降水相机锚点");
        return type;
    }

    [Test]
    public void 降水锚点位于相机和飞机之间()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateAnchorPosition");
        Assert.That(method, Is.Not.Null, "尚未实现降水锚点位置计算");

        Vector3 anchor = (Vector3)method.Invoke(null, new object[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(100f, 20f, -40f)
        });

        Assert.That(anchor, Is.EqualTo(new Vector3(50f, 10f, -20f)));
    }

    [Test]
    public void 覆盖半径会包住相机和飞机并受上限约束()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateCoverageRadius");
        Assert.That(method, Is.Not.Null, "尚未实现降水覆盖半径计算");

        float normal = (float)method.Invoke(null, new object[] { 190f, 100f, 35f, 140f });
        float capped = (float)method.Invoke(null, new object[] { 300f, 100f, 35f, 140f });

        Assert.That(normal, Is.EqualTo(130f).Within(0.001f));
        Assert.That(capped, Is.EqualTo(140f).Within(0.001f));
    }

    [Test]
    public void 相机管理器公开当前激活相机()
    {
        PropertyInfo property = typeof(CameraManager).GetProperty("ActiveCamera");

        Assert.That(property, Is.Not.Null, "相机管理器未公开当前激活相机");
        Assert.That(property.PropertyType, Is.EqualTo(typeof(Camera)));
    }

    [Test]
    public void 近景降水锚点始终位于当前相机()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateNearAnchorPosition");
        Assert.That(method, Is.Not.Null, "尚未实现跟随相机的近景降水锚点");

        Vector3 cameraPosition = new Vector3(150f, 10f, -40f);
        Vector3 anchor = (Vector3)method.Invoke(null, new object[] { cameraPosition });

        Assert.That(anchor, Is.EqualTo(cameraPosition));
    }

    [Test]
    public void 远景降水半径同时覆盖相机与飞机()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDistantCoverageRadius");
        Assert.That(method, Is.Not.Null, "尚未实现远景降水覆盖范围计算");

        float normal = (float)method.Invoke(null, new object[] { 95f, 220f, 35f, 300f });
        float capped = (float)method.Invoke(null, new object[] { 400f, 220f, 35f, 300f });

        Assert.That(normal, Is.EqualTo(220f).Within(0.001f));
        Assert.That(capped, Is.EqualTo(300f).Within(0.001f));
    }

    [Test]
    public void 远景降水发射率按面积提高且受性能上限保护()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDistantEmissionRate");
        Assert.That(method, Is.Not.Null, "尚未实现远景降水粒子密度计算");

        float normal = (float)method.Invoke(null, new object[] { 3000f, 100f, 220f, 0.35f, 7000f });
        float capped = (float)method.Invoke(null, new object[] { 3000f, 25f, 250f, 0.35f, 7000f });

        Assert.That(normal, Is.EqualTo(5082f).Within(0.001f));
        Assert.That(capped, Is.EqualTo(7000f).Within(0.001f));
    }

    [Test]
    public void 远景降水锚点始终以相机为中心()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDistantAnchorPosition");
        Assert.That(method, Is.Not.Null, "尚未实现相机中心的远景降水锚点");

        Vector3 cameraPosition = new Vector3(95f, 12f, -60f);
        Vector3 sourceOffset = new Vector3(0f, 28f, 1f);
        Vector3 anchor = (Vector3)method.Invoke(null, new object[] { cameraPosition, sourceOffset });

        Assert.That(anchor, Is.EqualTo(cameraPosition + sourceOffset));
    }

    [Test]
    public void 近景发射率在粒子预算内提高密度()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateBudgetedEmissionRate");
        Assert.That(method, Is.Not.Null, "尚未实现受粒子预算约束的近景发射率计算");

        float rain = (float)method.Invoke(null, new object[] { 3000f, 2f, 1.3f, 10000 });
        float longLivedSnow = (float)method.Invoke(null, new object[] { 3000f, 2f, 3f, 10000 });

        Assert.That(rain, Is.EqualTo(6000f).Within(0.001f));
        Assert.That(longLivedSnow, Is.EqualTo(10000f / 3f).Within(0.001f));
    }

    [Test]
    public void 近景密集层允许小于预制体默认半径()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDenseNearRadius");
        Assert.That(method, Is.Not.Null, "尚未实现近景密集层半径计算");

        float radius = (float)method.Invoke(null, new object[] { 60f, 140f });

        Assert.That(radius, Is.EqualTo(60f).Within(0.001f));
    }

    [Test]
    public void 近景降水盒在世界水平面上对称覆盖()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateNearBoxSize");
        Assert.That(method, Is.Not.Null, "尚未实现近景世界轴对齐降水盒");

        Vector3 size = (Vector3)method.Invoke(null, new object[] { 60f, 100f });

        Assert.That(size, Is.EqualTo(new Vector3(120f, 100f, 120f)));
    }

    [Test]
    public void 雪的近景降水盒保持在相机附近()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateSnowBoxVerticalSettings");
        Assert.That(method, Is.Not.Null, "尚未实现雪的低空降水盒参数");

        Vector2 settings = (Vector2)method.Invoke(null, new object[] { 50f });

        Assert.That(settings.y, Is.EqualTo(30f).Within(0.001f));
        Assert.That(settings.x - settings.y * 0.5f, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void 降水发射盒补偿水平风漂移()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateWindCompensatedAnchorPosition");
        Assert.That(method, Is.Not.Null, "尚未实现降水发射盒的风漂移补偿");

        Vector3 anchor = (Vector3)method.Invoke(null, new object[]
        {
            new Vector3(10f, 20f, 30f),
            new Vector3(0f, -100f, -20f),
            1.3f
        });

        Assert.That(anchor, Is.EqualTo(new Vector3(10f, 20f, 43f)));
    }

    [Test]
    public void 粒子曲线优先使用实际常量值()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateParticleCurveValue");
        Assert.That(method, Is.Not.Null, "尚未实现粒子曲线实际值计算");

        float constant = (float)method.Invoke(null, new object[] { -20f, 0f, -20f });
        float rangeAverage = (float)method.Invoke(null, new object[] { 0f, 2f, 4f });

        Assert.That(constant, Is.EqualTo(-20f).Within(0.001f));
        Assert.That(rangeAverage, Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void 广域降水盒在相机周围均匀覆盖水平方向()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDistantBoxSize");
        Assert.That(method, Is.Not.Null, "尚未实现广域降水盒尺寸计算");

        Vector3 size = (Vector3)method.Invoke(null, new object[] { 300f, 100f });

        Assert.That(size, Is.EqualTo(new Vector3(600f, 100f, 600f)));
    }

    [Test]
    public void 广域降水盒位于相机上方而非飞机一侧()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDistantBoxAnchorPosition");
        Assert.That(method, Is.Not.Null, "尚未实现广域降水盒锚点计算");

        Vector3 anchor = (Vector3)method.Invoke(null, new object[]
        {
            new Vector3(95f, 12f, -60f),
            80f
        });

        Assert.That(anchor, Is.EqualTo(new Vector3(95f, 92f, -60f)));
    }

    [Test]
    public void 广域降水盒按平面面积计算发射率()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDistantBoxEmissionRate");
        Assert.That(method, Is.Not.Null, "尚未实现广域降水盒发射率计算");

        float rate = (float)method.Invoke(null, new object[] { 6000f, 100f, 300f, 0.1f, 7000f });
        float expected = 6000f * (600f * 600f / (Mathf.PI * 100f * 100f)) * 0.1f;

        Assert.That(rate, Is.EqualTo(expected).Within(0.01f));
    }

    [Test]
    public void 广域长寿命降水受存活粒子预算限制()
    {
        MethodInfo method = GetAnchorType().GetMethod("CalculateDistantBudgetedEmissionRate");
        Assert.That(method, Is.Not.Null, "尚未实现广域降水粒子预算限制");

        float rain = (float)method.Invoke(null, new object[] { 10000f, 1.3f, 15000 });
        float snow = (float)method.Invoke(null, new object[] { 10000f, 5f, 15000 });

        Assert.That(rain, Is.EqualTo(10000f).Within(0.001f));
        Assert.That(snow, Is.EqualTo(3000f).Within(0.001f));
    }

    [Test]
    public void 跟随相机的降水使用本地模拟空间()
    {
        MethodInfo method = GetAnchorType().GetMethod("GetCameraFollowSimulationSpace");
        Assert.That(method, Is.Not.Null, "尚未实现相机跟随降水的模拟空间设置");

        ParticleSystemSimulationSpace simulationSpace =
            (ParticleSystemSimulationSpace)method.Invoke(null, null);

        Assert.That(simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.Local));
    }
}

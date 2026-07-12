using System;
using System.Reflection;
using System.Linq;
using NUnit.Framework;

public class B737UniStormFogDistanceControllerTests
{
    private static float Calculate(float altitudeFt)
    {
        Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("B737UniStormFogDistanceController"))
            .FirstOrDefault(type => type != null);
        Assert.That(controllerType, Is.Not.Null, "尚未创建动态雾距离控制器");

        MethodInfo method = controllerType.GetMethod("CalculateFogStartDistance");
        Assert.That(method, Is.Not.Null, "尚未实现高度映射方法");

        return (float)method.Invoke(null, new object[] { altitudeFt, 500f, 35000f, 8000f, 35000f });
    }

    [TestCase(0f, 8000f)]
    [TestCase(500f, 8000f)]
    [TestCase(35000f, 35000f)]
    [TestCase(45000f, 35000f)]
    [TestCase(float.NaN, 8000f)]
    public void 雾起始距离在边界高度保持正确(float altitudeFt, float expectedDistance)
    {
        float distance = Calculate(altitudeFt);

        Assert.That(distance, Is.EqualTo(expectedDistance).Within(0.001f));
    }

    [Test]
    public void 雾起始距离在爬升中段平滑增加()
    {
        float distance = Calculate(17750f);

        Assert.That(distance, Is.GreaterThan(8000f));
        Assert.That(distance, Is.LessThan(35000f));
    }
}

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class B737UniStormWeatherMenuControllerTests
{
    [TestCase("Partly Cloudy", "局部多云")]
    [TestCase("Heavy Rain", "大雨")]
    [TestCase("Thunder Snow", "雷暴雪")]
    [TestCase("Custom Weather", "Custom Weather")]
    public void 天气名称使用中文显示或保留未知名称(string source, string expected)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("B737UniStormWeatherMenuController"))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "尚未创建天气菜单控制器");

        MethodInfo method = type.GetMethod("GetLocalizedWeatherName");
        Assert.That(method, Is.Not.Null, "尚未实现天气中文名称映射");

        string localized = (string)method.Invoke(null, new object[] { source });

        Assert.That(localized, Is.EqualTo(expected));
    }

    [TestCase(9, 5, "当前调节时间：09:05")]
    [TestCase(23, 59, "当前调节时间：23:59")]
    public void 时间文本使用中文并补齐两位数字(int hour, int minute, string expected)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("B737UniStormWeatherMenuController"))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "尚未创建天气菜单控制器");

        MethodInfo method = type.GetMethod("GetTimeText");
        Assert.That(method, Is.Not.Null, "尚未实现时间中文显示");

        string timeText = (string)method.Invoke(null, new object[] { hour, minute });

        Assert.That(timeText, Is.EqualTo(expected));
    }
}

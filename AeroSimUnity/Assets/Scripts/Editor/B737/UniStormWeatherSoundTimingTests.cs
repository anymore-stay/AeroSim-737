using System.IO;
using NUnit.Framework;
using UnityEngine;

public class UniStormWeatherSoundTimingTests
{
    private const string UniStormSystemPath =
        "Assets/UniStorm Weather System/Scripts/System/UniStormSystem.cs";

    [Test]
    public void 降水天气声音淡入不再等待云量达到目标()
    {
        string methodSource = ReadSoundFadeMethodSource();

        Assert.That(
            methodSource,
            Does.Not.Contain("CurrentWeatherType.PrecipitationWeatherType == WeatherType.Yes_No.Yes"),
            "降水天气声音应该立即淡入，不能再等待云量达到目标。");
    }

    [Test]
    public void 降水天气声音切换时不使用全局天气过渡时长()
    {
        string source = ReadUniStormSystemSource().Replace("\r\n", "\n");

        Assert.That(
            source,
            Does.Contain("A.volume = CurrentWeatherType.WeatherVolume;"),
            "降水天气声音应该在切换时立即设置到目标音量，不能再依赖长时间淡入。");
        Assert.That(
            source,
            Does.Contain("else\n                        {\n                            SoundInCoroutine = StartCoroutine(SoundFadeSequence(10 * TransitionSpeed, CurrentWeatherType.WeatherVolume, A, false));"),
            "10 * TransitionSpeed 的慢淡入只能保留在非降水天气的 else 分支里。");
    }

    private static string ReadSoundFadeMethodSource()
    {
        string source = ReadUniStormSystemSource();
        int methodStart = source.IndexOf(
            "IEnumerator SoundFadeSequence(float TransitionTime, float MaxValue, AudioSource SourceToFade, bool FadeOut)");

        Assert.That(methodStart, Is.GreaterThanOrEqualTo(0), "没有找到天气声音淡入方法");

        int nextMethodStart = source.IndexOf("IEnumerator RainShaderFadeSequence", methodStart);
        Assert.That(nextMethodStart, Is.GreaterThan(methodStart), "没有找到声音淡入方法的结束位置");

        return source.Substring(methodStart, nextMethodStart - methodStart);
    }

    private static string ReadUniStormSystemSource()
    {
        string absolutePath = Path.Combine(Application.dataPath, UniStormSystemPath.Substring("Assets/".Length));
        return File.ReadAllText(absolutePath);
    }
}

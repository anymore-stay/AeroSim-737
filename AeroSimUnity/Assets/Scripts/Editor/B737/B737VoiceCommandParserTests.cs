using System;
using NUnit.Framework;

public class B737VoiceCommandParserTests
{
    [Test]
    public void ParsesChineseThrottlePercentage()
    {
        bool parsed = B737VoiceCommandParser.TryParse(
            "请把油门调到百分之六十五。",
            out B737VoiceCommand command,
            out _);

        Assert.That(parsed, Is.True);
        Assert.That(command.Type, Is.EqualTo(B737VoiceCommandType.SetThrottle));
        Assert.That(command.Value, Is.EqualTo(0.65f).Within(0.001f));
    }

    [Test]
    public void ParsesArabicThrottlePercentage()
    {
        bool parsed = B737VoiceCommandParser.TryParse(
            "油门设置到72%",
            out B737VoiceCommand command,
            out _);

        Assert.That(parsed, Is.True);
        Assert.That(command.Type, Is.EqualTo(B737VoiceCommandType.SetThrottle));
        Assert.That(command.Value, Is.EqualTo(0.72f).Within(0.001f));
    }

    [TestCase("放下起落架", 1f)]
    [TestCase("把起落架收起来", 0f)]
    public void ParsesLandingGearCommands(string transcript, float expectedValue)
    {
        bool parsed = B737VoiceCommandParser.TryParse(
            transcript,
            out B737VoiceCommand command,
            out _);

        Assert.That(parsed, Is.True);
        Assert.That(command.Type, Is.EqualTo(B737VoiceCommandType.SetGearDown));
        Assert.That(command.Value, Is.EqualTo(expectedValue));
    }

    [Test]
    public void RejectsVoiceReverseThrust()
    {
        bool parsed = B737VoiceCommandParser.TryParse(
            "开启反推",
            out _,
            out string message);

        Assert.That(parsed, Is.False);
        Assert.That(message, Does.Contain("暂未开放"));
    }

    [Test]
    public void RejectsUnknownInstruction()
    {
        bool parsed = B737VoiceCommandParser.TryParse(
            "今天天气不错",
            out _,
            out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void BuildsXfyunWebSocketAuthenticationUrl()
    {
        Uri uri = XfyunIatClient.BuildAuthenticatedUri(
            "test-api-key",
            "test-api-secret",
            new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(uri.Scheme, Is.EqualTo("wss"));
        Assert.That(uri.Host, Is.EqualTo("iat-api.xfyun.cn"));
        Assert.That(uri.AbsolutePath, Is.EqualTo("/v2/iat"));
        Assert.That(uri.Query, Does.Contain("authorization="));
        Assert.That(uri.Query, Does.Contain("date="));
        Assert.That(uri.Query, Does.Contain("host="));
        Assert.That(uri.Query, Does.Not.Contain("test-api-secret"));
    }
}

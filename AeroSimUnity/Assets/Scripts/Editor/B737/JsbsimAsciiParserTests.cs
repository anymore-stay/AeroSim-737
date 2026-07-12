using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

public class JsbsimAsciiParserTests
{
    [TestCase("0", 0f)]
    [TestCase("-123.5", -123.5f)]
    [TestCase("+42.25", 42.25f)]
    [TestCase(".5", 0.5f)]
    [TestCase("5.", 5f)]
    [TestCase("1.25e2", 125f)]
    [TestCase("-2.5E-2", -0.025f)]
    [TestCase(" 17.75\r", 17.75f)]
    public void TryParseFloatMatchesExpectedValue(string text, float expected)
    {
        byte[] buffer = Encoding.ASCII.GetBytes(text);

        bool parsed = JsbsimAsciiParser.TryParseFloat(
            buffer,
            0,
            buffer.Length,
            out float value);

        Assert.That(parsed, Is.True);
        Assert.That(value, Is.EqualTo(expected).Within(0.0001f));
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("1.2.3")]
    [TestCase("2e")]
    public void TryParseFloatRejectsInvalidValues(string text)
    {
        byte[] buffer = Encoding.ASCII.GetBytes(text);

        bool parsed = JsbsimAsciiParser.TryParseFloat(
            buffer,
            0,
            buffer.Length,
            out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void ParseValuesParsesCsvWithoutCreatingFieldStrings()
    {
        byte[] buffer = Encoding.ASCII.GetBytes("1,-2.5,3e2,invalid");
        float[] values = new float[4];
        bool[] valid = new bool[4];

        int count = JsbsimAsciiParser.ParseValues(
            buffer,
            0,
            buffer.Length,
            values,
            valid);

        Assert.That(count, Is.EqualTo(4));
        Assert.That(valid, Is.EqualTo(new[] { true, true, true, false }));
        Assert.That(values[0], Is.EqualTo(1f));
        Assert.That(values[1], Is.EqualTo(-2.5f));
        Assert.That(values[2], Is.EqualTo(300f));
    }

    [Test]
    public void BridgeParsesLabelsAndMultipleDataLines()
    {
        GameObject gameObject = new GameObject("JsbsimBridgeParserTest");
        gameObject.SetActive(false);

        try
        {
            JsbsimBridge bridge = gameObject.AddComponent<JsbsimBridge>();
            MethodInfo parsePacket = typeof(JsbsimBridge).GetMethod(
                "ParsePacket",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(byte[]), typeof(int) },
                null);
            FieldInfo latestField = typeof(JsbsimBridge).GetField(
                "latest",
                BindingFlags.Instance | BindingFlags.NonPublic);

            byte[] packet = Encoding.ASCII.GetBytes(
                "<LABELS>,speed,altitude,heading\n"
                + "120.5,35000,271.25\n"
                + "121.25,35010,272.5\n");

            parsePacket.Invoke(bridge, new object[] { packet, packet.Length });
            var latest = (Dictionary<string, float>)latestField.GetValue(bridge);

            Assert.That(latest["speed"], Is.EqualTo(121.25f));
            Assert.That(latest["altitude"], Is.EqualTo(35010f));
            Assert.That(latest["heading"], Is.EqualTo(272.5f));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}

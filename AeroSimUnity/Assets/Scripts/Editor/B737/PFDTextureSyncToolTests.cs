using System;
using System.Reflection;
using NUnit.Framework;

public class PFDTextureSyncToolTests
{
    [Test]
    public void OutputPathPreservesNestedFolders()
    {
        MethodInfo method = GetBuildOutputAssetPathMethod();

        string result = (string)method.Invoke(
            null,
            new object[]
            {
                "Assets/PFD/Textures/Original",
                "Assets/PFD/Textures/Used",
                "Assets/PFD/Textures/Original/Buttons/Left/a.png"
            });

        Assert.That(result, Is.EqualTo("Assets/PFD/Textures/Used/Buttons/Left/a.png"));
    }

    [Test]
    public void OutputPathKeepsRootFilesAtOutputRoot()
    {
        MethodInfo method = GetBuildOutputAssetPathMethod();

        string result = (string)method.Invoke(
            null,
            new object[]
            {
                "Assets/PFD/Textures/Original",
                "Assets/PFD/Textures/PreviewRGB",
                "Assets/PFD/Textures/Original/a.png"
            });

        Assert.That(result, Is.EqualTo("Assets/PFD/Textures/PreviewRGB/a.png"));
    }

    private static MethodInfo GetBuildOutputAssetPathMethod()
    {
        Type toolType = Type.GetType("PFDTextureSyncTool, Assembly-CSharp");
        Assert.That(toolType, Is.Not.Null, "PFDTextureSyncTool 尚未编译。");

        MethodInfo method = toolType.GetMethod(
            "BuildOutputAssetPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "BuildOutputAssetPath 方法不存在。");
        return method;
    }
}

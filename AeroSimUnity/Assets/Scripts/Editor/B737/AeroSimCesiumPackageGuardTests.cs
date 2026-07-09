using System;
using System.Reflection;
using NUnit.Framework;

public class AeroSimCesiumPackageGuardTests
{
    [Test]
    public void GuardExposesPinnedUnityAndCesiumVersions()
    {
        Type guardType = Type.GetType("AeroSim.Editor.AeroSimCesiumPackageGuard, Assembly-CSharp-Editor");
        Assert.That(guardType, Is.Not.Null);

        Assert.That(GetConstString(guardType, "ExpectedUnityVersion"), Is.EqualTo("2022.3.62f3c1"));
        Assert.That(GetConstString(guardType, "SupportedUnityVersionPrefix"), Is.EqualTo("2022.3.62"));
        Assert.That(GetConstString(guardType, "CesiumVersion"), Is.EqualTo("1.24.0"));
    }

    [Test]
    public void SupportedUnityVersionCheckAcceptsSame2022_3_62Family()
    {
        Type guardType = Type.GetType("AeroSim.Editor.AeroSimCesiumPackageGuard, Assembly-CSharp-Editor");
        Assert.That(guardType, Is.Not.Null);

        MethodInfo method = guardType.GetMethod(
            "IsUnityVersionSupported",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, new object[] { "2022.3.62f3c1" }), Is.True);
        Assert.That((bool)method.Invoke(null, new object[] { "2022.3.62f1c1" }), Is.True);
        Assert.That((bool)method.Invoke(null, new object[] { "2022.3.61f1c1" }), Is.False);
        Assert.That((bool)method.Invoke(null, new object[] { "2023.2.0f1" }), Is.False);
    }

    [Test]
    public void NormalizeTextForComparisonIgnoresLineEndingOnlyDifferences()
    {
        Type guardType = Type.GetType("AeroSim.Editor.AeroSimCesiumPackageGuard, Assembly-CSharp-Editor");
        Assert.That(guardType, Is.Not.Null);

        MethodInfo normalizeMethod = guardType.GetMethod(
            "NormalizeTextForComparison",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.That(normalizeMethod, Is.Not.Null);

        string lfText = "line1\nline2\n";
        string crlfText = "line1\r\nline2\r\n";

        string normalizedLf = (string)normalizeMethod.Invoke(null, new object[] { lfText });
        string normalizedCrlf = (string)normalizeMethod.Invoke(null, new object[] { crlfText });

        Assert.That(normalizedLf, Is.EqualTo(normalizedCrlf));
        Assert.That(normalizedLf, Is.EqualTo("line1\nline2\n"));
    }

    [Test]
    public void BuildKnownTargetPathsReturnsProjectAndUserCesiumCacheLocations()
    {
        Type guardType = Type.GetType("AeroSim.Editor.AeroSimCesiumPackageGuard, Assembly-CSharp-Editor");
        Assert.That(guardType, Is.Not.Null);

        MethodInfo buildPathsMethod = guardType.GetMethod(
            "BuildKnownTargetPaths",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.That(buildPathsMethod, Is.Not.Null);

        string[] paths = (string[])buildPathsMethod.Invoke(
            null,
            new object[]
            {
                @"D:\Repo\AeroSim",
                @"C:\Users\dev\AppData\Local",
                "1.24.0",
                @"Source/Runtime/Resources/CesiumDefaultTilesetMaterial.mat"
            });

        Assert.That(paths, Has.Length.EqualTo(2));
        Assert.That(paths[0], Is.EqualTo(@"D:\Repo\AeroSim\Library\PackageCache\com.cesium.unity@1.24.0\Source\Runtime\Resources\CesiumDefaultTilesetMaterial.mat"));
        Assert.That(paths[1], Is.EqualTo(@"C:\Users\dev\AppData\Local\Unity\cache\packages\unity.pkg.cesium.com\com.cesium.unity@1.24.0\Source\Runtime\Resources\CesiumDefaultTilesetMaterial.mat"));
    }

    private static string GetConstString(MemberInfo type, string fieldName)
    {
        FieldInfo field = ((Type)type).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (string)field.GetRawConstantValue();
    }
}

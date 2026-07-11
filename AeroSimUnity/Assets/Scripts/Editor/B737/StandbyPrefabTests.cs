using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class StandbyPrefabTests
{
    private const string PrefabPath =
        "Assets/Aircraft/B737/Instruments/Standby/Prefab/Standby.prefab";

    [Test]
    public void PrefabContainsRequiredViewportsAndMasks()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        string[] viewportNames =
        {
            "HorizonViewport",
            "SpeedTapeViewport",
            "AltitudeTapeViewport",
            "SpeedValueViewport",
            "AltitudeValueViewport",
            "HeadingViewport"
        };

        foreach (string viewportName in viewportNames)
        {
            Transform viewport = FindDescendant(prefab.transform, viewportName);
            Assert.That(viewport, Is.Not.Null, viewportName + " 不存在");
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null, viewportName + " 缺少 RectMask2D");
        }
    }

    [Test]
    public void AltitudeSegmentsUseCorrectOverlapSpacing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform content = FindDescendant(prefab.transform, "AltitudeTapeContent");
        Assert.That(content, Is.Not.Null);
        Assert.That(content.childCount, Is.EqualTo(7));

        for (int i = 1; i < content.childCount; i++)
        {
            RectTransform previous = content.GetChild(i - 1) as RectTransform;
            RectTransform current = content.GetChild(i) as RectTransform;
            Assert.That(
                current.anchoredPosition.y - previous.anchoredPosition.y,
                Is.EqualTo(1960f).Within(0.001f));
        }
    }

    [Test]
    public void ControllerHasAllRequiredReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        StandbyDisplayController controller = prefab.GetComponent<StandbyDisplayController>();
        Assert.That(controller, Is.Not.Null);

        SerializedObject serialized = new SerializedObject(controller);
        AssertReference(serialized, "speedTapeContent");
        AssertReference(serialized, "altitudeTapeContent");
        AssertReference(serialized, "attitudeRollGroup");
        AssertReference(serialized, "bankPointerGroup");
        AssertReference(serialized, "horizonContent");
        AssertReference(serialized, "headingRose");
        AssertReference(serialized, "altitudePairWheel");
        Assert.That(serialized.FindProperty("airspeedDigitWheels").arraySize, Is.EqualTo(3));
        Assert.That(serialized.FindProperty("altitudeMainDigitWheels").arraySize, Is.EqualTo(3));
        Assert.That(serialized.FindProperty("invertHeading").boolValue, Is.True);
        Assert.That(serialized.FindProperty("minimumSpeedKnots").floatValue, Is.EqualTo(0f));
    }

    [Test]
    public void PrefabUsesRealOverlayAndRotatingIndicators()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform overlay = FindDescendant(prefab.transform, "Overlay");
        Transform bankPointer = FindDescendant(prefab.transform, "BankPointer");
        Transform headingRose = FindDescendant(prefab.transform, "HeadingRose");

        Assert.That(overlay, Is.Not.Null);
        Assert.That(bankPointer, Is.Not.Null);
        Assert.That(headingRose, Is.Not.Null);
        Assert.That(bankPointer.parent.name, Is.EqualTo("BankPointerGroup"));
        Assert.That(headingRose.parent.name, Is.EqualTo("HeadingViewport"));
        Assert.That(FindDescendant(prefab.transform, "BankScale"), Is.Null);
        Assert.That(FindDescendant(prefab.transform, "AircraftSymbol"), Is.Null);
        Assert.That(FindDescendant(prefab.transform, "HeadingReference"), Is.Null);
    }

    [Test]
    public void BarometerUsesTextValueAndImageUnit()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform baroReference = FindDescendant(prefab.transform, "BaroReference");
        Transform baroValueTransform = FindDescendant(prefab.transform, "BaroValue");
        Transform baroUnitTransform = FindDescendant(prefab.transform, "BaroUnit");

        Assert.That(baroReference, Is.Not.Null);
        Assert.That(baroValueTransform, Is.Not.Null);
        Assert.That(baroUnitTransform, Is.Not.Null);
        Assert.That(baroReference.GetComponent<Image>(), Is.Null, "BaroReference 不应再使用整段图片");
        Text baroValue = baroValueTransform.GetComponent<Text>();
        Image baroUnit = baroUnitTransform.GetComponent<Image>();
        Assert.That(baroValue, Is.Not.Null);
        Assert.That(baroValue.text, Is.EqualTo("29.91"));
        Assert.That(baroValue.font, Is.Not.Null);
        Assert.That(baroValue.color.g, Is.GreaterThan(baroValue.color.r));
        Assert.That(baroValue.color.g, Is.GreaterThan(baroValue.color.b));
        Assert.That(baroUnit, Is.Not.Null);
        Assert.That(baroUnit.sprite, Is.Not.Null);
        Assert.That(baroUnit.sprite.name, Is.EqualTo("IN_MB-1"));
    }

    [Test]
    public void PrefabUsesBlackBackgroundAndGreyBackingTexture()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform canvas = FindDescendant(prefab.transform, "Canvas");
        Transform blackBackground = FindDirectChild(canvas, "BlackBackground");
        Transform greyBacking = FindDirectChild(canvas, "grey_backing");

        Assert.That(blackBackground, Is.Not.Null, "缺少透明区域下方的纯黑底层");
        Assert.That(greyBacking, Is.Not.Null, "缺少灰色仪表底板");
        Assert.That(blackBackground.GetSiblingIndex(), Is.EqualTo(0));
        Assert.That(greyBacking.GetSiblingIndex(), Is.EqualTo(1));

        Image blackImage = blackBackground.GetComponent<Image>();
        Image greyImage = greyBacking.GetComponent<Image>();
        Assert.That(blackImage, Is.Not.Null);
        Assert.That(blackImage.sprite, Is.Null);
        Assert.That(blackImage.color, Is.EqualTo(Color.black));
        Assert.That(greyImage, Is.Not.Null);
        Assert.That(greyImage.sprite, Is.Not.Null);
        Assert.That(greyImage.sprite.name, Is.EqualTo("grey_backing-1"));
        Assert.That(greyImage.color, Is.EqualTo(Color.white));
    }

    [Test]
    public void AllTexturedGraphicsKeepOriginalWhiteColor()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Graphic[] graphics = prefab.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            bool hasTexture = graphic is RawImage rawImage && rawImage.texture != null;
            bool hasSprite = graphic is Image image && image.sprite != null;
            if (!hasTexture && !hasSprite)
            {
                continue;
            }

            Assert.That(
                graphic.color,
                Is.EqualTo(Color.white),
                graphic.name + " 不应覆盖原图颜色");
        }
    }

    [Test]
    public void AllReferencedStandbyTexturesUseCrispImportSettings()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Graphic[] graphics = prefab.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            Texture texture = null;
            if (graphic is RawImage rawImage)
            {
                texture = rawImage.texture;
            }
            else if (graphic is Image image && image.sprite != null)
            {
                texture = image.sprite.texture;
            }

            if (texture == null)
            {
                continue;
            }

            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, texturePath + " 缺少纹理导入器");
            Assert.That(importer.mipmapEnabled, Is.False, texturePath + " 不应生成 Mipmap");
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), texturePath + " 应使用点采样");
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), texturePath + " 应使用 Clamp");
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed),
                texturePath + " 不应压缩");
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), texturePath + " 不应缩放");
        }
    }

    private static void AssertReference(SerializedObject serialized, string propertyName)
    {
        Assert.That(
            serialized.FindProperty(propertyName).objectReferenceValue,
            Is.Not.Null,
            propertyName + " 未绑定");
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant.name == objectName)
            {
                return descendant;
            }
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }
}

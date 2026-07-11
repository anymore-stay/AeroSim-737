using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 按 278×278 参考坐标生成 Standby 初版仪表。
/// </summary>
public static class StandbyInstrumentBuilder
{
    private const string StandbyRoot = "Assets/Aircraft/B737/Instruments/Standby";
    private const string PrefabPath = StandbyRoot + "/Prefab/Standby.prefab";
    private const string DemoScenePath = StandbyRoot + "/Scene/Standby_demo.unity";
    private const string OriginTextureRoot = StandbyRoot + "/Textures/Origin";
    private const string GeneratedTextureRoot = StandbyRoot + "/Textures/Generated";
    private const string ConvertedTextureRoot = StandbyRoot + "/Textures/Standby";

    [MenuItem("工具/B737/Standby/生成初版仪表")]
    public static void BuildStandbyDemo()
    {
        ConfigureTextureImporters();
        BuildPrefab();
        BindDemoDataSource();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Standby 初版仪表已生成并绑定到 Standby_demo。请进入 Play 检查模拟数据。");
    }

    private static void BuildPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform canvasTransform = prefabRoot.transform.Find("Canvas");
            if (canvasTransform == null)
            {
                throw new MissingReferenceException("Standby.prefab 中缺少 Canvas。 ");
            }

            ClearChildren(canvasTransform);
            RectTransform canvasRect = canvasTransform as RectTransform;
            SetRect(canvasRect, Vector2.zero, new Vector2(278f, 278f));
            // Standby_demo 相机位于 Canvas 背面，翻转 X 后文字方向与参考图一致。
            canvasRect.localScale = new Vector3(-1f, 1f, 1f);

            CreateImage(
                canvasTransform,
                "grey_backing",
                null,
                Vector2.zero,
                new Vector2(278f, 278f),
                Color.black);

            RectTransform horizonViewport = CreateViewport(
                canvasTransform,
                "HorizonViewport",
                new Vector2(-6.5f, -1.5f),
                new Vector2(171f, 199f));
            RectTransform attitudeRollGroup = CreateRect(
                horizonViewport,
                "AttitudeRollGroup",
                Vector2.zero,
                new Vector2(171f, 198f));
            RectTransform horizonImage = CreateImage(
                attitudeRollGroup,
                "HorizonImage",
                LoadSprite(GeneratedTextureRoot + "/StandbyHorizon.png"),
                new Vector2(0f, -9.5f),
                new Vector2(301f, 1164f),
                Color.white);
            CreateImage(
                attitudeRollGroup,
                "BankPointer",
                LoadSprite(OriginTextureRoot + "/bank_pointer-1.png"),
                new Vector2(0f, 85f),
                new Vector2(13f, 11f),
                Color.white);
            CreateImage(
                horizonViewport,
                "BankScale",
                LoadSprite(GeneratedTextureRoot + "/StandbyBankScale.png"),
                new Vector2(0f, 85f),
                new Vector2(171f, 28f),
                Color.white);
            CreateImage(
                horizonViewport,
                "AircraftSymbol",
                LoadSprite(GeneratedTextureRoot + "/StandbyAircraftSymbol.png"),
                new Vector2(0f, -11.5f),
                new Vector2(111f, 37f),
                Color.white);

            RectTransform speedViewport = CreateViewport(
                canvasTransform,
                "SpeedTapeViewport",
                new Vector2(-115.5f, 0f),
                new Vector2(43f, 278f));
            RawImage speedTapeImage = CreateRawImage(
                speedViewport,
                "SpeedTapeContent",
                LoadTexture(OriginTextureRoot + "/speed_tape-1.png"),
                new Vector2(0f, 627f),
                new Vector2(43f, 1350f));

            RectTransform altitudeViewport = CreateViewport(
                canvasTransform,
                "AltitudeTapeViewport",
                new Vector2(109f, 0f),
                new Vector2(58f, 278f));
            RectTransform altitudeContent = CreateRect(
                altitudeViewport,
                "AltitudeTapeContent",
                new Vector2(-0.5f, 0f),
                new Vector2(57f, 13808f));
            string[] altitudeTextureNames =
            {
                "-1_6k-1.png",
                "6_13k-1.png",
                "13_20k-1.png",
                "20-27k-1.png",
                "27-34k-1.png",
                "34-41k-1.png",
                "41-48k-1.png"
            };
            for (int i = 0; i < altitudeTextureNames.Length; i++)
            {
                CreateRawImage(
                    altitudeContent,
                    "AltitudeTape_" + i,
                    LoadTexture(OriginTextureRoot + "/Alt_Tapes/" + altitudeTextureNames[i]),
                    new Vector2(0f, 756.5f + i * 1960f),
                    new Vector2(57f, 2048f));
            }

            RawImage[] speedWheels = CreateSpeedWheels(canvasTransform);
            RawImage[] altitudeWheels = CreateAltitudeWheels(canvasTransform, out RawImage altitudePairWheel);

            CreateImage(
                canvasTransform,
                "HeadingReference",
                LoadSprite(GeneratedTextureRoot + "/StandbyHeadingReference.png"),
                new Vector2(-6.5f, -121.5f),
                new Vector2(171f, 33f),
                Color.white);
            CreateImage(
                canvasTransform,
                "BaroReference",
                LoadSprite(GeneratedTextureRoot + "/StandbyBaroReference.png"),
                new Vector2(36f, 122f),
                new Vector2(90f, 34f),
                Color.white);

            StandbyDisplayController controller = prefabRoot.GetComponent<StandbyDisplayController>();
            if (controller == null)
            {
                controller = prefabRoot.AddComponent<StandbyDisplayController>();
            }

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("speedTapeContent").objectReferenceValue = speedTapeImage.rectTransform;
            serializedController.FindProperty("altitudeTapeContent").objectReferenceValue = altitudeContent;
            serializedController.FindProperty("attitudeRollGroup").objectReferenceValue = attitudeRollGroup;
            serializedController.FindProperty("horizonContent").objectReferenceValue = horizonImage;
            SetObjectArray(serializedController.FindProperty("airspeedDigitWheels"), speedWheels);
            SetObjectArray(serializedController.FindProperty("altitudeMainDigitWheels"), altitudeWheels);
            serializedController.FindProperty("altitudePairWheel").objectReferenceValue = altitudePairWheel;
            serializedController.FindProperty("minimumSpeedKnots").floatValue = 30f;
            serializedController.FindProperty("maximumSpeedKnots").floatValue = 500f;
            serializedController.FindProperty("speedReferenceKnots").floatValue = 40f;
            serializedController.FindProperty("speedPixelsPerKnot").floatValue = 2.8f;
            serializedController.FindProperty("invertSpeedTape").boolValue = true;
            serializedController.FindProperty("altitudePixelsPerFoot").floatValue = 0.28f;
            serializedController.FindProperty("invertAltitudeTape").boolValue = true;
            serializedController.FindProperty("pitchPixelsPerDegree").floatValue = 4.8f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            controller.RebuildBasePose();
            controller.RefreshAll();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static RawImage[] CreateSpeedWheels(Transform canvasTransform)
    {
        RectTransform frame = CreateFrame(
            canvasTransform,
                "SpeedValueViewport",
                new Vector2(-115.5f, -12.5f),
                new Vector2(47f, 40f));
        Texture2D texture = LoadTexture(OriginTextureRoot + "/speed_scrolling-1.png");
        RawImage[] wheels = new RawImage[3];
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i] = CreateRawImage(
                frame,
                "AirspeedWheel_" + i,
                texture,
                new Vector2(-12.5f + i * 14f, 0f),
                new Vector2(14f, 39f));
        }

        return wheels;
    }

    private static RawImage[] CreateAltitudeWheels(
        Transform canvasTransform,
        out RawImage pairWheel)
    {
        RectTransform frame = CreateFrame(
            canvasTransform,
            "AltitudeValueViewport",
            new Vector2(103f, -13.5f),
            new Vector2(72f, 43f));
        Texture2D mainTexture = LoadTexture(GeneratedTextureRoot + "/StandbyAltitudeMainDigits.png");
        RawImage[] wheels = new RawImage[3];
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i] = CreateRawImage(
                frame,
                "AltitudeWheel_" + i,
                mainTexture,
                new Vector2(-26f + i * 14f, 0f),
                new Vector2(14f, 39f));
        }

            pairWheel = CreateRawImage(
                frame,
                "AltitudePairWheel",
                LoadTexture(ConvertedTextureRoot + "/Scrolling_20s-1.png"),
                new Vector2(21f, 0f),
                new Vector2(24f, 37f));
        return wheels;
    }

    private static RectTransform CreateFrame(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size)
    {
        RectTransform frame = CreateRect(parent, objectName, position, size);
        Image image = frame.gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        Outline outline = frame.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        frame.gameObject.AddComponent<RectMask2D>();
        return frame;
    }

    private static RectTransform CreateViewport(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size)
    {
        RectTransform viewport = CreateRect(parent, objectName, position, size);
        viewport.gameObject.AddComponent<RectMask2D>();
        return viewport;
    }

    private static RectTransform CreateRect(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        SetRect(rectTransform, position, size);
        return rectTransform;
    }

    private static RectTransform CreateImage(
        Transform parent,
        string objectName,
        Sprite sprite,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        RectTransform rectTransform = CreateRect(parent, objectName, position, size);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return rectTransform;
    }

    private static RawImage CreateRawImage(
        Transform parent,
        string objectName,
        Texture2D texture,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rectTransform = CreateRect(parent, objectName, position, size);
        RawImage rawImage = rectTransform.gameObject.AddComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    private static void SetRect(RectTransform rectTransform, Vector2 position, Vector2 size)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void SetObjectArray(SerializedProperty property, RawImage[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void ConfigureTextureImporters()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { OriginTextureRoot, GeneratedTextureRoot, ConvertedTextureRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            TextureImporterSettings textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D LoadTexture(string assetPath)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            throw new FileNotFoundException("找不到 Standby 贴图。", assetPath);
        }

        return texture;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            throw new FileNotFoundException("找不到 Standby Sprite。", assetPath);
        }

        return sprite;
    }

    private static void BindDemoDataSource()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != DemoScenePath)
        {
            Debug.LogWarning("当前不是 Standby_demo 场景，Prefab 已生成，但未创建模拟数据对象。 ");
            return;
        }

        StandbyDisplayController displayController =
            Object.FindObjectOfType<StandbyDisplayController>(true);
        if (displayController == null)
        {
            throw new MissingReferenceException("Standby_demo 中找不到 StandbyDisplayController。 ");
        }

        GameObject demoObject = GameObject.Find("Standby_DemoData");
        if (demoObject == null)
        {
            demoObject = new GameObject("Standby_DemoData");
        }

        StandbyDemoDataSource dataSource = demoObject.GetComponent<StandbyDemoDataSource>();
        if (dataSource == null)
        {
            dataSource = demoObject.AddComponent<StandbyDemoDataSource>();
        }

        SerializedObject serializedDataSource = new SerializedObject(dataSource);
        serializedDataSource.FindProperty("displayController").objectReferenceValue = displayController;
        serializedDataSource.FindProperty("speedCycleSeconds").floatValue = 120f;
        serializedDataSource.FindProperty("altitudeCycleSeconds").floatValue = 120f;
        serializedDataSource.FindProperty("attitudeCycleSeconds").floatValue = 36f;
        serializedDataSource.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
    }
}

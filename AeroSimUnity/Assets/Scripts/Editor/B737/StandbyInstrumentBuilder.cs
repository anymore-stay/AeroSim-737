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
    private static readonly Color BarometerTextColor = new Color32(0, 150, 0, 255);
    private const string StandbyRoot = "Assets/Aircraft/B737/Instruments/Standby";
    private const string PrefabPath = StandbyRoot + "/Prefab/Standby.prefab";
    private const string DemoScenePath = StandbyRoot + "/Scene/Standby_demo.unity";
    private const string OriginTextureRoot = StandbyRoot + "/Textures/Origin";
    private const string GeneratedTextureRoot = StandbyRoot + "/Textures/Generated";
    private const string ConvertedTextureRoot = StandbyRoot + "/Textures/Standby";

    public static void BuildStandbyDemo()
    {
        ConfigureTextureImporters();
        BuildPrefab();
        BindDemoDataSource();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Standby 初版仪表已生成并绑定到 Standby_demo。请进入 Play 检查模拟数据。");
    }

    public static void ApplyCurrentVisualDefaults()
    {
        ConfigureTextureImporters();
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplyVisualDefaults(prefabRoot);

            // 只写入高度百位零点，不调用刷新逻辑，避免碰到用户已经微调好的位置和尺寸。
            StandbyDisplayController controller = prefabRoot.GetComponent<StandbyDisplayController>();
            if (controller != null)
            {
                SerializedObject serializedController = new SerializedObject(controller);
                SerializedProperty offsetProperty =
                    serializedController.FindProperty("altitudeHundredsWheelUvOffsetY");
                if (offsetProperty != null)
                {
                    offsetProperty.floatValue = 0.008f;
                    serializedController.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Transform altitudeWheelTransform = FindDescendant(prefabRoot.transform, "AltitudeWheel_2");
            RawImage altitudeWheel = altitudeWheelTransform != null
                ? altitudeWheelTransform.GetComponent<RawImage>()
                : null;
            if (altitudeWheel != null)
            {
                Rect uvRect = altitudeWheel.uvRect;
                uvRect.y = 0.04881633f;
                altitudeWheel.uvRect = uvRect;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Standby 当前视觉资源已修复：保留布局，恢复原图颜色并补齐灰色底板。");
    }

    public static void ApplyCurrentAirspeedStart()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            StandbyDisplayController controller = prefabRoot.GetComponent<StandbyDisplayController>();
            if (controller == null)
            {
                throw new MissingReferenceException("Standby.prefab 中缺少 StandbyDisplayController。 ");
            }

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("minimumSpeedKnots").floatValue = 0f;
            serializedController.FindProperty("airspeedKnots").floatValue = 0f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == DemoScenePath)
        {
            StandbyDemoDataSource dataSource = Object.FindObjectOfType<StandbyDemoDataSource>(true);
            if (dataSource != null)
            {
                SerializedObject serializedDataSource = new SerializedObject(dataSource);
                serializedDataSource.FindProperty("speedMinimumKnots").floatValue = 0f;
                serializedDataSource.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Standby 空速已同步：数字从 0 开始，刻度带超过 40 节后才滚动。");
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
                "BlackBackground",
                null,
                Vector2.zero,
                new Vector2(278f, 278f),
                Color.black);

            CreateImage(
                canvasTransform,
                "grey_backing",
                LoadSprite(ConvertedTextureRoot + "/grey_backing-1.png"),
                Vector2.zero,
                new Vector2(278f, 278f),
                Color.white);

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

            RectTransform headingViewport = CreateViewport(
                canvasTransform,
                "HeadingViewport",
                new Vector2(-6.5f, -121.5f),
                new Vector2(171f, 33f));
            RectTransform headingRose = CreateImage(
                headingViewport,
                "HeadingRose",
                LoadSprite(ConvertedTextureRoot + "/rose-1.png"),
                new Vector2(-1f, -173.5f),
                new Vector2(381f, 380f),
                Color.white);

            CreateImage(
                canvasTransform,
                "Overlay",
                LoadSprite(ConvertedTextureRoot + "/overlay-1.png"),
                new Vector2(0f, -7f),
                new Vector2(278f, 212f),
                Color.white);

            RectTransform bankPointerGroup = CreateRect(
                canvasTransform,
                "BankPointerGroup",
                new Vector2(-6.5f, -1.5f),
                new Vector2(171f, 199f));
            CreateImage(
                bankPointerGroup,
                "BankPointer",
                LoadSprite(ConvertedTextureRoot + "/bank_pointer-1.png"),
                new Vector2(0f, 85f),
                new Vector2(13f, 11f),
                Color.white);

            RawImage[] speedWheels = CreateSpeedWheels(canvasTransform);
            RawImage[] altitudeWheels = CreateAltitudeWheels(canvasTransform, out RawImage altitudePairWheel);

            CreateBarometerReference(canvasTransform);

            StandbyDisplayController controller = prefabRoot.GetComponent<StandbyDisplayController>();
            if (controller == null)
            {
                controller = prefabRoot.AddComponent<StandbyDisplayController>();
            }

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("speedTapeContent").objectReferenceValue = speedTapeImage.rectTransform;
            serializedController.FindProperty("altitudeTapeContent").objectReferenceValue = altitudeContent;
            serializedController.FindProperty("attitudeRollGroup").objectReferenceValue = attitudeRollGroup;
            serializedController.FindProperty("bankPointerGroup").objectReferenceValue = bankPointerGroup;
            serializedController.FindProperty("horizonContent").objectReferenceValue = horizonImage;
            serializedController.FindProperty("headingRose").objectReferenceValue = headingRose;
            SetObjectArray(serializedController.FindProperty("airspeedDigitWheels"), speedWheels);
            SetObjectArray(serializedController.FindProperty("altitudeMainDigitWheels"), altitudeWheels);
            serializedController.FindProperty("altitudePairWheel").objectReferenceValue = altitudePairWheel;
            serializedController.FindProperty("altitudeHundredsWheelUvOffsetY").floatValue = 0.008f;
            serializedController.FindProperty("minimumSpeedKnots").floatValue = 0f;
            serializedController.FindProperty("maximumSpeedKnots").floatValue = 500f;
            serializedController.FindProperty("speedReferenceKnots").floatValue = 40f;
            serializedController.FindProperty("speedPixelsPerKnot").floatValue = 2.8f;
            serializedController.FindProperty("invertSpeedTape").boolValue = true;
            serializedController.FindProperty("altitudePixelsPerFoot").floatValue = 0.28f;
            serializedController.FindProperty("invertAltitudeTape").boolValue = true;
            serializedController.FindProperty("pitchPixelsPerDegree").floatValue = 4.8f;
            // 实机 RenderTexture 与显示平面还会经过一次方向转换，这里反转圆盘以匹配 PFD。
            serializedController.FindProperty("invertHeading").boolValue = true;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            controller.RebuildBasePose();
            controller.RefreshAll();
            ApplyOriginalTextureColors(canvasTransform);

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
            new Vector2(-114f, -12.5f),
            new Vector2(42f, 39f));
        Texture2D texture = LoadTexture(OriginTextureRoot + "/speed_scrolling-1.png");
        RawImage[] wheels = new RawImage[3];
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i] = CreateRawImage(
                frame,
                "AirspeedWheel_" + i,
                texture,
                new Vector2(-14f + i * 14f, 0f),
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
            new Vector2(103f, -12.5f),
            new Vector2(66f, 39f));
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

    private static void ApplyVisualDefaults(GameObject prefabRoot)
    {
        Transform canvasTransform = prefabRoot.transform.Find("Canvas");
        if (canvasTransform == null)
        {
            throw new MissingReferenceException("Standby.prefab 中缺少 Canvas。 ");
        }

        RectTransform blackBackground = FindDirectChild(canvasTransform, "BlackBackground");
        if (blackBackground == null)
        {
            blackBackground = CreateImage(
                canvasTransform,
                "BlackBackground",
                null,
                Vector2.zero,
                new Vector2(278f, 278f),
                Color.black);
        }

        Image blackImage = blackBackground.GetComponent<Image>();
        if (blackImage == null)
        {
            blackImage = blackBackground.gameObject.AddComponent<Image>();
        }

        blackImage.sprite = null;
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
        blackBackground.SetAsFirstSibling();

        RectTransform greyBacking = FindDirectChild(canvasTransform, "grey_backing");
        if (greyBacking == null)
        {
            greyBacking = CreateImage(
                canvasTransform,
                "grey_backing",
                LoadSprite(ConvertedTextureRoot + "/grey_backing-1.png"),
                Vector2.zero,
                new Vector2(278f, 278f),
                Color.white);
        }

        Image greyImage = greyBacking.GetComponent<Image>();
        if (greyImage == null)
        {
            greyImage = greyBacking.gameObject.AddComponent<Image>();
        }

        greyImage.sprite = LoadSprite(ConvertedTextureRoot + "/grey_backing-1.png");
        greyImage.color = Color.white;
        greyImage.preserveAspect = false;
        greyImage.raycastTarget = false;
        greyBacking.SetSiblingIndex(1);

        ApplyBarometerReference(canvasTransform);
        ApplyOriginalTextureColors(canvasTransform);
    }

    private static void CreateBarometerReference(Transform canvasTransform)
    {
        RectTransform baroReference = CreateRect(
            canvasTransform,
            "BaroReference",
            new Vector2(36f, 122f),
            new Vector2(90f, 34f));
        PopulateBarometerReference(baroReference);
    }

    private static void ApplyBarometerReference(Transform canvasTransform)
    {
        RectTransform baroReference = FindDirectChild(canvasTransform, "BaroReference");
        if (baroReference == null)
        {
            CreateBarometerReference(canvasTransform);
            return;
        }

        Image oldBarometerImage = baroReference.GetComponent<Image>();
        if (oldBarometerImage != null)
        {
            Object.DestroyImmediate(oldBarometerImage);
        }

        PopulateBarometerReference(baroReference);
    }

    private static void PopulateBarometerReference(RectTransform baroReference)
    {
        ClearChildren(baroReference);

        Text baroValue = CreateText(
            baroReference,
            "BaroValue",
            "29.91",
            new Vector2(-10.5f, 0f),
            new Vector2(49f, 16f),
            16,
            BarometerTextColor);
        baroValue.alignment = TextAnchor.MiddleCenter;

        CreateImage(
            baroReference,
            "BaroUnit",
            LoadSprite(OriginTextureRoot + "/IN_MB-1.png"),
            new Vector2(27.5f, -0.5f),
            new Vector2(17f, 17f),
            Color.white);
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color)
    {
        RectTransform rectTransform = CreateRect(parent, objectName, position, size);
        Text text = rectTransform.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void ApplyOriginalTextureColors(Transform canvasTransform)
    {
        Graphic[] graphics = canvasTransform.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            bool hasTexture = graphic is RawImage rawImage && rawImage.texture != null;
            bool hasSprite = graphic is Image image && image.sprite != null;
            if (hasTexture || hasSprite)
            {
                graphic.color = Color.white;
            }
        }
    }

    private static RectTransform FindDirectChild(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private static Transform FindDescendant(Transform parent, string objectName)
    {
        Transform[] descendants = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant.name == objectName)
            {
                return descendant;
            }
        }

        return null;
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
        serializedDataSource.FindProperty("speedMinimumKnots").floatValue = 0f;
        serializedDataSource.FindProperty("speedCycleSeconds").floatValue = 120f;
        serializedDataSource.FindProperty("altitudeCycleSeconds").floatValue = 120f;
        serializedDataSource.FindProperty("attitudeCycleSeconds").floatValue = 36f;
        serializedDataSource.ApplyModifiedPropertiesWithoutUndo();

        Camera demoCamera = Camera.main;
        if (demoCamera != null)
        {
            StandbyDemoPixelPerfectCamera pixelPerfectCamera =
                demoCamera.GetComponent<StandbyDemoPixelPerfectCamera>();
            if (pixelPerfectCamera == null)
            {
                pixelPerfectCamera = demoCamera.gameObject.AddComponent<StandbyDemoPixelPerfectCamera>();
            }

            pixelPerfectCamera.ApplyPixelPerfectSize();
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
    }
}

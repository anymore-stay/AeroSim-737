using UnityEditor;
using UnityEngine;

/// <summary>
/// 在 B737 Prefab 中建立左右 PFD 的相机、渲染纹理、材质和物理显示平面。
/// </summary>
public static class B737PFDDisplayRigEditorUtility
{
    private const string B737PrefabPath = "Assets/Aircraft/B737/Prefabs/B737.prefab";
    private const string PfdPrefabPath = "Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab";
    private const string LeftRenderTexturePath = "Assets/Aircraft/B737/Textures/PFD_Left.renderTexture";
    private const string RightRenderTexturePath = "Assets/Aircraft/B737/Textures/PFD_Right.renderTexture";
    private const string LeftMaterialPath = "Assets/Aircraft/B737/Materials/PFD_Left.mat";
    private const string RightMaterialPath = "Assets/Aircraft/B737/Materials/PFD_Right.mat";

    private const int LeftLayer = 10;
    private const int RightLayer = 11;
    private const float InitialHorizontalOffset = 0.255f;

    [MenuItem("AeroSim/B737/生成左右 PFD 显示链路")]
    public static void Generate()
    {
        ConfigureLayer(LeftLayer, "PFD_Left");
        ConfigureLayer(RightLayer, "PFD_Right");

        RenderTexture leftTexture = EnsureRenderTexture(LeftRenderTexturePath, "PFD_Left");
        RenderTexture rightTexture = EnsureRenderTexture(RightRenderTexturePath, "PFD_Right");
        Material leftMaterial = EnsureMaterial(LeftMaterialPath, "PFD_Left", leftTexture);
        Material rightMaterial = EnsureMaterial(RightMaterialPath, "PFD_Right", rightTexture);

        GameObject pfdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PfdPrefabPath);
        if (pfdPrefab == null)
        {
            Debug.LogError("[PFD] 未找到 PFD_Display.prefab，无法生成座舱显示链路。");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(B737PrefabPath);
        try
        {
            Transform rigRoot = EnsureChild(prefabRoot.transform, "B737_PFD_Rig");
            ConfigureSide(
                rigRoot,
                pfdPrefab,
                "PFD_Left_Rig",
                "PFD_Display_Left",
                "PFD_Left_Camera",
                LeftLayer,
                leftTexture);
            ConfigureSide(
                rigRoot,
                pfdPrefab,
                "PFD_Right_Rig",
                "PFD_Display_Right",
                "PFD_Right_Camera",
                RightLayer,
                rightTexture);

            Transform screenDirt = FindByName(prefabRoot.transform, "屏幕污渍");
            if (screenDirt == null)
            {
                throw new MissingReferenceException("未找到 B737/驾驶舱/屏幕污渍 节点。");
            }

            Transform leftReference = FindDirectChild(screenDirt, "ND_Plane");
            Transform rightReference = FindDirectChild(screenDirt, "ND_Plane (1)");
            EnsureDisplayPlane(
                screenDirt,
                leftReference,
                "PFD_Left_Plane",
                leftMaterial,
                InitialHorizontalOffset);
            EnsureDisplayPlane(
                screenDirt,
                rightReference,
                "PFD_Right_Plane",
                rightMaterial,
                -InitialHorizontalOffset);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, B737PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PFD] 左右 PFD 显示链路已生成。Plane 位置可在屏幕污渍节点下独立微调。");
    }

    private static void ConfigureSide(
        Transform rigRoot,
        GameObject pfdPrefab,
        string sideRigName,
        string displayName,
        string cameraName,
        int layer,
        RenderTexture targetTexture)
    {
        Transform sideRig = EnsureChild(rigRoot, sideRigName);
        Camera renderCamera = EnsureCamera(sideRig, cameraName, layer, targetTexture);

        Transform displayTransform = FindDirectChild(sideRig, displayName);
        GameObject display;
        if (displayTransform == null)
        {
            display = PrefabUtility.InstantiatePrefab(pfdPrefab, sideRig) as GameObject;
            display.name = displayName;
            display.transform.localPosition = Vector3.zero;
            display.transform.localRotation = Quaternion.identity;
            display.transform.localScale = Vector3.one;
        }
        else
        {
            display = displayTransform.gameObject;
        }

        SetLayerRecursively(display, layer);

        Canvas canvas = display.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            throw new MissingComponentException(displayName + " 内没有 Canvas。");
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = renderCamera;
        canvas.planeDistance = 1f;
        canvas.targetDisplay = 0;

        PFDJsbsimDataDriver driver = display.GetComponent<PFDJsbsimDataDriver>();
        if (driver == null)
        {
            driver = display.AddComponent<PFDJsbsimDataDriver>();
        }

        DisableSimulator<PFDAirspeedTapeSimulator>(display);
        DisableSimulator<PFDAltitudeTapeSimulator>(display);
        DisableSimulator<PFDAttitudeSimulator>(display);
        DisableSimulator<PFDHeadingRoseSimulator>(display);
        DisableSimulator<PFDAngleOfAttackGaugeSimulator>(display);
        DisableSimulator<PFDVerticalSpeedIndicatorSimulator>(display);

        SetChildActive(display.transform, "PFD_PreviewGuide", false);
        SetChildActive(display.transform, "PFD_Final", true);
        EditorUtility.SetDirty(driver);
        EditorUtility.SetDirty(canvas);
    }

    private static Camera EnsureCamera(
        Transform parent,
        string cameraName,
        int layer,
        RenderTexture targetTexture)
    {
        Transform cameraTransform = FindDirectChild(parent, cameraName);
        GameObject cameraObject;
        if (cameraTransform == null)
        {
            cameraObject = new GameObject(cameraName, typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
        }
        else
        {
            cameraObject = cameraTransform.gameObject;
        }

        Camera renderCamera = cameraObject.GetComponent<Camera>();
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = Color.black;
        renderCamera.orthographic = true;
        renderCamera.orthographicSize = 1f;
        renderCamera.nearClipPlane = 0.1f;
        renderCamera.farClipPlane = 100f;
        renderCamera.allowHDR = false;
        renderCamera.allowMSAA = false;
        renderCamera.cullingMask = 1 << layer;
        renderCamera.targetTexture = targetTexture;
        renderCamera.enabled = true;
        EditorUtility.SetDirty(renderCamera);
        return renderCamera;
    }

    private static void EnsureDisplayPlane(
        Transform screenDirt,
        Transform referencePlane,
        string planeName,
        Material material,
        float horizontalOffset)
    {
        if (referencePlane == null)
        {
            throw new MissingReferenceException("未找到用于复制的 ND Plane。");
        }

        Transform existing = FindDirectChild(screenDirt, planeName);
        GameObject plane;
        if (existing == null)
        {
            plane = Object.Instantiate(referencePlane.gameObject, screenDirt);
            plane.name = planeName;
            plane.transform.localPosition = referencePlane.localPosition
                + new Vector3(horizontalOffset, 0f, 0f);
            plane.transform.localRotation = referencePlane.localRotation;
            plane.transform.localScale = referencePlane.localScale;
        }
        else
        {
            // 已存在时保留用户在 Inspector 中微调过的位置、角度和比例。
            plane = existing.gameObject;
        }

        MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            throw new MissingComponentException(planeName + " 缺少 MeshRenderer。");
        }

        renderer.sharedMaterial = material;
        plane.SetActive(true);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(plane.transform);
    }

    private static RenderTexture EnsureRenderTexture(string path, string assetName)
    {
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture == null)
        {
            texture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32)
            {
                name = assetName,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            AssetDatabase.CreateAsset(texture, path);
        }
        else
        {
            texture.Release();
            texture.width = 512;
            texture.height = 512;
            texture.depth = 0;
            texture.format = RenderTextureFormat.ARGB32;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.antiAliasing = 1;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            EditorUtility.SetDirty(texture);
        }

        return texture;
    }

    private static Material EnsureMaterial(string path, string assetName, RenderTexture texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new MissingReferenceException("未找到 URP Lit Shader，无法生成 PFD 玻璃材质。");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = assetName };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            // 现有材质也迁移到 Lit，避免重新生成后继续使用无光照材质。
            material.shader = shader;
        }

        // 无论旧材质是否被改成透明，都统一恢复为不透明屏幕材质，避免排序和深度写入异常。
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.Zero);
        material.SetFloat("_ZWrite", 1f);
        material.renderQueue = -1;
        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");

        material.mainTexture = texture;
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f, 1f));
        material.SetFloat("_Smoothness", 0.35f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        // 清除“发光为黑色”标记，确保 _EMISSION 在资源重载后仍然保持启用。
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        UnityEngine.Rendering.LocalKeyword emissionKeyword =
            new UnityEngine.Rendering.LocalKeyword(shader, "_EMISSION");
        material.EnableKeyword(emissionKeyword);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureLayer(int index, string layerName)
    {
        Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        SerializedObject serializedTagManager = new SerializedObject(tagManager);
        SerializedProperty layers = serializedTagManager.FindProperty("layers");
        SerializedProperty layer = layers.GetArrayElementAtIndex(index);

        if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != layerName)
        {
            throw new UnityException("Layer " + index + " 已被占用：" + layer.stringValue);
        }

        layer.stringValue = layerName;
        serializedTagManager.ApplyModifiedProperties();
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindByName(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindByName(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }
    }

    private static void DisableSimulator<T>(GameObject display) where T : Behaviour
    {
        T simulator = display.GetComponent<T>();
        if (simulator != null)
        {
            simulator.enabled = false;
            EditorUtility.SetDirty(simulator);
        }
    }

    private static void SetChildActive(Transform root, string childName, bool active)
    {
        Transform child = FindByName(root, childName);
        if (child != null)
        {
            child.gameObject.SetActive(active);
            EditorUtility.SetDirty(child.gameObject);
        }
    }
}

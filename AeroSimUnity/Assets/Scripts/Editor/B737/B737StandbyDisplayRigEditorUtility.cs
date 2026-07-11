using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在 B737 Prefab 中建立备用仪表的独立渲染链路和座舱显示平面。
/// </summary>
public static class B737StandbyDisplayRigEditorUtility
{
    private const string B737PrefabPath = "Assets/Aircraft/B737/Prefabs/B737.prefab";
    private const string StandbyPrefabPath = "Assets/Aircraft/B737/Instruments/Standby/Prefab/Standby.prefab";
    private const string RenderTexturePath = "Assets/Aircraft/B737/Textures/Standby.renderTexture";
    private const string MaterialPath = "Assets/Aircraft/B737/Materials/Standby.mat";
    private const int StandbyLayer = 14;

    [MenuItem("AeroSim/B737/生成 Standby 显示链路")]
    public static void Generate()
    {
        ConfigureLayer(StandbyLayer, "Standby");
        RenderTexture renderTexture = EnsureRenderTexture();
        Material material = EnsureMaterial(renderTexture);
        GameObject standbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StandbyPrefabPath);
        if (standbyPrefab == null)
        {
            throw new MissingReferenceException("未找到 Standby.prefab。");
        }

        B737FmsDisplayRig.SuppressEditorRebuild = true;
        GameObject prefabRoot = null;
        try
        {
            prefabRoot = PrefabUtility.LoadPrefabContents(B737PrefabPath);
            Transform rig = EnsureChild(prefabRoot.transform, "B737_Standby_Rig");
            Camera renderCamera = EnsureCamera(rig, renderTexture);
            GameObject display = EnsureDisplay(rig, standbyPrefab, renderCamera);
            EnsurePhysicalPlane(prefabRoot.transform, material);

            StandbyDemoDataSource demoData = display.GetComponent<StandbyDemoDataSource>();
            if (demoData != null)
            {
                demoData.enabled = false;
                EditorUtility.SetDirty(demoData);
            }

            if (display.GetComponent<StandbyJsbsimDataDriver>() == null)
            {
                display.AddComponent<StandbyJsbsimDataDriver>();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, B737PrefabPath);
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            B737FmsDisplayRig.SuppressEditorRebuild = false;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Standby] 已生成正式显示链路。位置可在屏幕污渍/Standby_Plane 上微调。");
    }

    private static GameObject EnsureDisplay(Transform rig, GameObject prefab, Camera renderCamera)
    {
        Transform existing = FindDirectChild(rig, "Standby_Display_Runtime");
        GameObject display = existing != null
            ? existing.gameObject
            : PrefabUtility.InstantiatePrefab(prefab, rig) as GameObject;
        display.name = "Standby_Display_Runtime";
        display.transform.localPosition = Vector3.zero;
        display.transform.localRotation = Quaternion.identity;
        display.transform.localScale = Vector3.one;
        SetLayerRecursively(display, StandbyLayer);

        Canvas canvas = display.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            throw new MissingComponentException("Standby.prefab 中没有 Canvas。");
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = renderCamera;
        canvas.planeDistance = 1f;

        // Standby 原始布局按 278×278 像素制作，让它等比例放大并铺满 512×512 输出纹理。
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(278f, 278f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(scaler);
        return display;
    }

    private static Camera EnsureCamera(Transform rig, RenderTexture renderTexture)
    {
        Transform existing = FindDirectChild(rig, "Standby_Camera");
        GameObject cameraObject = existing != null
            ? existing.gameObject
            : new GameObject("Standby_Camera", typeof(Camera));
        cameraObject.transform.SetParent(rig, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 1f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.cullingMask = 1 << StandbyLayer;
        camera.targetTexture = renderTexture;
        camera.enabled = true;
        EditorUtility.SetDirty(camera);
        return camera;
    }

    private static void EnsurePhysicalPlane(Transform root, Material material)
    {
        Transform screenDirt = FindByName(root, "屏幕污渍");
        Transform reference = FindDirectChild(screenDirt, "EICAS1_Plane");
        if (screenDirt == null || reference == null)
        {
            throw new MissingReferenceException("未找到屏幕污渍或 EICAS1_Plane 参考平面。");
        }

        Transform existing = FindDirectChild(screenDirt, "Standby_Plane");
        GameObject plane;
        if (existing == null)
        {
            plane = Object.Instantiate(reference.gameObject, screenDirt);
            plane.name = "Standby_Plane";
            plane.transform.localPosition = new Vector3(3.0025f, 3.101f, -30.747f);
            plane.transform.localRotation = reference.localRotation;
            plane.transform.localScale = new Vector3(0.0065f, reference.localScale.y, 0.0065f);
        }
        else
        {
            // 已存在时保留用户在 Inspector 中微调过的位置、角度和比例。
            plane = existing.gameObject;
        }

        MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        plane.SetActive(true);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(plane.transform);
    }

    private static RenderTexture EnsureRenderTexture()
    {
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (texture == null)
        {
            texture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32) { name = "Standby" };
            AssetDatabase.CreateAsset(texture, RenderTexturePath);
        }

        texture.useMipMap = false;
        texture.autoGenerateMips = false;
        texture.antiAliasing = 1;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static Material EnsureMaterial(RenderTexture texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Standby" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f, 1f));
        material.SetFloat("_Smoothness", 0.35f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.EnableKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_EMISSION"));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureLayer(int index, string layerName)
    {
        Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        SerializedObject serialized = new SerializedObject(tagManager);
        SerializedProperty layer = serialized.FindProperty("layers").GetArrayElementAtIndex(index);
        if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != layerName)
        {
            throw new UnityException("Layer " + index + " 已被占用：" + layer.stringValue);
        }

        layer.stringValue = layerName;
        serialized.ApplyModifiedProperties();
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
        {
            return child;
        }

        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        }
        return null;
    }

    private static Transform FindByName(Transform root, string name)
    {
        if (root == null || root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindByName(root.GetChild(i), name);
            if (found != null) return found;
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
}

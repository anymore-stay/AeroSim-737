using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class B737PFDDisplayRigTests
{
    private const string B737PrefabPath = "Assets/Aircraft/B737/Prefabs/B737.prefab";
    private const string LeftRenderTexturePath = "Assets/Aircraft/B737/Textures/PFD_Left.renderTexture";
    private const string RightRenderTexturePath = "Assets/Aircraft/B737/Textures/PFD_Right.renderTexture";
    private const string LeftMaterialPath = "Assets/Aircraft/B737/Materials/PFD_Left.mat";
    private const string RightMaterialPath = "Assets/Aircraft/B737/Materials/PFD_Right.mat";

    [Test]
    public void LeftAndRightPfdRenderChainsExistAndAreIndependent()
    {
        RenderTexture leftTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(LeftRenderTexturePath);
        RenderTexture rightTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RightRenderTexturePath);

        Assert.That(leftTexture, Is.Not.Null, "缺少左侧 PFD RenderTexture。");
        Assert.That(rightTexture, Is.Not.Null, "缺少右侧 PFD RenderTexture。");
        Assert.That(leftTexture, Is.Not.SameAs(rightTexture));
        Assert.That(leftTexture.width, Is.EqualTo(512));
        Assert.That(leftTexture.height, Is.EqualTo(512));
        Assert.That(rightTexture.width, Is.EqualTo(512));
        Assert.That(rightTexture.height, Is.EqualTo(512));

        GameObject root = PrefabUtility.LoadPrefabContents(B737PrefabPath);
        try
        {
            ValidateSide(root.transform, "PFD_Left_Rig", "PFD_Display_Left", "PFD_Left_Camera", 10, leftTexture);
            ValidateSide(root.transform, "PFD_Right_Rig", "PFD_Display_Right", "PFD_Right_Camera", 11, rightTexture);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void PhysicalPfdPlanesArePlacedUnderScreenDirtAndUseDedicatedTextures()
    {
        RenderTexture leftTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(LeftRenderTexturePath);
        RenderTexture rightTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RightRenderTexturePath);
        GameObject root = PrefabUtility.LoadPrefabContents(B737PrefabPath);

        try
        {
            Transform screenDirt = FindByName(root.transform, "屏幕污渍");
            Assert.That(screenDirt, Is.Not.Null, "未找到驾驶舱屏幕污渍节点。");

            ValidatePlane(screenDirt, "PFD_Left_Plane", leftTexture);
            ValidatePlane(screenDirt, "PFD_Right_Plane", rightTexture);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void PfdMaterialsUseLitGlassSettingsAndKeepIndependentTextures()
    {
        RenderTexture leftTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(LeftRenderTexturePath);
        RenderTexture rightTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RightRenderTexturePath);
        Material leftMaterial = AssetDatabase.LoadAssetAtPath<Material>(LeftMaterialPath);
        Material rightMaterial = AssetDatabase.LoadAssetAtPath<Material>(RightMaterialPath);

        Assert.That(leftTexture, Is.Not.Null, "缺少左侧 PFD RenderTexture。");
        Assert.That(rightTexture, Is.Not.Null, "缺少右侧 PFD RenderTexture。");
        Assert.That(leftMaterial, Is.Not.Null, "缺少左侧 PFD 材质。");
        Assert.That(rightMaterial, Is.Not.Null, "缺少右侧 PFD 材质。");

        ValidateGlassMaterial(leftMaterial, leftTexture);
        ValidateGlassMaterial(rightMaterial, rightTexture);

        Assert.That(leftMaterial.GetTexture("_BaseMap"), Is.Not.SameAs(rightMaterial.GetTexture("_BaseMap")));
        Assert.That(leftMaterial.GetTexture("_MainTex"), Is.Not.SameAs(rightMaterial.GetTexture("_MainTex")));
        Assert.That(leftMaterial.GetTexture("_EmissionMap"), Is.Not.SameAs(rightMaterial.GetTexture("_EmissionMap")));
    }

    [Test]
    public void EnsureMaterialMigratesTransparentUnlitAssetToOpaqueLitIdempotently()
    {
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(LeftRenderTexturePath);
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        Assert.That(texture, Is.Not.Null, "缺少用于迁移测试的 PFD RenderTexture。");
        Assert.That(unlitShader, Is.Not.Null, "缺少 URP Unlit Shader。");

        string temporaryPath = "Assets/Aircraft/B737/Materials/PFD_TransparentMigration_"
            + System.Guid.NewGuid().ToString("N")
            + ".mat";
        Material transparentMaterial = new Material(unlitShader) { name = "PFD_TransparentMigration" };
        ConfigureTransparentMaterial(transparentMaterial);

        try
        {
            AssetDatabase.CreateAsset(transparentMaterial, temporaryPath);

            Material firstResult = InvokeEnsureMaterial(temporaryPath, transparentMaterial.name, texture);
            ValidateOpaqueMaterialState(firstResult, texture);

            Material secondResult = InvokeEnsureMaterial(temporaryPath, transparentMaterial.name, texture);
            Assert.That(secondResult, Is.SameAs(firstResult), "重复迁移必须复用同一份材质资产。");
            ValidateOpaqueMaterialState(secondResult, texture);
        }
        finally
        {
            AssetDatabase.DeleteAsset(temporaryPath);
        }
    }

    private static void ValidateSide(
        Transform root,
        string rigName,
        string displayName,
        string cameraName,
        int expectedLayer,
        RenderTexture expectedTexture)
    {
        Transform rigRoot = FindByName(root, "B737_PFD_Rig");
        Assert.That(rigRoot, Is.Not.Null, "缺少 B737_PFD_Rig。");

        Transform sideRig = FindByName(rigRoot, rigName);
        Transform display = FindByName(sideRig, displayName);
        Transform cameraTransform = FindByName(sideRig, cameraName);

        Assert.That(sideRig, Is.Not.Null, "缺少 " + rigName + "。");
        Assert.That(display, Is.Not.Null, "缺少 " + displayName + "。");
        Assert.That(cameraTransform, Is.Not.Null, "缺少 " + cameraName + "。");
        Assert.That(display.gameObject.layer, Is.EqualTo(expectedLayer));

        Camera renderCamera = cameraTransform.GetComponent<Camera>();
        Canvas canvas = display.GetComponentInChildren<Canvas>(true);
        PFDJsbsimDataDriver driver = display.GetComponent<PFDJsbsimDataDriver>();

        Assert.That(renderCamera, Is.Not.Null);
        Assert.That(renderCamera.targetTexture, Is.SameAs(expectedTexture));
        Assert.That(renderCamera.cullingMask, Is.EqualTo(1 << expectedLayer));
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
        Assert.That(canvas.worldCamera, Is.SameAs(renderCamera));
        Assert.That(driver, Is.Not.Null, "正式 PFD 实例缺少 JSBSim 数据驱动器。");

        AssertSimulatorDisabled<PFDAirspeedTapeSimulator>(display);
        AssertSimulatorDisabled<PFDAltitudeTapeSimulator>(display);
        AssertSimulatorDisabled<PFDAttitudeSimulator>(display);
        AssertSimulatorDisabled<PFDHeadingRoseSimulator>(display);
        AssertSimulatorDisabled<PFDAngleOfAttackGaugeSimulator>(display);
        AssertSimulatorDisabled<PFDVerticalSpeedIndicatorSimulator>(display);

        Transform preview = FindByName(display, "PFD_PreviewGuide");
        Transform final = FindByName(display, "PFD_Final");
        Assert.That(preview, Is.Not.Null);
        Assert.That(final, Is.Not.Null);
        Assert.That(preview.gameObject.activeSelf, Is.False);
        Assert.That(final.gameObject.activeSelf, Is.True);
    }

    private static void ValidatePlane(Transform screenDirt, string planeName, RenderTexture expectedTexture)
    {
        Transform plane = FindByName(screenDirt, planeName);
        Assert.That(plane, Is.Not.Null, "缺少 " + planeName + "。");
        Assert.That(plane.parent, Is.SameAs(screenDirt));

        MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.sharedMaterial, Is.Not.Null);
        Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(expectedTexture));
    }

    private static void ValidateGlassMaterial(Material material, RenderTexture expectedTexture)
    {
        Assert.That(material.shader, Is.Not.Null);
        Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
        Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(expectedTexture));
        Assert.That(material.GetTexture("_MainTex"), Is.SameAs(expectedTexture));
        Assert.That(material.GetTexture("_EmissionMap"), Is.SameAs(expectedTexture));
        Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(material.GetFloat("_EnvironmentReflections"), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(material.GetFloat("_SpecularHighlights"), Is.EqualTo(1f).Within(0.0001f));
        LocalKeyword emissionKeyword = new LocalKeyword(material.shader, "_EMISSION");
        Assert.That(material.IsKeywordEnabled(emissionKeyword), Is.True, material.name + " 未启用 _EMISSION。");

        Color emissionColor = material.GetColor("_EmissionColor");
        Assert.That(emissionColor.r, Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(emissionColor.g, Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(emissionColor.b, Is.EqualTo(0.55f).Within(0.001f));
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Blend", 1f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHAMODULATE_ON");
    }

    private static Material InvokeEnsureMaterial(string path, string assetName, RenderTexture texture)
    {
        System.Reflection.MethodInfo ensureMaterial = typeof(B737PFDDisplayRigEditorUtility).GetMethod(
            "EnsureMaterial",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(ensureMaterial, Is.Not.Null, "未找到材质迁移方法 EnsureMaterial。");

        return (Material)ensureMaterial.Invoke(null, new object[] { path, assetName, texture });
    }

    private static void ValidateOpaqueMaterialState(Material material, RenderTexture expectedTexture)
    {
        Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
        Assert.That(material.GetFloat("_Surface"), Is.EqualTo(0f));
        Assert.That(material.GetFloat("_AlphaClip"), Is.EqualTo(0f));
        Assert.That(material.GetFloat("_Blend"), Is.EqualTo(0f));
        Assert.That(material.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.One));
        Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.Zero));
        Assert.That(material.GetFloat("_SrcBlendAlpha"), Is.EqualTo((float)BlendMode.One));
        Assert.That(material.GetFloat("_DstBlendAlpha"), Is.EqualTo((float)BlendMode.Zero));
        Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1f));
        SerializedObject serializedMaterial = new SerializedObject(material);
        Assert.That(serializedMaterial.FindProperty("m_CustomRenderQueue").intValue, Is.EqualTo(-1));
        Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Opaque"));
        Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.False);
        Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.False);
        Assert.That(material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"), Is.False);
        Assert.That(material.IsKeywordEnabled("_ALPHAMODULATE_ON"), Is.False);
        Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(expectedTexture));
        Assert.That(material.GetTexture("_MainTex"), Is.SameAs(expectedTexture));
        Assert.That(material.GetTexture("_EmissionMap"), Is.SameAs(expectedTexture));
    }

    private static void AssertSimulatorDisabled<T>(Transform display) where T : Behaviour
    {
        T simulator = display.GetComponent<T>();
        Assert.That(simulator, Is.Not.Null, "正式实例缺少可保留的模拟器 " + typeof(T).Name + "。");
        Assert.That(simulator.enabled, Is.False, typeof(T).Name + " 在正式 PFD 实例中必须禁用。");
    }

    private static Transform FindByName(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

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
}

using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class B737NavigationLights : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private bool lightsOn = true;
    [SerializeField] private bool enableKeyboardControl = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.N;

    [Header("Movable Light Points")]
    [SerializeField] private Transform leftRedPoint;
    [SerializeField] private Transform rightGreenPoint;
    [SerializeField] private Transform tailWhitePoint;

    [Header("Light Point Parents")]
    [SerializeField] private Transform leftRedParent;
    [SerializeField] private Transform rightGreenParent;
    [SerializeField] private Transform tailWhiteParent;

    [Header("Default Local Positions")]
    [SerializeField] private Vector3 leftRedDefaultLocalPosition = new Vector3(-8.2f, 0.05f, -1.2f);
    [SerializeField] private Vector3 rightGreenDefaultLocalPosition = new Vector3(8.2f, 0.05f, -1.2f);
    [SerializeField] private Vector3 tailWhiteDefaultLocalPosition = new Vector3(0f, 1.05f, 20.8f);

    [Header("Default Local Scales")]
    [SerializeField] private Vector3 leftRedDefaultLocalScale = Vector3.one;
    [SerializeField] private Vector3 rightGreenDefaultLocalScale = Vector3.one;
    [SerializeField] private Vector3 tailWhiteDefaultLocalScale = Vector3.one;

    [Header("Initial Light Defaults")]
    [SerializeField] private Color leftRedColor = new Color(1f, 0.05f, 0.025f, 1f);
    [SerializeField] private Color rightGreenColor = new Color(0.02f, 0.95f, 0.18f, 1f);
    [SerializeField] private Color tailWhiteColor = new Color(1f, 0.96f, 0.9f, 1f);
    [SerializeField, HideInInspector, Min(0f)] private float pointIntensity = 0.28f;
    [SerializeField, HideInInspector, Min(0.1f)] private float pointRange = 3.2f;
    [Header("Per-Light Defaults")]
    [SerializeField, Min(0f)] private float leftRedIntensity = 0.28f;
    [SerializeField, Min(0f)] private float rightGreenIntensity = 0.28f;
    [SerializeField, Min(0f)] private float tailWhiteIntensity = 0.28f;
    [SerializeField, Min(0.1f)] private float leftRedRange = 3.2f;
    [SerializeField, Min(0.1f)] private float rightGreenRange = 3.2f;
    [SerializeField, Min(0.1f)] private float tailWhiteRange = 3.2f;

    [Header("Lens Visuals")]
    [SerializeField, Min(0.01f)] private float lensScale = 0.06f;
    [SerializeField, Min(0.01f)] private float glowScale = 0.24f;
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.16f;
    [SerializeField, Min(0f)] private float emissionIntensity = 5.5f;

    public bool LightsOn => lightsOn;

    public void SetLightsOn(bool enabled)
    {
        lightsOn = enabled;
        ApplyLights();
    }

    [ContextMenu("Create Missing Navigation Light Points")]
    public void CreateMissingLightPoints()
    {
        EnsureRig();
        ApplyLights();
    }

    private void Awake()
    {
        EnsureRig();
        ApplyLights();
    }

    private void OnEnable()
    {
        EnsureRig();
        ApplyLights();
    }

    private void Update()
    {
        EnsureRig();

        if (Application.isPlaying && enableKeyboardControl && Input.GetKeyDown(toggleKey))
        {
            lightsOn = !lightsOn;
        }

        ApplyLights();
    }

    private void OnValidate()
    {
        pointIntensity = Mathf.Max(0f, pointIntensity);
        pointRange = Mathf.Max(0.1f, pointRange);
        leftRedIntensity = Mathf.Max(0f, leftRedIntensity);
        rightGreenIntensity = Mathf.Max(0f, rightGreenIntensity);
        tailWhiteIntensity = Mathf.Max(0f, tailWhiteIntensity);
        leftRedRange = Mathf.Max(0.1f, leftRedRange);
        rightGreenRange = Mathf.Max(0.1f, rightGreenRange);
        tailWhiteRange = Mathf.Max(0.1f, tailWhiteRange);
        lensScale = Mathf.Max(0.01f, lensScale);
        glowScale = Mathf.Max(0.01f, glowScale);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
    }

    private void EnsureRig()
    {
        if (!B737ExteriorLightUtility.CanModifySceneObject(this))
        {
            return;
        }

        Transform root = B737ExteriorLightUtility.FindOrCreateChild(transform, "B737_NavigationLights", Vector3.zero);
        leftRedPoint = B737ExteriorLightUtility.FindOrCreatePoint(GetPointParent(leftRedParent, root), leftRedPoint, "NAV_Left_Red_Movable", leftRedDefaultLocalPosition, leftRedDefaultLocalScale);
        rightGreenPoint = B737ExteriorLightUtility.FindOrCreatePoint(GetPointParent(rightGreenParent, root), rightGreenPoint, "NAV_Right_Green_Movable", rightGreenDefaultLocalPosition, rightGreenDefaultLocalScale);
        tailWhitePoint = B737ExteriorLightUtility.FindOrCreatePoint(GetPointParent(tailWhiteParent, root), tailWhitePoint, "NAV_Tail_White_Movable", tailWhiteDefaultLocalPosition, tailWhiteDefaultLocalScale);
    }

    private void ApplyLights()
    {
        ApplyNavigationPoint(leftRedPoint, leftRedColor, leftRedIntensity, leftRedRange);
        ApplyNavigationPoint(rightGreenPoint, rightGreenColor, rightGreenIntensity, rightGreenRange);
        ApplyNavigationPoint(tailWhitePoint, tailWhiteColor, tailWhiteIntensity, tailWhiteRange);
    }

    private Transform GetPointParent(Transform preferredParent, Transform fallbackParent)
    {
        return preferredParent != null ? preferredParent : fallbackParent;
    }

    private void ApplyNavigationPoint(Transform point, Color color, float intensity, float range)
    {
        if (point == null)
        {
            return;
        }

        Light light = B737ExteriorLightUtility.EnsureLight(point, LightType.Point, color, intensity, range, 0f);
        B737ExteriorLightUtility.SetLightEnabled(light, lightsOn);
        B737ExteriorLightUtility.EnsureLensVisual(point, color, lensScale, glowScale, glowAlpha, emissionIntensity, lightsOn);
    }

    private void OnDrawGizmosSelected()
    {
        B737ExteriorLightUtility.DrawPointGizmo(leftRedPoint, leftRedDefaultLocalPosition, transform, leftRedColor, 0.2f);
        B737ExteriorLightUtility.DrawPointGizmo(rightGreenPoint, rightGreenDefaultLocalPosition, transform, rightGreenColor, 0.2f);
        B737ExteriorLightUtility.DrawPointGizmo(tailWhitePoint, tailWhiteDefaultLocalPosition, transform, tailWhiteColor, 0.2f);
    }
}

internal static class B737ExteriorLightUtility
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public static bool CanModifySceneObject(MonoBehaviour owner)
    {
        return owner != null && owner.gameObject != null && owner.gameObject.scene.IsValid();
    }

    public static Transform FindOrCreateChild(Transform parent, string childName, Vector3 defaultLocalPosition)
    {
        return FindOrCreateChild(parent, childName, defaultLocalPosition, Vector3.one);
    }

    public static Transform FindOrCreateChild(
        Transform parent,
        string childName,
        Vector3 defaultLocalPosition,
        Vector3 defaultLocalScale)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childGo = new GameObject(childName);
        child = childGo.transform;
        child.SetParent(parent, false);
        child.localPosition = defaultLocalPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = defaultLocalScale;
        return child;
    }

    public static Transform FindOrCreatePoint(
        Transform root,
        Transform current,
        string pointName,
        Vector3 defaultLocalPosition)
    {
        return FindOrCreatePoint(root, current, pointName, defaultLocalPosition, Vector3.one);
    }

    public static Transform FindOrCreatePoint(
        Transform root,
        Transform current,
        string pointName,
        Vector3 defaultLocalPosition,
        Vector3 defaultLocalScale)
    {
        if (current != null)
        {
            return current;
        }

        return FindOrCreateChild(root, pointName, defaultLocalPosition, defaultLocalScale);
    }

    public static Light EnsureLight(
        Transform point,
        LightType type,
        Color defaultColor,
        float defaultIntensity,
        float defaultRange,
        float defaultSpotAngle)
    {
        Light light = point.GetComponent<Light>();
        if (light == null)
        {
            // 默认值只在首次创建时写入，避免覆盖 Inspector 里手动调好的 Light 参数。
            light = point.gameObject.AddComponent<Light>();
            light.type = type;
            light.color = defaultColor;
            light.intensity = Mathf.Max(0f, defaultIntensity);
            light.range = Mathf.Max(0.1f, defaultRange);
            if (type == LightType.Spot)
            {
                light.spotAngle = Mathf.Clamp(defaultSpotAngle, 1f, 179f);
            }
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        if (light.type != type)
        {
            light.type = type;
        }

        return light;
    }

    public static void SetLightEnabled(Light light, bool enabled)
    {
        if (light == null)
        {
            return;
        }

        light.enabled = enabled;
    }

    public static void EnsureLensVisual(
        Transform point,
        Color color,
        float lensScale,
        float glowScale,
        float glowAlpha,
        float emissionIntensity,
        bool enabled)
    {
        Renderer lensRenderer = EnsureSphereRenderer(point, "Lens", Mathf.Max(0.01f, lensScale));
        Renderer glowRenderer = EnsureSphereRenderer(point, "SoftGlow", Mathf.Max(0.01f, glowScale));

        ConfigureVisualRenderer(lensRenderer, color, 1f, emissionIntensity, false, enabled);
        ConfigureVisualRenderer(glowRenderer, color, glowAlpha, emissionIntensity * 0.35f, true, enabled);
    }

    public static Renderer EnsureSphereRenderer(Transform parent, string visualName, float scale)
    {
        Transform existing = parent.Find(visualName);
        GameObject visualObject;
        if (existing == null)
        {
            visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualObject.name = visualName;
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            visualObject = existing.gameObject;
        }

        visualObject.transform.localScale = Vector3.one * scale;

        Collider collider = visualObject.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyObject(collider);
        }

        Renderer renderer = visualObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return renderer;
    }

    public static void ConfigureVisualRenderer(
        Renderer renderer,
        Color color,
        float alpha,
        float emissionIntensity,
        bool additive,
        bool enabled)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.enabled = enabled;
        Material material = renderer.sharedMaterial;
        if (material == null || !material.name.StartsWith("B737_RuntimeLight", System.StringComparison.Ordinal))
        {
            material = CreateVisualMaterial(additive);
            renderer.sharedMaterial = material;
        }

        Color visibleColor = color;
        visibleColor.a = Mathf.Clamp01(alpha);
        SetMaterialColor(material, visibleColor, color * Mathf.Max(0f, emissionIntensity));
    }

    private static Material CreateVisualMaterial(bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = additive ? "B737_RuntimeLight_AdditiveGlow" : "B737_RuntimeLight_Lens"
        };

        if (additive)
        {
            ConfigureTransparentAdditive(material);
        }

        return material;
    }

    private static void SetMaterialColor(Material material, Color visibleColor, Color emissionColor)
    {
        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, visibleColor);
        }
        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, visibleColor);
        }
        if (material.HasProperty(EmissionColorId))
        {
            material.SetColor(EmissionColorId, emissionColor);
        }
    }

    private static void ConfigureTransparentAdditive(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 1f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.One);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    public static Quaternion LookRotationOrIdentity(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    public static void DrawPointGizmo(
        Transform point,
        Vector3 fallbackLocalPosition,
        Transform owner,
        Color color,
        float radius)
    {
        Gizmos.color = color;
        Vector3 position = point != null ? point.position : owner.TransformPoint(fallbackLocalPosition);
        Gizmos.DrawWireSphere(position, radius);
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
#if UNITY_EDITOR
        else
        {
            // Unity 在 OnValidate/渲染回调/域重载期间禁止立即销毁，延迟到编辑器安全时机处理。
            EditorApplication.delayCall += () =>
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            };
        }
#else
        else
        {
            Object.DestroyImmediate(target);
        }
#endif
    }
}

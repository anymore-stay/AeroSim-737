using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a self-contained FMS display render rig and applies it to the two CDU screen meshes.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class B737FmsDisplayRig : MonoBehaviour
{
    [Header("Assets")]
    [SerializeField] private RenderTexture targetTexture;
    [SerializeField] private Material displayMaterial;

    [Header("Rig")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private string fmsLayerName = "FMS";
    [SerializeField] private int fallbackLayer = 12;
    [SerializeField] private Vector2Int textureSize = new Vector2Int(768, 1024);
    [SerializeField] private bool placeRigObjectsAtSceneRoot = true;
    [SerializeField] private bool keepScreenMaterialBound = true;
    [SerializeField] private bool refreshScreenBindingsAtRuntime = true;
    [SerializeField] private bool driveScreenTextureWithPropertyBlock = true;

    [Header("Scene Objects")]
    [SerializeField] private Camera fmsCamera;
    [SerializeField] private Canvas fmsCanvas;
    [SerializeField] private B737FmsDisplay fmsDisplay;

    [Header("Screen Meshes")]
    [SerializeField] private MeshRenderer leftScreenRenderer;
    [SerializeField] private MeshRenderer rightScreenRenderer;
    [SerializeField] private string leftScreenName = "FMS_screens__ImpMesh.000_x345_69206";
    [SerializeField] private string rightScreenName = "FMS_screens__ImpMesh.001_x345_12570";
    [SerializeField] private bool autoBindScreenRenderers = true;

    [Header("Display Planes")]
    [SerializeField] private bool createDisplayPlanes = true;
    [SerializeField] private bool driveGeneratedPlaneTransform = true;
    [SerializeField] private bool hideSourceScreenRenderers = true;
    [SerializeField] private float displayPlaneOffset = -0.001f;
    [SerializeField] private Vector3 displayPlaneLocalPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 displayPlaneLocalEulerOffset = new Vector3(210f, 0f, 180f);
    [SerializeField] private Vector2 displayPlaneScaleMultiplier = new Vector2(1f, 1.18f);
    [SerializeField] private bool flipDisplayPlaneX;
    [SerializeField] private bool flipDisplayPlaneY;
    [SerializeField] private MeshRenderer leftDisplayPlaneRenderer;
    [SerializeField] private MeshRenderer rightDisplayPlaneRenderer;
    [SerializeField] private string leftDisplayPlaneName = "FMS_LeftPlane";
    [SerializeField] private string rightDisplayPlaneName = "FMS_RightPlane";

    [Header("3D Buttons")]
    [SerializeField] private bool createButtonHitboxes = true;
    [SerializeField] private bool driveGeneratedButtonTransform = true;
    [SerializeField] private bool ensureClickRaycaster = true;
    [SerializeField] private string buttonLayerName = "FMSButton";
    [SerializeField] private int buttonFallbackLayer = 13;
    [SerializeField] private float buttonClickMaxDistance = 20f;
    [SerializeField] private float buttonHitboxOffset = 0.01f;
    [SerializeField] private Vector2 buttonHitboxSize = new Vector2(0.24f, 0.145f);
    [SerializeField] private float buttonHitboxSideOffset = 0.13f;
    [SerializeField] private float buttonHitboxVerticalOffset = -0.065f;
    [SerializeField] private float buttonHitboxDepth = 0.08f;
    [SerializeField] private float[] buttonHitboxRows = { 0.175f, 0.32f, 0.465f, 0.61f, 0.755f, 0.9f };
    [SerializeField] private string leftButtonRootName = "FMS_LeftButtons";
    [SerializeField] private string rightButtonRootName = "FMS_RightButtons";

    private bool ownsRuntimeTexture;
    private bool ownsRuntimeMaterial;
    private MaterialPropertyBlock screenPropertyBlock;
    private bool isEnsuringRig;

    public RenderTexture TargetTexture => targetTexture;
    public Material DisplayMaterial => displayMaterial;
    public B737FmsDisplay FmsDisplay => fmsDisplay;

    public void SetAssets(RenderTexture renderTextureAsset, Material materialAsset)
    {
        targetTexture = renderTextureAsset;
        displayMaterial = materialAsset;
        ownsRuntimeTexture = false;
        ownsRuntimeMaterial = false;
    }

    public void SetScreenRenderers(MeshRenderer leftRenderer, MeshRenderer rightRenderer)
    {
        leftScreenRenderer = leftRenderer;
        rightScreenRenderer = rightRenderer;
    }

    private void Awake()
    {
        if (buildOnAwake)
        {
            EnsureRig();
        }
    }

    private void Start()
    {
        EnsureRig();
    }

    private void OnEnable()
    {
        EnsureRig();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            EnsureRig();
            RenderPreview();
            return;
        }

        if (!keepScreenMaterialBound)
        {
            return;
        }

        if (refreshScreenBindingsAtRuntime)
        {
            AutoBindScreens(true);
        }
        else
        {
            AutoBindScreens();
        }

        ApplyTextureToMaterial();
        EnsureDisplayPlanes();
        EnsureButtonHitboxes();
        EnsureClickRaycaster();
    }

    private void OnValidate()
    {
        textureSize.x = Mathf.Max(64, textureSize.x);
        textureSize.y = Mathf.Max(64, textureSize.y);

        if (isActiveAndEnabled)
        {
            EnsureRig();
            RenderPreview();
        }
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (ownsRuntimeTexture && targetTexture != null)
        {
            targetTexture.Release();
            Destroy(targetTexture);
        }

        if (ownsRuntimeMaterial && displayMaterial != null)
        {
            Destroy(displayMaterial);
        }
    }

    [ContextMenu("Ensure FMS Rig")]
    public void EnsureRig()
    {
        if (isEnsuringRig)
        {
            return;
        }

        isEnsuringRig = true;
        try
        {
            int fmsLayer = ResolveLayer();
            EnsureRenderTexture();
            EnsureMaterial();
            EnsureCamera(fmsLayer);
            EnsureCanvas(fmsLayer);
            EnsureDisplay(fmsLayer);
            AutoBindScreens(true);
            ApplyTextureToMaterial();
            EnsureDisplayPlanes();
            EnsureButtonHitboxes();
            EnsureClickRaycaster();
            RenderPreview();
        }
        finally
        {
            isEnsuringRig = false;
        }
    }

    private void RenderPreview()
    {
        if (Application.isPlaying || fmsCamera == null || targetTexture == null)
        {
            return;
        }

        fmsCamera.targetTexture = targetTexture;
        fmsCamera.Render();
    }

    private void EnsureRenderTexture()
    {
        if (targetTexture != null)
        {
            return;
        }

        targetTexture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32)
        {
            name = "B737_FMS_RuntimeRT",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false
        };
        targetTexture.Create();
        ownsRuntimeTexture = true;
    }

    private void EnsureMaterial()
    {
        if (displayMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        displayMaterial = new Material(shader)
        {
            name = "B737_FMS_RuntimeMaterial"
        };
        ownsRuntimeMaterial = true;
    }

    private void EnsureCamera(int fmsLayer)
    {
        if (fmsCamera == null)
        {
            Transform child = FindRigObject("FMS_Camera");
            if (child != null)
            {
                fmsCamera = child.GetComponent<Camera>();
            }
        }

        if (fmsCamera == null)
        {
            GameObject cameraGo = new GameObject("FMS_Camera");
            cameraGo.transform.SetParent(GetRigObjectParent(), false);
            fmsCamera = cameraGo.AddComponent<Camera>();
        }

        EnsureRigObjectParent(fmsCamera.transform);
        fmsCamera.gameObject.layer = fmsLayer;
        fmsCamera.enabled = true;
        fmsCamera.clearFlags = CameraClearFlags.SolidColor;
        fmsCamera.backgroundColor = Color.black;
        fmsCamera.orthographic = true;
        fmsCamera.orthographicSize = 5f;
        fmsCamera.nearClipPlane = 0.1f;
        fmsCamera.farClipPlane = 20f;
        fmsCamera.cullingMask = 1 << fmsLayer;
        fmsCamera.targetTexture = targetTexture;
        fmsCamera.allowHDR = true;
        fmsCamera.allowMSAA = true;
    }

    private void EnsureCanvas(int fmsLayer)
    {
        if (fmsCanvas == null)
        {
            Transform child = FindRigObject("FMS_Canvas");
            if (child != null)
            {
                fmsCanvas = child.GetComponent<Canvas>();
            }
        }

        if (fmsCanvas == null)
        {
            GameObject canvasGo = new GameObject("FMS_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(GetRigObjectParent(), false);
            fmsCanvas = canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        EnsureRigObjectParent(fmsCanvas.transform);
        fmsCanvas.gameObject.layer = fmsLayer;
        fmsCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        fmsCanvas.worldCamera = fmsCamera;
        fmsCanvas.planeDistance = 1f;
        fmsCanvas.pixelPerfect = false;

        CanvasScaler scaler = fmsCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = fmsCanvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(textureSize.x, textureSize.y);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureDisplay(int fmsLayer)
    {
        if (fmsDisplay == null)
        {
            Transform child = fmsCanvas.transform.Find("FMS_Display");
            if (child != null)
            {
                fmsDisplay = child.GetComponent<B737FmsDisplay>();
            }
        }

        if (fmsDisplay == null)
        {
            GameObject displayGo = new GameObject("FMS_Display", typeof(RectTransform));
            displayGo.transform.SetParent(fmsCanvas.transform, false);
            fmsDisplay = displayGo.AddComponent<B737FmsDisplay>();
        }

        fmsDisplay.SetDisplaySize(new Vector2(textureSize.x, textureSize.y));
        SetLayerRecursive(fmsDisplay.gameObject, fmsLayer);
    }

    private void AutoBindScreens(bool force = false)
    {
        if (!autoBindScreenRenderers)
        {
            return;
        }

        if (leftScreenRenderer == null || force)
        {
            MeshRenderer renderer = FindMeshRendererByName(leftScreenName);
            if (renderer != null)
            {
                leftScreenRenderer = renderer;
            }
        }

        if (rightScreenRenderer == null || force)
        {
            MeshRenderer renderer = FindMeshRendererByName(rightScreenName);
            if (renderer != null)
            {
                rightScreenRenderer = renderer;
            }
        }
    }

    private MeshRenderer FindMeshRendererByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
            {
                return children[i].GetComponent<MeshRenderer>();
            }
        }

        return null;
    }

    private void ApplyTextureToMaterial()
    {
        if (displayMaterial == null || targetTexture == null)
        {
            return;
        }

        if (displayMaterial.HasProperty("_BaseMap"))
        {
            displayMaterial.SetTexture("_BaseMap", targetTexture);
        }

        if (displayMaterial.HasProperty("_MainTex"))
        {
            displayMaterial.SetTexture("_MainTex", targetTexture);
        }

        if (displayMaterial.HasProperty("_EmissionMap"))
        {
            displayMaterial.SetTexture("_EmissionMap", targetTexture);
        }

        if (displayMaterial.HasProperty("_EmissionColor"))
        {
            displayMaterial.SetColor("_EmissionColor", Color.white * 0.65f);
        }

        displayMaterial.EnableKeyword("_EMISSION");
        displayMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private void ApplyMaterialToScreens()
    {
        if (displayMaterial == null)
        {
            return;
        }

        if (leftScreenRenderer != null)
        {
            ApplyMaterialToRenderer(leftScreenRenderer);
            ApplyTexturePropertyBlock(leftScreenRenderer);
        }

        if (rightScreenRenderer != null)
        {
            ApplyMaterialToRenderer(rightScreenRenderer);
            ApplyTexturePropertyBlock(rightScreenRenderer);
        }
    }

    private void EnsureDisplayPlanes()
    {
        if (!createDisplayPlanes || displayMaterial == null)
        {
            return;
        }

        EnsureDisplayPlane(ref leftDisplayPlaneRenderer, leftDisplayPlaneName, leftScreenRenderer);
        EnsureDisplayPlane(ref rightDisplayPlaneRenderer, rightDisplayPlaneName, rightScreenRenderer);
    }

    private void EnsureButtonHitboxes()
    {
        if (!createButtonHitboxes || fmsDisplay == null)
        {
            return;
        }

        int buttonLayer = ResolveButtonLayer();
        EnsureButtonSet(leftDisplayPlaneRenderer, leftButtonRootName, B737FmsButton.ButtonType.LeftLine, -1f, buttonLayer);
        EnsureButtonSet(leftDisplayPlaneRenderer, rightButtonRootName, B737FmsButton.ButtonType.RightLine, 1f, buttonLayer);
        EnsureButtonSet(rightDisplayPlaneRenderer, leftButtonRootName, B737FmsButton.ButtonType.LeftLine, -1f, buttonLayer);
        EnsureButtonSet(rightDisplayPlaneRenderer, rightButtonRootName, B737FmsButton.ButtonType.RightLine, 1f, buttonLayer);
    }

    private void EnsureButtonSet(MeshRenderer displayPlane, string rootName, B737FmsButton.ButtonType type, float sideSign, int buttonLayer)
    {
        if (displayPlane == null)
        {
            return;
        }

        Transform root = displayPlane.transform.Find(rootName);
        if (root == null)
        {
            GameObject rootGo = new GameObject(rootName);
            rootGo.transform.SetParent(displayPlane.transform, false);
            root = rootGo.transform;
        }

        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        int rowCount = buttonHitboxRows != null && buttonHitboxRows.Length > 0 ? buttonHitboxRows.Length : 6;
        for (int i = 0; i < rowCount; i++)
        {
            int lineIndex = i + 1;
            string buttonName = type == B737FmsButton.ButtonType.LeftLine ? $"L{lineIndex}_Hitbox" : $"R{lineIndex}_Hitbox";
            Transform button = root.Find(buttonName);
            if (button == null)
            {
                GameObject buttonGo = new GameObject(buttonName);
                buttonGo.transform.SetParent(root, false);
                button = buttonGo.transform;
            }

            BoxCollider collider = button.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = button.gameObject.AddComponent<BoxCollider>();
            }

            B737FmsButton fmsButton = button.GetComponent<B737FmsButton>();
            if (fmsButton == null)
            {
                fmsButton = button.gameObject.AddComponent<B737FmsButton>();
            }

            fmsButton.Configure(fmsDisplay, type, lineIndex);

            if (driveGeneratedButtonTransform)
            {
                float row = buttonHitboxRows != null && i < buttonHitboxRows.Length ? buttonHitboxRows[i] : 0.16f + i * 0.13f;
                float localX = sideSign * (0.5f + Mathf.Abs(buttonHitboxSideOffset));
                float localY = 0.5f - row + buttonHitboxVerticalOffset;
                button.localPosition = new Vector3(localX, localY, -Mathf.Abs(buttonHitboxOffset));
                button.localRotation = Quaternion.identity;
                button.localScale = Vector3.one;
            }

            collider.size = new Vector3(Mathf.Max(0.01f, buttonHitboxSize.x), Mathf.Max(0.01f, buttonHitboxSize.y), Mathf.Max(0.01f, buttonHitboxDepth));
            collider.center = Vector3.zero;
            button.gameObject.layer = buttonLayer;
        }
    }

    private void EnsureClickRaycaster()
    {
        if (!ensureClickRaycaster)
        {
            return;
        }

        B737FmsClickRaycaster raycaster = GetComponent<B737FmsClickRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<B737FmsClickRaycaster>();
        }

        raycaster.Configure(null, buttonLayerName, buttonClickMaxDistance);
    }

    private void EnsureDisplayPlane(ref MeshRenderer planeRenderer, string planeName, MeshRenderer sourceRenderer)
    {
        if (sourceRenderer == null)
        {
            return;
        }

        if (planeRenderer == null)
        {
            Transform existing = sourceRenderer.transform.Find(planeName);
            if (existing != null)
            {
                planeRenderer = existing.GetComponent<MeshRenderer>();
            }
        }

        if (planeRenderer == null)
        {
            GameObject planeGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            planeGo.name = planeName;
            planeGo.transform.SetParent(sourceRenderer.transform, false);
            planeRenderer = planeGo.GetComponent<MeshRenderer>();

            Collider collider = planeGo.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyObject(collider);
            }
        }

        planeRenderer.sharedMaterial = displayMaterial;
        ApplyTexturePropertyBlock(planeRenderer);

        if (driveGeneratedPlaneTransform)
        {
            AlignPlaneToSourceRenderer(planeRenderer.transform, sourceRenderer);
        }

        if (hideSourceScreenRenderers)
        {
            sourceRenderer.enabled = false;
        }
    }

    private void AlignPlaneToSourceRenderer(Transform plane, MeshRenderer sourceRenderer)
    {
        Bounds localBounds = sourceRenderer.localBounds;
        Vector3 center = localBounds.center;
        Vector3 size = localBounds.size;
        float width = Mathf.Max(size.x * Mathf.Max(0.001f, Mathf.Abs(displayPlaneScaleMultiplier.x)), 0.001f);
        float height = Mathf.Max(size.y * Mathf.Max(0.001f, Mathf.Abs(displayPlaneScaleMultiplier.y)), 0.001f);
        if (flipDisplayPlaneX)
        {
            width = -width;
        }

        if (flipDisplayPlaneY)
        {
            height = -height;
        }

        plane.localPosition = center + Vector3.back * displayPlaneOffset + displayPlaneLocalPositionOffset;
        plane.localRotation = Quaternion.Euler(displayPlaneLocalEulerOffset);
        plane.localScale = new Vector3(width, height, 1f);
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void ApplyMaterialToRenderer(MeshRenderer renderer)
    {
        if (renderer == null || displayMaterial == null)
        {
            return;
        }

        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            renderer.sharedMaterial = displayMaterial;
            return;
        }

        bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != displayMaterial)
            {
                materials[i] = displayMaterial;
                changed = true;
            }
        }

        if (changed)
        {
            renderer.sharedMaterials = materials;
        }
    }

    private void ApplyTexturePropertyBlock(MeshRenderer renderer)
    {
        if (!driveScreenTextureWithPropertyBlock || renderer == null || targetTexture == null)
        {
            return;
        }

        if (screenPropertyBlock == null)
        {
            screenPropertyBlock = new MaterialPropertyBlock();
        }

        int materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
        for (int i = 0; i < materialCount; i++)
        {
            screenPropertyBlock.Clear();
            screenPropertyBlock.SetTexture("_BaseMap", targetTexture);
            screenPropertyBlock.SetTexture("_MainTex", targetTexture);
            screenPropertyBlock.SetTexture("_EmissionMap", targetTexture);
            screenPropertyBlock.SetColor("_BaseColor", Color.white);
            screenPropertyBlock.SetColor("_Color", Color.white);
            screenPropertyBlock.SetColor("_EmissionColor", Color.white);
            renderer.SetPropertyBlock(screenPropertyBlock, i);
        }
    }

    private int ResolveLayer()
    {
        int layer = LayerMask.NameToLayer(fmsLayerName);
        if (layer < 0)
        {
            layer = Mathf.Clamp(fallbackLayer, 0, 31);
        }

        return layer;
    }

    private int ResolveButtonLayer()
    {
        int layer = LayerMask.NameToLayer(buttonLayerName);
        if (layer < 0)
        {
            layer = Mathf.Clamp(buttonFallbackLayer, 0, 31);
        }

        return layer;
    }

    private Transform FindRigObject(string objectName)
    {
        if (!placeRigObjectsAtSceneRoot)
        {
            return transform.Find(objectName);
        }

        if (!gameObject.scene.IsValid())
        {
            return transform.Find(objectName);
        }

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i].transform;
            }
        }

        return transform.Find(objectName);
    }

    private Transform GetRigObjectParent()
    {
        return placeRigObjectsAtSceneRoot ? null : transform;
    }

    private void EnsureRigObjectParent(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Transform desiredParent = GetRigObjectParent();
        if (target.parent != desiredParent)
        {
            target.SetParent(desiredParent, false);
        }
    }

    private static void SetLayerRecursive(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        Transform t = target.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ND 装配层：创建 Camera + Canvas + RenderTexture，并把纹理贴到驾驶舱平面上。
/// </summary>
public class B737NavigationDisplayRig : MonoBehaviour
{
    [Header("Assets")]
    [SerializeField] private RenderTexture targetTexture;
    [SerializeField] private Material displayMaterial;

    [Header("Rig")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private string ndLayerName = "ND";
    [SerializeField] private int fallbackLayer = 8;
    [SerializeField] private int textureSize = 530;

    [Header("Scene Objects")]
    [SerializeField] private Camera ndCamera;
    [SerializeField] private Canvas ndCanvas;
    [SerializeField] private B737NavigationDisplay navigationDisplay;

    [Header("Display Plane")]
    [SerializeField] private bool createDisplayPlane = true;
    [Tooltip("只驱动本脚本创建的 ND_Plane 尺寸。已有驾驶舱屏幕 MeshRenderer 不会被自动缩放，避免拽歪模型。")]
    [SerializeField] private bool driveGeneratedPlaneTransform = true;
    [SerializeField] private Transform displayPlaneParent;
    [SerializeField] private MeshRenderer displayPlaneRenderer;
    [SerializeField] private Vector3 planeLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 planeLocalEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 planeLocalScale = new Vector3(0.053f, 1f, 0.053f);

    private bool ownsRuntimeTexture;
    private bool ownsRuntimeMaterial;

    public RenderTexture TargetTexture => targetTexture;
    public Material DisplayMaterial => displayMaterial;
    public B737NavigationDisplay NavigationDisplay => navigationDisplay;

    private void Awake()
    {
        if (buildOnAwake)
        {
            EnsureRig();
        }
    }

    private void OnValidate()
    {
        CaptureGeneratedPlaneTransform();
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
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
    }

    public void SetAssets(RenderTexture renderTextureAsset, Material materialAsset)
    {
        targetTexture = renderTextureAsset;
        displayMaterial = materialAsset;
        ownsRuntimeTexture = false;
        ownsRuntimeMaterial = false;
    }

    public void SetDisplayPlaneRenderer(MeshRenderer renderer)
    {
        displayPlaneRenderer = renderer;
    }

    public void EnsureRig()
    {
        int ndLayer = ResolveLayer();
        EnsureRenderTexture();
        EnsureMaterial();
        EnsureCamera(ndLayer);
        EnsureCanvas(ndLayer);
        EnsureDisplay(ndLayer);
        EnsurePlane();
        ApplyTextureToMaterial();
    }

    private void EnsureRenderTexture()
    {
        if (targetTexture != null)
        {
            return;
        }

        targetTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
        {
            name = "B737_ND_RuntimeRT",
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
            name = "B737_ND_RuntimeMaterial"
        };
        ownsRuntimeMaterial = true;
    }

    private void EnsureCamera(int ndLayer)
    {
        if (ndCamera == null)
        {
            Transform child = transform.Find("ND_Camera");
            if (child != null)
            {
                ndCamera = child.GetComponent<Camera>();
            }
        }

        if (ndCamera == null)
        {
            GameObject cameraGo = new GameObject("ND_Camera");
            cameraGo.transform.SetParent(transform, false);
            ndCamera = cameraGo.AddComponent<Camera>();
        }

        ndCamera.gameObject.layer = ndLayer;
        ndCamera.enabled = true;
        ndCamera.clearFlags = CameraClearFlags.SolidColor;
        ndCamera.backgroundColor = Color.black;
        ndCamera.orthographic = true;
        ndCamera.orthographicSize = 5f;
        ndCamera.nearClipPlane = 0.1f;
        ndCamera.farClipPlane = 20f;
        ndCamera.cullingMask = 1 << ndLayer;
        ndCamera.targetTexture = targetTexture;
        ndCamera.allowHDR = true;
        ndCamera.allowMSAA = true;
    }

    private void EnsureCanvas(int ndLayer)
    {
        if (ndCanvas == null)
        {
            Transform child = transform.Find("ND_Canvas");
            if (child != null)
            {
                ndCanvas = child.GetComponent<Canvas>();
            }
        }

        if (ndCanvas == null)
        {
            GameObject canvasGo = new GameObject("ND_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            ndCanvas = canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        ndCanvas.gameObject.layer = ndLayer;
        ndCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        ndCanvas.worldCamera = ndCamera;
        ndCanvas.planeDistance = 1f;
        ndCanvas.pixelPerfect = false;

        CanvasScaler scaler = ndCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = ndCanvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(textureSize, textureSize);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureDisplay(int ndLayer)
    {
        if (navigationDisplay == null)
        {
            Transform child = ndCanvas.transform.Find("ND_Display");
            if (child != null)
            {
                navigationDisplay = child.GetComponent<B737NavigationDisplay>();
            }
        }

        if (navigationDisplay == null)
        {
            GameObject displayGo = new GameObject("ND_Display", typeof(RectTransform));
            displayGo.transform.SetParent(ndCanvas.transform, false);
            navigationDisplay = displayGo.AddComponent<B737NavigationDisplay>();
        }

        RectTransform rt = navigationDisplay.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(textureSize, textureSize);
        SetLayerRecursive(navigationDisplay.gameObject, ndLayer);
        if (!Application.isPlaying)
        {
            navigationDisplay.RebuildDisplay();
        }
    }

    private void EnsurePlane()
    {
        bool createdPlane = false;

        if (displayPlaneRenderer == null && createDisplayPlane)
        {
            Transform parent = displayPlaneParent != null ? displayPlaneParent : transform;
            Transform existing = parent.Find("ND_Plane");
            if (existing != null)
            {
                displayPlaneRenderer = existing.GetComponent<MeshRenderer>();
            }

            if (displayPlaneRenderer == null)
            {
                GameObject planeGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
                planeGo.name = "ND_Plane";
                planeGo.transform.SetParent(parent, false);
                displayPlaneRenderer = planeGo.GetComponent<MeshRenderer>();
                createdPlane = true;
            }
        }

        if (displayPlaneRenderer != null)
        {
            if (createdPlane)
            {
                ApplyGeneratedPlaneTransform();
            }
            else
            {
                CaptureGeneratedPlaneTransform();
            }

            displayPlaneRenderer.sharedMaterial = displayMaterial;
        }
    }

    private void ApplyGeneratedPlaneTransform()
    {
        if (!driveGeneratedPlaneTransform || displayPlaneRenderer == null || displayPlaneRenderer.name != "ND_Plane")
        {
            return;
        }

        Transform plane = displayPlaneRenderer.transform;
        plane.localPosition = planeLocalPosition;
        plane.localRotation = Quaternion.Euler(planeLocalEuler);
        plane.localScale = planeLocalScale;
    }

    private void CaptureGeneratedPlaneTransform()
    {
        if (displayPlaneRenderer == null || displayPlaneRenderer.name != "ND_Plane")
        {
            return;
        }

        Transform plane = displayPlaneRenderer.transform;
        planeLocalPosition = plane.localPosition;
        planeLocalEuler = plane.localEulerAngles;
        planeLocalScale = plane.localScale;
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
            displayMaterial.SetColor("_EmissionColor", Color.white * 0.55f);
        }
    }

    private int ResolveLayer()
    {
        int layer = LayerMask.NameToLayer(ndLayerName);
        if (layer < 0)
        {
            layer = Mathf.Clamp(fallbackLayer, 0, 31);
        }

        return layer;
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

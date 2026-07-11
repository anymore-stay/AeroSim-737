using UnityEngine;

[DisallowMultipleComponent]
public class B737FmsClickRaycaster : MonoBehaviour
{
    [SerializeField] private Camera clickCamera;
    [SerializeField] private string buttonLayerName = "FMSButton";
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private bool debugHits = true;

    private int cachedMask;
    private string cachedLayerName;

    public void Configure(Camera cameraOverride, string layerName, float distance)
    {
        clickCamera = cameraOverride;
        buttonLayerName = layerName;
        maxDistance = Mathf.Max(0.1f, distance);
        cachedMask = 0;
        RefreshLayerMask();
    }

    private void Awake()
    {
        RefreshLayerMask();
    }

    private void OnValidate()
    {
        maxDistance = Mathf.Max(0.1f, maxDistance);
        RefreshLayerMask();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Camera activeCamera = ResolveCamera();
        if (activeCamera == null)
        {
            if (debugHits)
            {
                Debug.LogWarning("[B737 FMS] No camera available for FMS button raycast.", this);
            }

            return;
        }

        RefreshLayerMask();
        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, cachedMask, QueryTriggerInteraction.Collide))
        {
            if (debugHits)
            {
                Debug.Log("[B737 FMS] Click missed FMS button hitboxes.", this);
            }

            return;
        }

        B737FmsButton button = hit.collider.GetComponentInParent<B737FmsButton>();
        if (button == null)
        {
            if (debugHits)
            {
                Debug.Log($"[B737 FMS] Raycast hit {hit.collider.name}, but no B737FmsButton was found.", hit.collider);
            }

            return;
        }

        if (debugHits)
        {
            Debug.Log($"[B737 FMS] Clicked {button.name}.", button);
        }

        button.Press();
    }

    private Camera ResolveCamera()
    {
        if (clickCamera != null && clickCamera.isActiveAndEnabled)
        {
            return clickCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            clickCamera = mainCamera;
            return clickCamera;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled && cameras[i].targetTexture == null)
            {
                clickCamera = cameras[i];
                return clickCamera;
            }
        }

        return null;
    }

    private void RefreshLayerMask()
    {
        if (cachedMask != 0 && cachedLayerName == buttonLayerName)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(buttonLayerName);
        cachedLayerName = buttonLayerName;
        cachedMask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;
    }
}

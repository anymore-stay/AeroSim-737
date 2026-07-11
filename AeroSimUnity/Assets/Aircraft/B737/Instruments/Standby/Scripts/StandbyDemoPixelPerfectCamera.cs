using UnityEngine;

/// <summary>
/// 让 Standby_demo 中一个 UI 单位尽量对应一个屏幕像素，避免低分辨率原始图集被过度放大。
/// </summary>
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class StandbyDemoPixelPerfectCamera : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float pixelsPerUnit = 1f;
    [SerializeField, Min(1f)] private float minimumOrthographicSize = 139f;

    private Camera targetCamera;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyPixelPerfectSize();
    }

    private void OnEnable()
    {
        ApplyPixelPerfectSize();
    }

    private void LateUpdate()
    {
        ApplyPixelPerfectSize();
    }

    private void OnPreCull()
    {
        // 编辑状态调整 Game 窗口后，在实际渲染前再次对齐像素比例。
        ApplyPixelPerfectSize();
    }

    public void ApplyPixelPerfectSize()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        targetCamera.orthographic = true;
        float pixelHeight = targetCamera.pixelHeight > 0 ? targetCamera.pixelHeight : Screen.height;
        targetCamera.orthographicSize = Mathf.Max(
            minimumOrthographicSize,
            pixelHeight / (2f * Mathf.Max(0.1f, pixelsPerUnit)));
    }
}

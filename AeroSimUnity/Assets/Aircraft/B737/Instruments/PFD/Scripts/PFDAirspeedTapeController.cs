using UnityEngine;

public class PFDAirspeedTapeController : MonoBehaviour
{
    // speed_tape-1.png 相邻 10 节刻度线中心距离为 30 像素。
    private const float PixelsPerKnot = 3f;

    [Header("空速带内容层")]
    [SerializeField] private RectTransform guideContent;
    [SerializeField] private RectTransform finalContent;

    [Header("空速范围与校准")]
    [SerializeField] private float minimumAirspeedKts = 40f;
    [SerializeField] private float maximumAirspeedKts = 440f;
    [SerializeField] private bool invertDirection = true;

    private RectTransform cachedGuideContent;
    private RectTransform cachedFinalContent;
    private float guideReferenceContentY;
    private float finalReferenceContentY;

    /// <summary>
    /// 设置当前空速，并同步移动预览层与最终层的空速带。
    /// 后续接入真实数据时可直接传入 JsbsimBridge.SpeedKts。
    /// </summary>
    public void SetAirspeed(float airspeedKts)
    {
        EnsureBindings();
        float contentOffsetY = PFDAirspeedTapeMath.CalculateContentOffsetY(
            airspeedKts,
            minimumAirspeedKts,
            maximumAirspeedKts,
            PixelsPerKnot,
            minimumAirspeedKts,
            invertDirection);

        ApplyContentY(guideContent, guideReferenceContentY + contentOffsetY);
        ApplyContentY(finalContent, finalReferenceContentY + contentOffsetY);
    }

    private void EnsureBindings()
    {
        if (guideContent == null || finalContent == null)
        {
            RectTransform[] descendants = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform descendant in descendants)
            {
                if (guideContent == null && descendant.name == "Guide_AirSpeedTapeContent")
                {
                    guideContent = descendant;
                }
                else if (finalContent == null && descendant.name == "Final_AirSpeedTapeContent")
                {
                    finalContent = descendant;
                }
            }
        }

        // 引用变化时读取 Prefab 中人工校准好的位置，避免运行时覆盖布局调整。
        if (guideContent != cachedGuideContent)
        {
            cachedGuideContent = guideContent;
            guideReferenceContentY = guideContent == null ? 0f : guideContent.anchoredPosition.y;
        }

        if (finalContent != cachedFinalContent)
        {
            cachedFinalContent = finalContent;
            finalReferenceContentY = finalContent == null ? 0f : finalContent.anchoredPosition.y;
        }
    }

    private static void ApplyContentY(RectTransform content, float targetY)
    {
        if (content == null)
        {
            return;
        }

        Vector2 position = content.anchoredPosition;
        position.y = targetY;
        content.anchoredPosition = position;
    }
}

using System;
using UnityEngine;

public class PFDAltitudeTapeController : MonoBehaviour
{
    [Serializable]
    private class TapeSegment
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private float yCorrection;

        public RectTransform RectTransform => rectTransform;
        public float YCorrection => yCorrection;
    }

    [Header("高度带内容层")]
    [SerializeField] private RectTransform guideContent;
    [SerializeField] private RectTransform finalContent;

    [Header("高度范围与校准")]
    [SerializeField] private float minimumAltitudeFt = -1000f;
    [SerializeField] private float maximumAltitudeFt = 50000f;
    [SerializeField, Min(0.0001f)] private float pixelsPerFoot = 0.44f;
    [SerializeField] private float referenceAltitudeFt;
    [SerializeField] private bool invertDirection;

    [Header("预览层图片拼接")]
    [SerializeField, Min(0f)] private float segmentOverlap = 24f;
    [SerializeField] private TapeSegment[] guideSegments = Array.Empty<TapeSegment>();

    private bool hasWarnedInvalidPixelsPerFoot;
    private RectTransform cachedGuideContent;
    private RectTransform cachedFinalContent;
    private float guideReferenceContentY;
    private float finalReferenceContentY;

    /// <summary>
    /// 设置当前高度，并同步移动预览层与最终层的高度带。
    /// </summary>
    public void SetAltitude(float altitudeFt)
    {
        EnsureBindings();

        if (pixelsPerFoot <= 0f)
        {
            if (!hasWarnedInvalidPixelsPerFoot)
            {
                Debug.LogWarning("PFD 高度带的每英尺像素数必须大于零。", this);
                hasWarnedInvalidPixelsPerFoot = true;
            }

            return;
        }

        hasWarnedInvalidPixelsPerFoot = false;
        float contentOffsetY = PFDAltitudeTapeMath.CalculateContentY(
            altitudeFt,
            minimumAltitudeFt,
            maximumAltitudeFt,
            pixelsPerFoot,
            referenceAltitudeFt,
            0f,
            invertDirection);

        ApplyContentY(guideContent, guideReferenceContentY + contentOffsetY);
        ApplyContentY(finalContent, finalReferenceContentY + contentOffsetY);
    }

    /// <summary>
    /// 按列表顺序从低到高重新排列预览高度带图片。
    /// </summary>
    [ContextMenu("重新排列预览高度带图片")]
    public void RebuildGuideSegmentLayout()
    {
        if (guideContent == null)
        {
            Debug.LogWarning("尚未绑定 Guide_AltitudeTapeContent，无法排列高度带图片。", this);
            return;
        }

        float cursorY = 0f;
        float contentWidth = 0f;

        foreach (TapeSegment segment in guideSegments)
        {
            if (segment == null || segment.RectTransform == null)
            {
                Debug.LogWarning("高度带图片列表中存在空引用。", this);
                continue;
            }

            RectTransform segmentRect = segment.RectTransform;
            segmentRect.anchorMin = new Vector2(0.5f, 0f);
            segmentRect.anchorMax = new Vector2(0.5f, 0f);
            segmentRect.pivot = new Vector2(0.5f, 0.5f);
            segmentRect.anchoredPosition = new Vector2(
                segmentRect.anchoredPosition.x,
                cursorY + segmentRect.rect.height * 0.5f + segment.YCorrection);

            cursorY += Mathf.Max(0f, segmentRect.rect.height - segmentOverlap);
            contentWidth = Mathf.Max(contentWidth, segmentRect.rect.width);
        }

        Vector2 contentSize = guideContent.sizeDelta;
        contentSize.x = Mathf.Max(contentSize.x, contentWidth);
        contentSize.y = cursorY + segmentOverlap;
        guideContent.sizeDelta = contentSize;
    }

    private void EnsureBindings()
    {
        if (guideContent == null || finalContent == null)
        {
            RectTransform[] descendants = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform descendant in descendants)
            {
                if (guideContent == null && descendant.name == "Guide_AltitudeTapeContent")
                {
                    guideContent = descendant;
                }
                else if (finalContent == null && descendant.name == "Final_AltitudeTapeContent")
                {
                    finalContent = descendant;
                }
            }
        }

        // Content 引用发生变化时，读取 Prefab 中人工校准好的纵向位置作为参考基准。
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

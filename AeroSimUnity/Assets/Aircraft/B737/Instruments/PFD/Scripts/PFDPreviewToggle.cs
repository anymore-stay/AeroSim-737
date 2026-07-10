using UnityEngine;

public class PFDPreviewToggle : MonoBehaviour
{
    [SerializeField] private GameObject previewGroup;
    [SerializeField] private GameObject finalGroup;
    [SerializeField] private bool showPreview;

    public bool ShowPreview => showPreview;

    public void TogglePreview()
    {
        SetPreviewVisible(!showPreview);
    }

    public void SetPreviewVisible(bool visible)
    {
        showPreview = visible;

        if (previewGroup != null)
        {
            previewGroup.SetActive(showPreview);
        }

        if (finalGroup != null)
        {
            finalGroup.SetActive(!showPreview);
        }
    }

    private void OnValidate()
    {
        SetPreviewVisible(showPreview);
    }
}

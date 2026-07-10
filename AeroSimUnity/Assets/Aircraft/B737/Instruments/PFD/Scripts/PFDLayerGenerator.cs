using UnityEngine;

public class PFDLayerGenerator : MonoBehaviour
{
    [SerializeField] private GameObject previewGuide;
    [SerializeField] private string finalLayerName = "PFD_Final";
    [SerializeField] private bool overwriteExisting = true;
    [SerializeField] private bool hidePreviewAfterGenerate = true;
    [SerializeField] private bool showFinalAfterGenerate = true;

    public GameObject PreviewGuide => previewGuide;
    public string FinalLayerName => finalLayerName;
    public bool OverwriteExisting => overwriteExisting;
    public bool HidePreviewAfterGenerate => hidePreviewAfterGenerate;
    public bool ShowFinalAfterGenerate => showFinalAfterGenerate;
}

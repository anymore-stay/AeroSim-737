using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]
public sealed class AircraftViewCullingOptimizer : MonoBehaviour
{
    private const string RuntimeObjectName = "[AircraftViewCullingOptimizer]";
    private const string CabinGroupName = "\u5ba2\u8231";
    private const string CockpitGroupName = "\u9a7e\u9a76\u8231";
    private const float RefreshInterval = 2f;
    private const long SmallShadowCasterTriangleLimit = 500;
    private const float SmallShadowCasterSizeLimit = 1f;

    private enum ViewMode
    {
        Unknown,
        ThirdPerson,
        Cockpit,
        Cabin
    }

    private enum InteriorSection
    {
        Cockpit,
        Cabin
    }

    private sealed class ManagedRenderer
    {
        public Renderer Renderer;
        public InteriorSection Section;
        public bool OriginalEnabled;
        public bool VisibleFromExterior;
        public ShadowCastingMode OriginalShadowCastingMode;
    }

    private readonly List<ManagedRenderer> managedRenderers = new List<ManagedRenderer>();
    private readonly HashSet<Renderer> knownRenderers = new HashSet<Renderer>();
    private readonly List<Material> sharedMaterials = new List<Material>(8);

    private Transform aircraftRoot;
    private ViewMode currentView = ViewMode.Unknown;
    private float nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeOptimizer()
    {
        if (FindObjectOfType<AircraftViewCullingOptimizer>() != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<AircraftViewCullingOptimizer>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RefreshAircraft();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            RefreshAircraft();
        }

        ViewMode nextView = DetectViewMode();
        if (nextView != currentView)
        {
            currentView = nextView;
            ApplyViewVisibility();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        RestoreManagedRenderers();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RestoreManagedRenderers();
        managedRenderers.Clear();
        knownRenderers.Clear();
        aircraftRoot = null;
        currentView = ViewMode.Unknown;
        RefreshAircraft();
    }

    private void RefreshAircraft()
    {
        nextRefreshTime = Time.unscaledTime + RefreshInterval;

        if (aircraftRoot == null)
        {
            aircraftRoot = FindAircraftRoot();
        }

        if (aircraftRoot == null)
        {
            return;
        }

        Transform cockpit = aircraftRoot.Find(CockpitGroupName);
        Transform cabin = aircraftRoot.Find(CabinGroupName);
        AddSectionRenderers(cockpit, InteriorSection.Cockpit);
        AddSectionRenderers(cabin, InteriorSection.Cabin);

        currentView = DetectViewMode();
        ApplyViewVisibility();
    }

    private static Transform FindAircraftRoot()
    {
        if (CameraManager.Instance != null)
        {
            return CameraManager.Instance.transform;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (!NameContains(cameras[i].name, "Cockpit"))
            {
                continue;
            }

            Transform current = cameras[i].transform;
            while (current.parent != null)
            {
                current = current.parent;
            }
            return current;
        }

        return null;
    }

    private void AddSectionRenderers(Transform sectionRoot, InteriorSection section)
    {
        if (sectionRoot == null)
        {
            return;
        }

        Renderer[] renderers = sectionRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !knownRenderers.Add(renderer))
            {
                continue;
            }

            ManagedRenderer managed = new ManagedRenderer
            {
                Renderer = renderer,
                Section = section,
                OriginalEnabled = renderer.enabled,
                VisibleFromExterior = ShouldShowFromExterior(sectionRoot, renderer.transform, section),
                OriginalShadowCastingMode = renderer.shadowCastingMode
            };
            managedRenderers.Add(managed);

            if (ShouldDisableShadowCasting(renderer))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    private bool ShouldDisableShadowCasting(Renderer renderer)
    {
        if (renderer.shadowCastingMode == ShadowCastingMode.Off)
        {
            return false;
        }

        string rendererName = renderer.name;
        if (NameContains(rendererName, "screen")
            || NameContains(rendererName, "display")
            || NameContains(rendererName, "text")
            || NameContains(rendererName, "label")
            || NameContains(rendererName, "glass")
            || NameContains(rendererName, "window")
            || NameContains(rendererName, "lamp")
            || NameContains(rendererName, "led")
            || NameContains(rendererName, "pfd")
            || NameContains(rendererName, "eicas")
            || NameContains(rendererName, "fms")
            || NameContains(rendererName, "clock")
            || rendererName.IndexOf("\u4eea\u8868", StringComparison.OrdinalIgnoreCase) >= 0
            || rendererName.IndexOf("\u6587\u5b57", StringComparison.OrdinalIgnoreCase) >= 0
            || rendererName.IndexOf("\u73bb\u7483", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        sharedMaterials.Clear();
        renderer.GetSharedMaterials(sharedMaterials);
        for (int i = 0; i < sharedMaterials.Count; i++)
        {
            Material material = sharedMaterials[i];
            if (material != null && material.renderQueue >= (int)RenderQueue.Transparent)
            {
                return true;
            }
        }

        Mesh mesh = GetRendererMesh(renderer);
        if (mesh == null || renderer.bounds.size.sqrMagnitude > SmallShadowCasterSizeLimit * SmallShadowCasterSizeLimit)
        {
            return false;
        }

        long triangles = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            triangles += (long)mesh.GetIndexCount(i) / 3;
            if (triangles > SmallShadowCasterTriangleLimit)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldShowFromExterior(
        Transform sectionRoot,
        Transform rendererTransform,
        InteriorSection section)
    {
        string groupName = GetDirectChildName(sectionRoot, rendererTransform);
        if (section == InteriorSection.Cabin)
        {
            return groupName != "\u8d27\u8231";
        }

        return groupName != "\u9876\u677f1"
               && groupName != "\u9876\u677f2"
               && groupName != "\u4eea\u8868"
               && groupName != "\u5f00\u5173"
               && groupName != "\u811a\u8235"
               && groupName != "\u64cd\u7eb5\u6746"
               && groupName != "FMS\u5c4f\u5e55"
               && groupName != "\u5c4f\u5e55\u6c61\u6e0d";
    }

    private static string GetDirectChildName(Transform sectionRoot, Transform descendant)
    {
        Transform current = descendant;
        while (current != null && current.parent != sectionRoot)
        {
            current = current.parent;
        }

        return current != null ? current.name : string.Empty;
    }

    private static Mesh GetRendererMesh(Renderer renderer)
    {
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null)
        {
            return skinned.sharedMesh;
        }

        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private void ApplyViewVisibility()
    {
        for (int i = 0; i < managedRenderers.Count; i++)
        {
            ManagedRenderer managed = managedRenderers[i];
            if (managed.Renderer == null)
            {
                continue;
            }

            bool sectionVisible = currentView == ViewMode.Unknown
                                  || (currentView == ViewMode.ThirdPerson
                                      && managed.VisibleFromExterior)
                                  || (currentView == ViewMode.Cockpit
                                      && managed.Section == InteriorSection.Cockpit)
                                  || (currentView == ViewMode.Cabin
                                      && managed.Section == InteriorSection.Cabin);
            managed.Renderer.enabled = managed.OriginalEnabled && sectionVisible;
        }
    }

    private static ViewMode DetectViewMode()
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.targetTexture != null || !camera.enabled)
            {
                continue;
            }

            if (NameContains(camera.name, "Cockpit"))
            {
                return ViewMode.Cockpit;
            }
            if (NameContains(camera.name, "Cabin"))
            {
                return ViewMode.Cabin;
            }
            if (NameContains(camera.name, "Third"))
            {
                return ViewMode.ThirdPerson;
            }
        }

        return ViewMode.Unknown;
    }

    private void RestoreManagedRenderers()
    {
        for (int i = 0; i < managedRenderers.Count; i++)
        {
            ManagedRenderer managed = managedRenderers[i];
            if (managed.Renderer == null)
            {
                continue;
            }

            managed.Renderer.enabled = managed.OriginalEnabled;
            managed.Renderer.shadowCastingMode = managed.OriginalShadowCastingMode;
        }
    }

    private static bool NameContains(string value, string search)
    {
        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

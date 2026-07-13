using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class InstrumentCameraOptimizer : MonoBehaviour
{
    private const string RuntimeObjectName = "[InstrumentCameraOptimizer]";
    private const int InstrumentRendererIndex = 1;
    private const float BackgroundRefreshRate = 2f;
    private const float CameraRefreshInterval = 2f;

    private sealed class InstrumentCamera
    {
        public Camera Camera;
        public float ForegroundInterval;
        public float NextRenderTime;
        public bool RenderRequested;
    }

    private readonly List<InstrumentCamera> instrumentCameras = new List<InstrumentCamera>();
    private float nextCameraRefreshTime;
    private bool wasCockpitViewActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeOptimizer()
    {
        if (FindObjectOfType<InstrumentCameraOptimizer>() != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<InstrumentCameraOptimizer>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RefreshInstrumentCameras();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextCameraRefreshTime)
        {
            RefreshInstrumentCameras();
        }

        bool cockpitViewActive = IsCockpitViewActive();
        if (cockpitViewActive != wasCockpitViewActive)
        {
            RequestImmediateRefresh();
            wasCockpitViewActive = cockpitViewActive;
        }

        float backgroundInterval = 1f / BackgroundRefreshRate;
        for (int i = 0; i < instrumentCameras.Count; i++)
        {
            InstrumentCamera instrument = instrumentCameras[i];
            Camera camera = instrument.Camera;
            if (camera == null || !camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (Time.unscaledTime < instrument.NextRenderTime)
            {
                if (!instrument.RenderRequested)
                {
                    camera.enabled = false;
                }
                continue;
            }

            instrument.NextRenderTime = Time.unscaledTime
                                        + (cockpitViewActive
                                            ? instrument.ForegroundInterval
                                            : backgroundInterval);
            instrument.RenderRequested = true;
            camera.enabled = true;
        }
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        for (int i = 0; i < instrumentCameras.Count; i++)
        {
            if (instrumentCameras[i].Camera != null)
            {
                instrumentCameras[i].Camera.enabled = true;
            }
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshInstrumentCameras();
    }

    private void HandleEndCameraRendering(ScriptableRenderContext context, Camera renderedCamera)
    {
        for (int i = 0; i < instrumentCameras.Count; i++)
        {
            InstrumentCamera instrument = instrumentCameras[i];
            if (instrument.Camera != renderedCamera)
            {
                continue;
            }

            renderedCamera.enabled = false;
            instrument.RenderRequested = false;
            return;
        }
    }

    private void RefreshInstrumentCameras()
    {
        nextCameraRefreshTime = Time.unscaledTime + CameraRefreshInterval;

        for (int i = instrumentCameras.Count - 1; i >= 0; i--)
        {
            if (instrumentCameras[i].Camera == null)
            {
                instrumentCameras.RemoveAt(i);
            }
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera.targetTexture == null || ContainsCamera(camera))
            {
                continue;
            }

            ConfigureLightweightRendering(camera);
            camera.enabled = false;

            instrumentCameras.Add(new InstrumentCamera
            {
                Camera = camera,
                ForegroundInterval = 1f / GetForegroundRefreshRate(camera.name),
                NextRenderTime = 0f
            });
        }
    }

    private bool ContainsCamera(Camera camera)
    {
        for (int i = 0; i < instrumentCameras.Count; i++)
        {
            if (instrumentCameras[i].Camera == camera)
            {
                return true;
            }
        }

        return false;
    }

    private static void ConfigureLightweightRendering(Camera camera)
    {
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.depthTextureMode = DepthTextureMode.None;

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.stopNaN = false;
        cameraData.dithering = false;
        cameraData.antialiasing = AntialiasingMode.None;
        cameraData.SetRenderer(InstrumentRendererIndex);
    }

    private void RequestImmediateRefresh()
    {
        for (int i = 0; i < instrumentCameras.Count; i++)
        {
            instrumentCameras[i].NextRenderTime = 0f;
        }
    }

    private static bool IsCockpitViewActive()
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.targetTexture != null || !camera.enabled)
            {
                continue;
            }

            if (camera.name.IndexOf("Cockpit", StringComparison.OrdinalIgnoreCase) >= 0
                || camera.name.IndexOf("Cabin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static float GetForegroundRefreshRate(string cameraName)
    {
        if (Contains(cameraName, "PFD")
            || Contains(cameraName, "ND")
            || Contains(cameraName, "Standby"))
        {
            return 30f;
        }

        if (Contains(cameraName, "EICAS"))
        {
            return 20f;
        }

        if (Contains(cameraName, "FMS") || Contains(cameraName, "Clock"))
        {
            return 15f;
        }

        return 20f;
    }

    private static bool Contains(string value, string search)
    {
        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

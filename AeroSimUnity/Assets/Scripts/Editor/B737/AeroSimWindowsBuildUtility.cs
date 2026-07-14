using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class AeroSimWindowsBuildUtility
{
    private const string StartScenePath = "Assets/Scenes/StartMenu.unity";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string BuildRelativePath = "Builds/Windows/AeroSim-737.exe";

    [MenuItem("AeroSim/Build/生成开始界面场景")]
    public static void GenerateStartMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "StartMenu";

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.043f, 0.055f, 1f);
        cameraObject.tag = "MainCamera";

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        new GameObject("StartMenuController", typeof(AeroSimStartMenuController));

        Directory.CreateDirectory(Path.GetDirectoryName(ToAbsoluteProjectPath(StartScenePath)));
        EditorSceneManager.SaveScene(scene, StartScenePath);
        ConfigureBuildScenes();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("AeroSim/Build/配置 Windows 构建场景")]
    public static void ConfigureBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(StartScenePath, true),
            new EditorBuildSettingsScene(MainScenePath, true)
        };
    }

    [MenuItem("AeroSim/Build/构建 Windows x64")]
    public static void BuildWindowsPlayer()
    {
        GenerateStartMenuScene();
        ValidateStartMenuConfiguration();

        string outputPath = GetBuildOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { StartScenePath, MainScenePath },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Windows 构建失败：{summary.result}");
        }

        Debug.Log($"Windows 构建完成：{outputPath}");
    }

    [MenuItem("AeroSim/Build/验证开始界面配置")]
    public static void ValidateStartMenuConfiguration()
    {
        AeroSimStartMenuController.GraphicsPreset[] presets = AeroSimStartMenuController.CreateDefaultPresets();
        if (presets.Length != 3)
        {
            throw new InvalidOperationException($"开始界面画质档位数量错误：{presets.Length}");
        }

        AeroSimStartMenuController.GraphicsPreset highestPreset = presets[presets.Length - 1];
        if (highestPreset.Width != 3840 || highestPreset.Height != 2160 || highestPreset.QualityName != "High Fidelity")
        {
            throw new InvalidOperationException("最高画质档位必须是 3840x2160 且使用 High Fidelity。");
        }

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Length < 2 || scenes[0].path != StartScenePath || scenes[1].path != MainScenePath)
        {
            throw new InvalidOperationException("构建场景顺序必须是 StartMenu -> MainScene。");
        }

        Debug.Log("开始界面配置验证通过。");
    }

    public static string GetBuildOutputPath()
    {
        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        string repositoryDirectory = Directory.GetParent(projectDirectory).FullName;
        return Path.Combine(repositoryDirectory, BuildRelativePath);
    }

    private static string ToAbsoluteProjectPath(string assetPath)
    {
        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectDirectory, assetPath);
    }
}

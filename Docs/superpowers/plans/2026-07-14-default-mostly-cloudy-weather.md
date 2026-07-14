# 默认大部多云天气 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将主场景默认天气改为“大部多云”，并让天气菜单默认显示同一状态。

**Architecture:** 通过主场景中 UniStorm 系统的 `CurrentWeatherType` 引用决定启动天气。天气菜单已经根据 `CurrentWeatherType` 自动选中当前天气，因此只需要锁定场景引用，并用编辑器测试防止回退。

**Tech Stack:** Unity、NUnit 编辑器测试、UniStorm Weather System、YAML 场景序列化。

---

### Task 1: 添加默认天气测试

**Files:**
- Modify: `AeroSimUnity/Assets/Scripts/Editor/B737/B737UniStormWeatherMenuControllerTests.cs`
- Read: `AeroSimUnity/Assets/Scenes/MainScene.unity`
- Read: `AeroSimUnity/Assets/UniStorm Weather System/Weather Types/Non-Precipitation/Mostly Cloudy.asset.meta`

- [ ] **Step 1: Write the failing test**

在 `B737UniStormWeatherMenuControllerTests` 类内追加：

```csharp
[Test]
public void 主场景默认天气为大部多云()
{
    const string mostlyCloudyGuid = "b1b04f0270cfa784588fdc5818097ad3";
    string sceneText = File.ReadAllText("Assets/Scenes/MainScene.unity");
    string mostlyCloudyMetaText = File.ReadAllText("Assets/UniStorm Weather System/Weather Types/Non-Precipitation/Mostly Cloudy.asset.meta");

    Assert.That(mostlyCloudyMetaText, Does.Contain($"guid: {mostlyCloudyGuid}"));
    Assert.That(sceneText, Does.Contain($"CurrentWeatherType: {{fileID: 11400000, guid: {mostlyCloudyGuid}, type: 2}}"));
}
```

并补充 `using System.IO;`。

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
& "D:/Software/UnityEditor/app/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -projectPath "D:/Desktop/AeroSim-737/AeroSimUnity" -runTests -testPlatform EditMode -testFilter B737UniStormWeatherMenuControllerTests.主场景默认天气为大部多云 -testResults "D:/Desktop/AeroSim-737/outputs/test-results/default-mostly-cloudy-red.xml" -quit
```

Expected: FAIL，因为 `CurrentWeatherType` 仍然包含 `865815ce607d94c4cb41162c44555e7c`。

### Task 2: 替换场景默认天气引用

**Files:**
- Modify: `AeroSimUnity/Assets/Scenes/MainScene.unity`

- [ ] **Step 1: Write minimal implementation**

只替换这一行：

```yaml
CurrentWeatherType: {fileID: 11400000, guid: b1b04f0270cfa784588fdc5818097ad3, type: 2}
```

- [ ] **Step 2: Run test to verify it passes**

Run:

```powershell
& "D:/Software/UnityEditor/app/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -projectPath "D:/Desktop/AeroSim-737/AeroSimUnity" -runTests -testPlatform EditMode -testFilter B737UniStormWeatherMenuControllerTests.主场景默认天气为大部多云 -testResults "D:/Desktop/AeroSim-737/outputs/test-results/default-mostly-cloudy-green.xml" -quit
```

Expected: PASS。

- [ ] **Step 3: Check changed files**

Run:

```powershell
git diff -- AeroSimUnity/Assets/Scripts/Editor/B737/B737UniStormWeatherMenuControllerTests.cs AeroSimUnity/Assets/Scenes/MainScene.unity
```

Expected: 测试文件只新增默认天气断言；场景文件只替换 `CurrentWeatherType` 的 GUID。

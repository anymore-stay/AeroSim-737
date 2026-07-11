# PFD 高度刻度带实施计划

> **供智能执行者使用：** 必须使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans`，逐项执行本计划。所有步骤使用复选框跟踪。

**目标：** 在现有 PFD Prefab 中加入可人工精调遮罩、可拼接 12 张贴图、可由模拟高度连续驱动的高度刻度带。

**架构：** 使用 `RectMask2D` 限制可见区域，12 个 `Image` 子对象纵向排列在 Content 下，通过改变 Guide 与 Final 两个 Content 的 `anchoredPosition.y` 同步滚动。纯计算放在独立数学类中，模拟数据由独立模拟器提供，之后真实数据继续调用同一个 `SetAltitude` 接口。

**技术栈：** Unity 2022.3、C#、uGUI、RectMask2D、Unity Test Framework、NUnit、Unity MCP。

---

## 文件结构

- 新建 `AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeMath.cs`：只负责高度限制、滚动坐标和自动模拟高度计算。
- 新建 `AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeController.cs`：保存 UI 引用、排列图片、同步移动 Guide 与 Final Content。
- 新建 `AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeSimulator.cs`：提供手动和自动模拟高度。
- 新建 `AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs`：覆盖数学计算、控制器双层同步和模拟器边界。
- 修改 `AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab`：加入高度带层级、遮罩、图片和组件绑定。
- 修改 `AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scene/PFD_demo.unity`：同步场景中的 Prefab 实例并验证画面。

### 任务 1：高度滚动纯计算

**文件：**

- 新建：`AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs`
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeMath.cs`

- [ ] **步骤 1：编写失败测试**

先创建测试文件，内容如下：

```csharp
using System;
using System.Reflection;
using NUnit.Framework;

public class PFDAltitudeTapeTests
{
    [Test]
    public void 高度会被限制在贴图范围内()
    {
        Type mathType = 获取运行时类型("PFDAltitudeTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "ClampAltitude");

        Assert.That((float)method.Invoke(null, new object[] { -2000f, -1000f, 50000f }), Is.EqualTo(-1000f));
        Assert.That((float)method.Invoke(null, new object[] { 51000f, -1000f, 50000f }), Is.EqualTo(50000f));
        Assert.That((float)method.Invoke(null, new object[] { 3600f, -1000f, 50000f }), Is.EqualTo(3600f));
    }

    [Test]
    public void 高度差会换算为内容层纵向位置()
    {
        Type mathType = 获取运行时类型("PFDAltitudeTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateContentY");

        float result = (float)method.Invoke(
            null,
            new object[] { 1000f, -1000f, 50000f, 0.44f, 0f, 12f, false });

        Assert.That(result, Is.EqualTo(452f).Within(0.001f));
    }

    [Test]
    public void 可以反转高度带滚动方向()
    {
        Type mathType = 获取运行时类型("PFDAltitudeTapeMath");
        MethodInfo method = 获取公开静态方法(mathType, "CalculateContentY");

        float result = (float)method.Invoke(
            null,
            new object[] { 1000f, -1000f, 50000f, 0.44f, 0f, 12f, true });

        Assert.That(result, Is.EqualTo(-428f).Within(0.001f));
    }

    private static Type 获取运行时类型(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName + " 尚未实现或尚未编译。");
        return type;
    }

    private static MethodInfo 获取公开静态方法(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, methodName + " 公开静态方法不存在。");
        return method;
    }
}
```

- [ ] **步骤 2：运行测试并确认失败**

通过 Unity MCP 运行 EditMode 测试：

```text
run_tests(mode="EditMode", test_names=[
  "PFDAltitudeTapeTests.高度会被限制在贴图范围内",
  "PFDAltitudeTapeTests.高度差会换算为内容层纵向位置",
  "PFDAltitudeTapeTests.可以反转高度带滚动方向"
])
```

预期：测试失败，提示 `PFDAltitudeTapeMath 尚未实现或尚未编译。`

- [ ] **步骤 3：实现最小数学类**

创建 `PFDAltitudeTapeMath.cs`：

```csharp
using UnityEngine;

public static class PFDAltitudeTapeMath
{
    public static float ClampAltitude(float altitudeFt, float minimumAltitudeFt, float maximumAltitudeFt)
    {
        float minimum = Mathf.Min(minimumAltitudeFt, maximumAltitudeFt);
        float maximum = Mathf.Max(minimumAltitudeFt, maximumAltitudeFt);
        return Mathf.Clamp(altitudeFt, minimum, maximum);
    }

    public static float CalculateContentY(
        float altitudeFt,
        float minimumAltitudeFt,
        float maximumAltitudeFt,
        float pixelsPerFoot,
        float referenceAltitudeFt,
        float referenceContentY,
        bool invertDirection)
    {
        float clampedAltitude = ClampAltitude(altitudeFt, minimumAltitudeFt, maximumAltitudeFt);
        float direction = invertDirection ? -1f : 1f;
        return referenceContentY
            + (clampedAltitude - referenceAltitudeFt) * pixelsPerFoot * direction;
    }
}
```

- [ ] **步骤 4：等待 Unity 编译并重新运行测试**

读取 `mcpforunity://editor/state`，等待 `is_compiling == false`，然后重新运行任务 1 的三个测试。

预期：3 个测试全部通过，Console 没有编译错误。

- [ ] **步骤 5：提交纯计算与测试**

```powershell
git add -- 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeMath.cs' 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeMath.cs.meta' 'AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs' 'AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs.meta'
git commit -m '功能：增加PFD高度带滚动计算'
```

### 任务 2：高度带控制器与双层同步

**文件：**

- 修改：`AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs`
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeController.cs`

- [ ] **步骤 1：在测试类中加入控制器失败测试**

在 `PFDAltitudeTapeTests` 类结束前加入：

```csharp
    [Test]
    public void 控制器会同步移动预览层和最终层()
    {
        UnityEngine.GameObject root = new UnityEngine.GameObject("PFD_Root");
        UnityEngine.GameObject guideObject = new UnityEngine.GameObject("Guide_AltitudeTapeContent", typeof(UnityEngine.RectTransform));
        UnityEngine.GameObject finalObject = new UnityEngine.GameObject("Final_AltitudeTapeContent", typeof(UnityEngine.RectTransform));

        try
        {
            guideObject.transform.SetParent(root.transform, false);
            finalObject.transform.SetParent(root.transform, false);

            Type controllerType = 获取运行时类型("PFDAltitudeTapeController");
            UnityEngine.Component controller = root.AddComponent(controllerType);
            MethodInfo setAltitude = controllerType.GetMethod("SetAltitude", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(setAltitude, Is.Not.Null, "SetAltitude 公开实例方法不存在。");

            setAltitude.Invoke(controller, new object[] { 1000f });

            UnityEngine.RectTransform guide = guideObject.GetComponent<UnityEngine.RectTransform>();
            UnityEngine.RectTransform final = finalObject.GetComponent<UnityEngine.RectTransform>();
            Assert.That(guide.anchoredPosition.y, Is.EqualTo(final.anchoredPosition.y).Within(0.001f));
            Assert.That(guide.anchoredPosition.y, Is.Not.EqualTo(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
```

- [ ] **步骤 2：运行新测试并确认失败**

```text
run_tests(mode="EditMode", test_names=["PFDAltitudeTapeTests.控制器会同步移动预览层和最终层"])
```

预期：失败，提示 `PFDAltitudeTapeController 尚未实现或尚未编译。`

- [ ] **步骤 3：实现控制器**

创建 `PFDAltitudeTapeController.cs`：

```csharp
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
    [SerializeField] private float referenceContentY;
    [SerializeField] private bool invertDirection;

    [Header("预览层图片拼接")]
    [SerializeField, Min(0f)] private float segmentOverlap = 24f;
    [SerializeField] private TapeSegment[] guideSegments = Array.Empty<TapeSegment>();

    private bool hasWarnedInvalidPixelsPerFoot;

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
        float targetY = PFDAltitudeTapeMath.CalculateContentY(
            altitudeFt,
            minimumAltitudeFt,
            maximumAltitudeFt,
            pixelsPerFoot,
            referenceAltitudeFt,
            referenceContentY,
            invertDirection);

        ApplyContentY(guideContent, targetY);
        ApplyContentY(finalContent, targetY);
    }

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
        if (guideContent != null && finalContent != null)
        {
            return;
        }

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
```

- [ ] **步骤 4：等待编译并运行全部高度带测试**

预期：任务 1 的三个测试和双层同步测试全部通过；Console 无错误。

- [ ] **步骤 5：提交控制器**

```powershell
git add -- 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeController.cs' 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeController.cs.meta' 'AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs'
git commit -m '功能：增加PFD高度刻度带控制器'
```

### 任务 3：手动与自动模拟高度

**文件：**

- 修改：`AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs`
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeSimulator.cs`

- [ ] **步骤 1：加入自动模拟失败测试**

在测试类结束前加入：

```csharp
    [Test]
    public void 自动模拟会在一个周期内往返最低和最高高度()
    {
        Type simulatorType = 获取运行时类型("PFDAltitudeTapeSimulator");
        MethodInfo method = 获取公开静态方法(simulatorType, "EvaluateAutomaticAltitude");

        float start = (float)method.Invoke(null, new object[] { 0f, 0f, 10000f, 8f });
        float peak = (float)method.Invoke(null, new object[] { 4f, 0f, 10000f, 8f });
        float end = (float)method.Invoke(null, new object[] { 8f, 0f, 10000f, 8f });

        Assert.That(start, Is.EqualTo(0f).Within(0.001f));
        Assert.That(peak, Is.EqualTo(10000f).Within(0.001f));
        Assert.That(end, Is.EqualTo(0f).Within(0.001f));
    }
```

- [ ] **步骤 2：运行新测试并确认失败**

预期：失败，提示 `PFDAltitudeTapeSimulator 尚未实现或尚未编译。`

- [ ] **步骤 3：实现模拟器**

创建 `PFDAltitudeTapeSimulator.cs`：

```csharp
using UnityEngine;

public class PFDAltitudeTapeSimulator : MonoBehaviour
{
    public enum SimulationMode
    {
        Manual,
        Automatic
    }

    [SerializeField] private PFDAltitudeTapeController controller;
    [SerializeField] private SimulationMode mode = SimulationMode.Manual;
    [SerializeField, Range(-1000f, 50000f)] private float simulatedAltitudeFt;
    [SerializeField] private float automaticMinimumAltitudeFt;
    [SerializeField] private float automaticMaximumAltitudeFt = 10000f;
    [SerializeField, Min(0.1f)] private float automaticRoundTripSeconds = 12f;

    public static float EvaluateAutomaticAltitude(
        float time,
        float minimumAltitudeFt,
        float maximumAltitudeFt,
        float roundTripSeconds)
    {
        float minimum = Mathf.Min(minimumAltitudeFt, maximumAltitudeFt);
        float maximum = Mathf.Max(minimumAltitudeFt, maximumAltitudeFt);
        float range = maximum - minimum;

        if (range <= 0f || roundTripSeconds <= 0f)
        {
            return minimum;
        }

        float distance = time * range * 2f / roundTripSeconds;
        return minimum + Mathf.PingPong(distance, range);
    }

    private void Update()
    {
        if (controller == null)
        {
            controller = GetComponent<PFDAltitudeTapeController>();
        }

        if (controller == null)
        {
            return;
        }

        float altitudeFt = mode == SimulationMode.Manual
            ? simulatedAltitudeFt
            : EvaluateAutomaticAltitude(
                Time.time,
                automaticMinimumAltitudeFt,
                automaticMaximumAltitudeFt,
                automaticRoundTripSeconds);

        controller.SetAltitude(altitudeFt);
    }
}
```

- [ ] **步骤 4：等待编译并运行全部高度带测试**

预期：5 个高度带测试全部通过；Console 无错误。

- [ ] **步骤 5：提交模拟器**

```powershell
git add -- 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeSimulator.cs' 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAltitudeTapeSimulator.cs.meta' 'AeroSimUnity/Assets/Scripts/Editor/B737/PFDAltitudeTapeTests.cs'
git commit -m '功能：增加PFD高度模拟器'
```

### 任务 4：搭建预览高度带 UI

**文件：**

- 修改：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab`

- [ ] **步骤 1：确认 Unity 状态和用户现有改动**

读取 `mcpforunity://editor/state`，确认当前工程是 `AeroSimUnity`、没有编译和资源刷新阻塞。记录当前 Prefab 与场景差异，不覆盖与高度带无关的 AOA 或其他 PFD 改动。

- [ ] **步骤 2：在预览层创建可调遮罩结构**

通过 Unity MCP 打开 `PFD_Display.prefab`，在 `PFD_PreviewGuide` 下创建：

```text
Guide_AltitudeTapeViewport
└── Guide_AltitudeTapeContent
```

初始 RectTransform 建议值：

```text
Guide_AltitudeTapeViewport
  anchorMin/anchorMax = (0.5, 0.5)
  pivot = (0.5, 0.5)
  anchoredPosition = (196, 38)
  sizeDelta = (70, 420)
  component = RectMask2D

Guide_AltitudeTapeContent
  anchorMin/anchorMax = (0.5, 0.5)
  pivot = (0.5, 0)
  anchoredPosition = (0, 0)
  sizeDelta = (70, 1)
```

这些值只作为可见的初始位置，最终由用户使用 Rect Tool 精调。

- [ ] **步骤 3：创建并绑定 12 个 Image**

按以下顺序在 Content 下创建，Sprite 使用 `PreviewRGB/ALT_Tapes` 对应图片：

```text
Guide_ALT_-10_036  -> -10_036-1.png
Guide_ALT_036_082  -> 036_082-1.png
Guide_ALT_082_128  -> 082_128-1.png
Guide_ALT_128_174  -> 128_174-1.png
Guide_ALT_174_220  -> 174_220-1.png
Guide_ALT_220_266  -> 220_266-1.png
Guide_ALT_266_312  -> 266_312-1.png
Guide_ALT_312_358  -> 312_358-1.png
Guide_ALT_358_404  -> 358_404-1.png
Guide_ALT_404_450  -> 404_450-1.png
Guide_ALT_450_496  -> 450_496-1.png
Guide_ALT_496_500  -> 496_500-1.png
```

每个 Image：

```text
anchorMin/anchorMax = (0.5, 0)
pivot = (0.5, 0.5)
宽高 = Sprite 原始尺寸
preserveAspect = false
raycastTarget = false
```

- [ ] **步骤 4：把组件挂到 PFD_Display 根节点**

加入 `PFDAltitudeTapeController` 和 `PFDAltitudeTapeSimulator`。控制器先绑定 Guide Content 与 12 个 Guide 图片，Final Content 暂时为空；默认参数：

```text
minimumAltitudeFt = -1000
maximumAltitudeFt = 50000
pixelsPerFoot = 0.44
referenceAltitudeFt = 0
referenceContentY = 0
invertDirection = false
segmentOverlap = 24
```

模拟器默认参数：

```text
mode = Manual
simulatedAltitudeFt = 0
automaticMinimumAltitudeFt = 0
automaticMaximumAltitudeFt = 10000
automaticRoundTripSeconds = 12
```

- [ ] **步骤 5：执行图片自动排列并保存 Prefab**

调用控制器的 `RebuildGuideSegmentLayout`。确认图片从低到高向上排列，并且 Content 高度覆盖全部图片。保存 Prefab 后检查 Console。

- [ ] **步骤 6：提交预览层 UI**

```powershell
git add -- 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab'
git commit -m '界面：搭建PFD高度刻度带预览层'
```

### 任务 5：生成最终层并绑定双层滚动

**文件：**

- 修改：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab`
- 修改：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scene/PFD_demo.unity`

- [ ] **步骤 1：先保存和检查当前 PFD 层级**

确认 `PFD_PreviewGuide` 中包含当前所有已完成元素。现有生成器会删除并重建整个 `PFD_Final`，因此只有在 Preview 已包含需要保留的全部内容时才执行生成；否则只复制 `Guide_AltitudeTapeViewport` 子树到现有 Final，避免覆盖用户正在进行的其他 PFD 修改。

- [ ] **步骤 2：生成或补充 Final 高度带**

首选使用现有 `PFDLayerGeneratorEditor` 生成 Final。生成后应得到：

```text
PFD_Final
└── Final_AltitudeTapeViewport
    └── Final_AltitudeTapeContent
        ├── Final_ALT_-10_036
        ├── ...
        └── Final_ALT_496_500
```

确认 Final 图片 Sprite 全部来自 `Textures/Used/ALT_Tapes`。

- [ ] **步骤 3：绑定 Final Content**

将 `Final_AltitudeTapeContent` 绑定到根节点控制器的 `finalContent`。手动调用 `SetAltitude(3600)`，确认 Guide 和 Final Content 的 Y 坐标一致。

- [ ] **步骤 4：保存 Prefab 与场景实例**

保存 `PFD_Display.prefab`，回到 `PFD_demo.unity`，应用或刷新 Prefab 实例。不要改变与本功能无关的相机、光照和其他仪表对象。

- [ ] **步骤 5：提交最终层与场景同步**

```powershell
git add -- 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab' 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scene/PFD_demo.unity'
git commit -m '界面：完成PFD高度刻度带双层同步'
```

### 任务 6：画面校准与最终验证

**文件：**

- 可能修改：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab`
- 可能修改：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scene/PFD_demo.unity`

- [ ] **步骤 1：手动校准遮罩区域**

在 Scene 视图中选择 `Guide_AltitudeTapeViewport`，使用 Rect Tool 调整到右侧灰色矩形。只改 Viewport 的位置与尺寸，不改滚动公式。

- [ ] **步骤 2：校准拼接接缝**

先调统一 `segmentOverlap`，再使用每张图片的 `yCorrection` 修复个别接缝。重点检查：

```text
3600 / 8200 / 12800 / 17400 / 22000 / 26600
31200 / 35800 / 40400 / 45000 / 49600
```

- [ ] **步骤 3：校准高度与中心线**

依次输入模拟高度 `0、1000、3600、8200`。调整 `pixelsPerFoot` 和 `referenceContentY`，使对应刻度落到中央高度指示位置。若方向相反，只切换 `invertDirection`。

- [ ] **步骤 4：自动滚动检查**

将模拟器切换到 `Automatic`，先使用 `0～10000 ft / 12 秒`，再选择包含一个接缝的小范围慢速检查。确认：

- 刻度只在 Viewport 内显示；
- 滚动连续，没有明显跳动；
- 接缝没有重复刻度或可见空隙；
- Preview 与 Final 切换后高度位置一致；
- Viewport 的人工调整不会被脚本复位。

- [ ] **步骤 5：运行全部相关测试**

运行：

```text
PFDAltitudeTapeTests
PFDHorizonMotionTests
PFDTextureSyncToolTests
```

预期：全部通过。随后读取 Unity Console，预期没有新增 error。

- [ ] **步骤 6：截图验证**

分别截取：

1. 手动高度 `0 ft`；
2. 手动高度 `8200 ft`；
3. 自动模式经过 `8200 ft` 接缝附近。

检查右侧高度带与用户参考图的可见范围和滚动方向一致。

- [ ] **步骤 7：提交最终校准**

如果校准产生 Prefab 或场景变更：

```powershell
git add -- 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Prefab/PFD_Display.prefab' 'AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scene/PFD_demo.unity'
git commit -m '调整：校准PFD高度刻度带显示'
```

如果没有文件变化，则不创建空提交。

## 完成标准

- 12 张高度贴图在 Preview 和 Final 中顺序正确；
- RectMask2D 只显示用户可精调的矩形区域；
- Manual 和 Automatic 两种模拟模式可用；
- `SetAltitude` 可以同步驱动 Guide 与 Final；
- 输入超出范围的高度时安全限制在 `-1000～50000 ft`；
- 所有新增代码注释使用简体中文；
- 所有 PFD 相关测试通过；
- Unity Console 没有新增错误；
- 未覆盖用户现有的 AOA、Prefab 或场景无关改动。

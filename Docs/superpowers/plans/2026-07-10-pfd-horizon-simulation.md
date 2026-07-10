# PFD 地平仪模拟运动实施计划

> **供执行代理使用：** 按测试驱动方式逐项执行，并在每一项完成后重新运行 PFD 地平仪测试。

**目标：** 为现有 PFD Prefab 增加可手动校准、可自动往复的地平仪俯仰与横滚运动。

**架构：** 使用纯数学类计算 Horizon 的位置和角度，运行时控制器只负责绑定与应用结果，模拟器只负责产生测试姿态。以后 JSBSim 数据源直接调用控制器公开入口。

**技术栈：** Unity 2022.3、uGUI RectTransform、C#、NUnit EditMode 测试。

---

### 任务 1：先定义姿态换算行为

**文件：**

- 新建：`AeroSimUnity/Assets/Scripts/Editor/B737/PFDHorizonMotionTests.cs`
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDHorizonMath.cs`

- [ ] 编写失败测试，验证零姿态、`+10` 度俯仰下移 `52` 像素、`+30` 度横滚逆时针旋转，以及组合姿态的位移方向。
- [ ] 在 Unity Test Runner 中运行 `PFDHorizonMotionTests`，确认因为 `PFDHorizonMath` 尚不存在而失败。
- [ ] 实现 `PFDHorizonMath.CalculateAnchoredPosition` 与 `CalculateRotationZ`。
- [ ] 再次运行测试并确认通过。

计划中的公开接口：

```csharp
public static Vector2 CalculateAnchoredPosition(
    Vector2 basePosition,
    float pitchDeg,
    float rollDeg,
    float pixelsPerDegree,
    bool invertPitch,
    bool invertRoll);

public static float CalculateRotationZ(
    float baseRotationZ,
    float rollDeg,
    bool invertRoll);
```

### 任务 2：驱动 Guide 与 Final

**文件：**

- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDHorizonController.cs`
- 修改：`AeroSimUnity/Assets/Scripts/Editor/B737/PFDHorizonMotionTests.cs`

- [ ] 编写失败测试：创建名为 `Guide_Horizon`、`Final_Horizon` 的 RectTransform，调用控制器公开入口后，两个目标都得到相同姿态结果。
- [ ] 运行测试并确认因为控制器不存在而失败。
- [ ] 实现自动绑定、基础位姿缓存、可选平滑、`SetAttitude` 与 `ResetAttitude`。
- [ ] 运行测试并确认通过。

计划中的公开入口：

```csharp
public void SetAttitude(float pitchDeg, float rollDeg);
public void ResetAttitude();
```

### 任务 3：增加手动与自动模拟

**文件：**

- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDAttitudeSimulator.cs`
- 修改：`AeroSimUnity/Assets/Scripts/Editor/B737/PFDHorizonMotionTests.cs`

- [ ] 编写失败测试，验证自动模式在周期起点输出零姿态、四分之一周期输出正峰值。
- [ ] 运行测试并确认因为模拟器不存在而失败。
- [ ] 实现 `Manual`、`Automatic` 两种模式以及 `EvaluateAutomaticAttitude`。
- [ ] 运行全部 PFD 地平仪测试并确认通过。

计划中的公开计算入口：

```csharp
public static Vector2 EvaluateAutomaticAttitude(
    float time,
    float pitchAmplitude,
    float pitchPeriod,
    float rollAmplitude,
    float rollPeriod);
```

### 任务 4：Unity 场景验证

**文件：**

- 不直接修改 Prefab YAML，由 Unity Inspector 完成挂载与保存。

- [ ] 等待 Unity 完成脚本编译，检查 Console 无错误。
- [ ] 在 `PFD_Display` 上添加 `PFDHorizonController` 与 `PFDAttitudeSimulator`。
- [ ] 自动模式下确认 Horizon 连续运动，静态覆盖层与 Wings 不动。
- [ ] 手动模式输入 `Pitch=10`、`Roll=0`，确认下移约 `52` 像素。
- [ ] 手动模式输入 `Pitch=0`、`Roll=30`，确认逆时针旋转 `30` 度。
- [ ] 使用现有 Preview/Final 切换按钮确认两层姿态一致。
- [ ] 将组件改动应用回 `PFD_Display.prefab`。


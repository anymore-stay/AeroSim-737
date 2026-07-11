# PFD 座舱显示与 JSBSim 数据接入实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在左右驾驶舱 PFD 屏幕正确显示 `PFD_Display`，并接入 JSBSim 七项实时飞行数据。

**Architecture:** 左右各使用独立的 UI 相机、RenderTexture、材质和物理 Plane；每个 PFD 实例通过轻量驱动器订阅同一个 `JsbsimBridge`。现有 PFD 控制器继续负责动画和限幅，驱动器只做字段映射与单位换算。

**Tech Stack:** Unity 2022.3.62f3c1、uGUI、URP、RenderTexture、JSBSim UDP 5501、Unity Test Framework。

---

### 任务一：实时数据适配器

**文件：**
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDJsbsimDataMath.cs`
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDJsbsimDataDriver.cs`
- 修改：`AeroSimUnity/Assets/Scripts/Aircraft/B737/JsbsimBridge.cs`
- 测试：`AeroSimUnity/Assets/Scripts/Editor/B737/PFDJsbsimDataDriverTests.cs`

- [ ] 先编写磁航向归一化和垂直速度换算失败测试。
- [ ] 运行测试，确认因适配数学类不存在而失败。
- [ ] 实现最小数学适配和驱动器。
- [ ] 给桥接器增加 `AngleOfAttackDeg` 只读属性。
- [ ] 运行测试确认通过。

### 任务二：左右 PFD 渲染链路

**文件：**
- 新建：左右 PFD RenderTexture、材质和运行时 Rig 资源。
- 修改：`AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab`
- 修改：`AeroSimUnity/Assets/Scenes/MainScene.unity`

- [ ] 配置 `PFD_Left`、`PFD_Right` 两个 Layer。
- [ ] 创建左右 512×512 RenderTexture 和专用材质。
- [ ] 创建左右 PFD 相机并绑定对应 RenderTexture。
- [ ] 实例化两份 PFD，绑定相机和对应 Layer。
- [ ] 复制左右 ND Plane，移动到外侧 PFD 屏口并绑定 PFD 材质。
- [ ] 确保 Plane 位于屏幕污渍网格后方。

### 任务三：正式运行配置

- [ ] 在两个正式 PFD 实例中禁用六个模拟器组件。
- [ ] 挂载并配置 `PFDJsbsimDataDriver`。
- [ ] 保持 `PFD_Final` 开启、`PFD_PreviewGuide` 关闭。
- [ ] 首包前初始化为零，断流后冻结最后有效值。

### 任务四：验证

- [ ] 等待 Unity 编译完成并检查 Console。
- [ ] 运行相关 EditMode 测试和全量 EditMode 测试。
- [ ] 在 Scene/Game 视图检查左右 PFD 画面与污渍遮挡关系。
- [ ] 运行 `JSBSimBridge/start_jsbsim.bat` 验证七项数据方向和单位。
- [ ] 记录左右 Plane 的 Inspector 微调方法。

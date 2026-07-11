# PFD 玻璃质感材质实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将左右 PFD 材质迁移到 URP Lit，在不改变布局的前提下获得与 ND 接近的微弱玻璃反射。

**Architecture:** 继续使用现有左右 RenderTexture 和物理 Plane，只调整独立 PFD 材质及生成工具。测试验证 Shader、纹理绑定、反射参数和 Plane Transform 保持不变。

**Tech Stack:** Unity 2022.3.62f3c1、URP 14、Unity Test Framework、RenderTexture。

---

### 任务一：用测试定义 PFD 玻璃材质规则

**文件：**
- 修改：`AeroSimUnity/Assets/Scripts/Editor/B737/B737PFDDisplayRigTests.cs`

- [ ] 增加测试 `PfdMaterialsUseLitGlassSettingsAndKeepIndependentTextures`。
- [ ] 验证左右材质使用 `Universal Render Pipeline/Lit`。
- [ ] 验证 BaseMap、MainTex、EmissionMap 分别绑定对应 PFD RenderTexture。
- [ ] 验证 Smoothness 为 `0.35`、Metallic 为 `0`，并开启高光、环境反射与 Emission。
- [ ] 运行该测试，确认当前 Unlit 材质导致测试失败。

### 任务二：迁移材质和生成逻辑

**文件：**
- 修改：`AeroSimUnity/Assets/Scripts/Editor/B737/B737PFDDisplayRigEditorUtility.cs`
- 修改：`AeroSimUnity/Assets/Aircraft/B737/Materials/PFD_Left.mat`
- 修改：`AeroSimUnity/Assets/Aircraft/B737/Materials/PFD_Right.mat`

- [ ] 将 `EnsureMaterial` 改为使用 URP Lit。
- [ ] 配置 BaseMap、MainTex、EmissionMap、EmissionColor、Smoothness、Metallic、环境反射和高光。
- [ ] 使用生成工具更新现有两份材质，并记录应用前后的左右 Plane Transform。
- [ ] 运行新测试，确认通过。

### 任务三：视觉与回归验证

- [ ] 运行相关 EditMode 测试。
- [ ] 运行全量 EditMode 测试。
- [ ] 启动 Unity 运行态，使用同一驾驶舱视角截图。
- [ ] 确认 PFD 出现微弱高光和反射，且数字可读性、左右画面和 Plane Transform 均未改变。
- [ ] 检查 Unity Console 无新增 PFD 编译或运行错误。


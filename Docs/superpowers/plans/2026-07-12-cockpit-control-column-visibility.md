# 驾驶舱操纵杆显隐功能实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在驾驶舱相机启用时使用普通数字键 1 切换操纵杆显示，并在 HUD 显示操作说明。

**Architecture:** 新增独立 B737 运行时组件负责目标绑定、相机模式判断和显隐状态，不把飞机部件逻辑写入相机或 HUD。HUD 只增加静态快捷键说明，B737 Prefab 保存目标引用。

**Tech Stack:** Unity 2022.3、C#、uGUI、Unity Test Framework、Unity Prefab YAML

---

### Task 1: 编写控制逻辑测试

**Files:**
- Create: `AeroSimUnity/Assets/Scripts/Editor/B737/B737CockpitControlColumnVisibilityTests.cs`
- Test: `AeroSimUnity/Assets/Scripts/Editor/B737/B737CockpitControlColumnVisibilityTests.cs`

- [ ] **Step 1: 写失败测试**

测试期望的公开接口：`SetBindings(GameObject, CockpitCameraController)`、`TryToggle(bool)` 和 `IsControlColumnVisible`。验证默认显示、驾驶舱允许切换、非驾驶舱拒绝切换。

- [ ] **Step 2: 运行测试确认失败**

运行 `B737CockpitControlColumnVisibilityTests`，预期因为类型尚不存在而编译失败或测试无法发现。

### Task 2: 实现独立显隐控制组件

**Files:**
- Create: `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737CockpitControlColumnVisibility.cs`
- Modify: `AeroSimUnity/Assets/Scripts/Editor/B737/B737CockpitControlColumnVisibilityTests.cs`

- [ ] **Step 1: 实现最小运行时逻辑**

组件使用 `KeyCode.Alpha1`，目标为空时按相对路径 `操纵杆/ImpEmpty.001_x24e_47969` 自动查找。`Update` 只在 Cockpit 模式且相机组件与 Camera 均启用时切换。

- [ ] **Step 2: 运行控制逻辑测试**

预期默认显示、Cockpit 切换和非 Cockpit 忽略全部通过。

### Task 3: 更新 HUD 与 Prefab

**Files:**
- Modify: `AeroSimUnity/Assets/Scripts/Aircraft/B737/FlightHud.cs`
- Modify: `AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab`
- Modify: `AeroSimUnity/Assets/Scripts/Editor/B737/B737CockpitControlColumnVisibilityTests.cs`

- [ ] **Step 1: 增加 Prefab 和 HUD 失败测试**

验证 B737 根节点存在控制组件、目标引用为 `ImpEmpty.001_x24e_47969`，并验证 HUD 源码包含 `1 操纵杆显示/隐藏（仅驾驶舱）`。

- [ ] **Step 2: 修改 HUD 文案**

在相机按键区域追加一行：`1 操纵杆显示/隐藏（仅驾驶舱）`。

- [ ] **Step 3: 将组件挂到 B737 Prefab**

使用 Unity Prefab API 添加组件并绑定目标节点，保留其他 Prefab 组件和用户位置调整。

- [ ] **Step 4: 运行测试与编译检查**

运行目标 EditMode 测试，并检查 Unity Console 没有新增编译错误或 Missing Reference。

### Task 4: 最终验证

**Files:**
- Verify: `AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab`
- Verify: `AeroSimUnity/Assets/Scripts/Aircraft/B737/FlightHud.cs`

- [ ] **Step 1: 检查 Git 差异和冲突标记**

确认只包含新控制脚本、测试、HUD 文案和 Prefab 组件。

- [ ] **Step 2: 运行完整目标测试**

运行 `B737CockpitControlColumnVisibilityTests`，要求 0 失败。

- [ ] **Step 3: 检查 Unity Console**

确认没有本功能产生的编译错误。

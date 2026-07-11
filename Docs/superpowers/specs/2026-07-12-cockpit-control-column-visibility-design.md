# 驾驶舱操纵杆显隐功能设计

## 目标

在 Play 模式中，仅当当前启用的相机属于 `CockpitCameraController.CameraMode.Cockpit` 时，允许玩家使用普通数字键 `1` 切换驾驶舱操纵杆模型的显示状态。

## 控制对象

- 飞机 Prefab：`Assets/Aircraft/B737/Prefabs/B737.prefab`
- 目标节点：`操纵杆/ImpEmpty.001_x24e_47969`
- 初始状态：进入 Play 时显示

## 实现结构

新增 `B737CockpitControlColumnVisibility` 运行时脚本，放在 `Assets/Scripts/Aircraft/B737/` 并挂载到 B737 根节点。

脚本职责：

1. 自动或通过序列化引用绑定目标操纵杆节点。
2. 每帧确认当前启用相机是否带有 Cockpit 模式的 `CockpitCameraController`。
3. 仅在驾驶舱视角下响应 `KeyCode.Alpha1`。
4. 每次按键切换目标节点的 `activeSelf`。
5. 切换到其他视角时保留操纵杆当前显隐状态，返回驾驶舱后继续使用上次状态。

不把逻辑写入相机控制器或 HUD，避免相机、UI 和飞机部件控制职责混合。

## HUD

在 `FlightHud` 的相机快捷键区域增加：

```text
1 操纵杆显示/隐藏（仅驾驶舱）
```

普通数字键 `1` 与现有 `Shift+数字键` 相机切换不冲突。

## 异常处理

- 找不到目标节点时不抛出运行时异常，只输出一次中文警告并保持功能不可用。
- 非 Play 模式不执行显隐切换。
- Cabin 和 ThirdPerson 模式按 `1` 不产生任何变化。

## 验证

- 默认状态为显示。
- Cockpit 模式按键可在显示和隐藏之间切换。
- Cabin、ThirdPerson 模式不响应。
- 离开并返回 Cockpit 后保留上次状态。
- B737 Prefab 中组件和目标引用有效。
- HUD 包含对应快捷键说明。

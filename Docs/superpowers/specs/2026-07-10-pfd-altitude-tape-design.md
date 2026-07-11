# PFD 高度刻度带设计说明

## 1. 目标

在现有 `PFD_Display` Prefab 中加入波音 737 PFD 右侧高度刻度带，实现：

- 12 张高度刻度图片按高度顺序首尾衔接；
- 刻度只显示在可手工精调的矩形区域内；
- 输入模拟高度后，刻度带连续、平滑地上下滚动；
- 当前阶段不制作中间白框内的高度数字滚轮；
- 后续可以将模拟高度替换为 `JsbsimBridge.AltitudeFt`。

高度贴图位于：

`Assets/Aircraft/B737/Instruments/PFD/Textures/PreviewRGB/ALT_Tapes`

对应的最终显示贴图位于：

`Assets/Aircraft/B737/Instruments/PFD/Textures/Used/ALT_Tapes`

贴图覆盖约 `-1000～50000 ft`，共 12 张。前 11 张尺寸为 `70×2048`，最后一张尺寸为 `70×220`。

## 2. 本阶段范围

本阶段只实现以下内容：

1. 高度刻度图片拼接；
2. 灰色矩形区域遮罩；
3. 使用模拟高度驱动连续滚动；
4. 支持手动模式与自动升降模式；
5. 保留方便人工精调的布局和校准参数。

本阶段不实现：

- 中央白框中的当前高度数字滚轮；
- 气压基准显示；
- 高度趋势向量；
- MCP、最低高度、选定高度等其他符号；
- 直接读取 `JsbsimBridge.AltitudeFt`。

## 3. 选定方案

采用 `RectMask2D + 12 张 Image + 移动 Content` 的方案。

选择理由：

- 只有 12 个 UI 图片对象，性能开销很小；
- Scene 视图中可以直接观察图片接缝；
- Viewport 的矩形位置和尺寸可以由开发者手工精调；
- Content 的位置、缩放和接缝可以独立调整；
- 与现有 `PFD_PreviewGuide → PFD_Final` 生成流程兼容；
- 后续接入真实高度数据时不需要改动 UI 层级。

暂不采用循环复用图片或 Shader 方案，因为它们会增加边界切换、纹理采样和调试复杂度，对 12 张图片没有明显收益。

## 4. Prefab 层级

预览层新增以下结构：

```text
PFD_PreviewGuide
└── Guide_AltitudeTapeViewport
    └── Guide_AltitudeTapeContent
        ├── Guide_ALT_-10_036
        ├── Guide_ALT_036_082
        ├── Guide_ALT_082_128
        ├── Guide_ALT_128_174
        ├── Guide_ALT_174_220
        ├── Guide_ALT_220_266
        ├── Guide_ALT_266_312
        ├── Guide_ALT_312_358
        ├── Guide_ALT_358_404
        ├── Guide_ALT_404_450
        ├── Guide_ALT_450_496
        └── Guide_ALT_496_500
```

`Guide_AltitudeTapeViewport`：

- 挂载 `RectMask2D`；
- 它的 `RectTransform` 决定最终可见的矩形区域；
- 开发者可以直接使用 Unity 的 Rect Tool 调整位置、宽度和高度；
- 本对象不参与滚动。

`Guide_AltitudeTapeContent`：

- 作为 12 张高度贴图的共同父对象；
- 只通过改变 `anchoredPosition.y` 实现滚动；
- 可以单独调整 X 位置和整体缩放；
- 不改变 Viewport 的遮罩范围。

12 个贴图对象：

- 使用 `UnityEngine.UI.Image`；
- 按低高度到高高度的顺序从下向上排列；
- 关闭 `Raycast Target`；
- 保持统一宽度比例；
- 允许每张图片设置独立的 Y 修正值。

现有 `PFDLayerGeneratorEditor` 会复制预览层，将 `Guide_` 前缀改为 `Final_`，并把 `PreviewRGB` Sprite 替换成相同路径结构下的 `Used` Sprite，因此不需要为最终层另写一套生成逻辑。

最终层生成后，根节点上的高度带控制器同时保存 `Guide_AltitudeTapeContent` 和 `Final_AltitudeTapeContent` 引用，并同步移动两个 Content。这样无论当前显示预览层还是最终层，刻度位置都保持一致。该处理方式与现有 `PFDHorizonController` 同时驱动 Guide 和 Final 的结构一致。

## 5. 图片拼接规则

相邻贴图在边界处包含重复高度刻度。例如一张图片顶部和下一张图片底部可能同时包含 `3600` 刻度。因此拼接时需要允许相邻图片重叠，不能只按图片外框紧贴。

拼接采用两级校准：

1. `segmentOverlap`：所有相邻图片共用的基础重叠量；
2. 单张 Y 修正：仅在个别接缝无法通过统一重叠量对齐时使用。

自动排列只在开发者明确执行“重新排列高度带图片”时运行，不在每帧或每次 Inspector 修改时自动重排，避免覆盖人工微调结果。

推荐调试顺序：

1. 暂停高度滚动；
2. 从最低高度图片开始向上排列；
3. 调整统一重叠量，使重复边界刻度重合；
4. 对个别图片设置独立 Y 修正；
5. 保存 Prefab；
6. 打开模拟滚动检查跨接缝效果。

## 6. 控制组件

### 6.1 PFDAltitudeTapeController

主要职责：

- 保存预览层与最终层的 Viewport、Content 和高度带图片引用；
- 根据高度计算 Content 的目标 Y 坐标，并同步应用到 Guide 与 Final；
- 将高度限制在贴图支持范围内；
- 提供图片重新排列入口；
- 提供人工校准参数；
- 对缺失引用输出中文警告并安全停止更新。

公开调用接口：

```csharp
public void SetAltitude(float altitudeFt)
```

主要可调参数：

- `minimumAltitudeFt`：最低高度，默认 `-1000`；
- `maximumAltitudeFt`：最高高度，默认 `50000`；
- `pixelsPerFoot`：每英尺对应的 UI 像素；
- `referenceAltitudeFt`：参考高度，默认可使用 `0`；
- `referenceContentY`：参考高度位于中心指示线时的 Content Y 坐标；
- `invertDirection`：反转滚动方向；
- `segmentOverlap`：相邻贴图统一重叠量；
- 各贴图的独立 Y 修正值。

高度换算公式：

```text
高度差 = 限制后的高度 - 参考高度
滚动距离 = 高度差 × 每英尺像素数
目标位置Y = 参考位置Y + 有方向符号的滚动距离
```

控制器只改变 Guide 与 Final Content 的 Y 坐标，不改变它们的 X 坐标、缩放和 Viewport 尺寸，保证人工布局调整不会被运行时代码覆盖。Final 尚未生成或暂时未绑定时，Guide 仍可独立调试；Final 生成后控制器会同步驱动两者。

### 6.2 PFDAltitudeTapeSimulator

模拟器提供两种模式：

- `Manual`：通过 Inspector 中的 `simulatedAltitudeFt` 手动设置高度；
- `Automatic`：在指定最低、最高测试高度之间自动往返。

模拟器只调用 `PFDAltitudeTapeController.SetAltitude`，不直接修改 RectTransform。以后接入真实数据时，禁用模拟器并将 `JsbsimBridge.AltitudeFt` 传给同一个接口即可。

## 7. 可调性要求

开发者必须能够在不修改代码的情况下调整：

- 遮罩矩形的位置、宽度和高度；
- 高度带在遮罩内的左右位置；
- 高度带整体缩放；
- 图片基础重叠量；
- 单张图片的接缝修正；
- 每英尺像素数；
- 参考高度和参考位置；
- 模拟高度与自动模拟范围；
- 滚动方向。

脚本中新增的代码注释必须全部使用简体中文。

## 8. 边界与异常处理

- 输入高度低于 `-1000 ft` 时固定在最低位置；
- 输入高度高于 `50000 ft` 时固定在最高位置；
- Viewport 或 Content 未绑定时不抛出空引用异常；
- 高度带图片引用缺失时输出明确的中文警告；
- `pixelsPerFoot` 小于等于零时拒绝应用无效滚动；
- 自动模式的上下限顺序错误时进行安全纠正或停止模拟；
- 不在运行时创建或销毁高度图片，避免不必要的分配和画面抖动。

## 9. 验证标准

编辑模式验证：

- 高度到 Content Y 坐标的计算结果正确；
- 高度范围限制正确；
- 正向和反向滚动计算正确；
- 图片自动排列顺序正确；
- 单张修正值不会影响其他图片。

Prefab 与画面验证：

- Viewport 可在 Scene 视图中自由调整；
- 高度带不会显示到 Viewport 矩形以外；
- 手动输入 `0、1000、3600、8200 ft` 时，对应刻度可以校准到中央指示位置；
- 自动模式经过所有选定测试接缝时连续、无闪烁、无明显跳动；
- 修改 Viewport 尺寸后滚动逻辑仍然有效；
- 在 Preview 与 Final 之间切换时，高度刻度位置保持一致；
- 重新生成 `PFD_Final` 后，节点命名和 PreviewRGB 到 Used 的 Sprite 替换正确；
- Unity Console 中没有新增错误。

## 10. 后续真实数据接入

完成当前画面后，真实数据接入保持最小改动：

1. 禁用或移除 `PFDAltitudeTapeSimulator`；
2. 获取 `JsbsimBridge` 引用；
3. 在合适的 PFD 数据驱动组件中调用：

```csharp
altitudeTapeController.SetAltitude(jsbsimBridge.AltitudeFt);
```

高度刻度带控制器不直接依赖 `JsbsimBridge`，以便继续单独调试和测试 UI。

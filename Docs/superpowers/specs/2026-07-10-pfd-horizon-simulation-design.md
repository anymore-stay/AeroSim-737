# PFD 地平仪模拟运动设计

## 目标

让当前 `PFD_Display` 中的 `Guide_Horizon` 和 `Final_Horizon` 使用模拟俯仰角、横滚角运动，并保留一个以后可直接接入 JSBSim 的角度输入入口。

## 当前约束

- Horizon 贴图当前固定校正角为 `-90` 度，运行时必须保留该基础角度。
- 当前 Horizon 的基础位置由 Prefab 决定，脚本运行时读取，不写死坐标。
- 贴图主刻度间隔为每 `10` 度 `52` 像素，因此默认比例为每度 `5.2` 像素。
- `Guide_HorizonOverlay`、`Guide_Wings`、背景和遮罩保持静止。
- Preview 与 Final 可以切换，因此两个 Horizon 需要始终使用同一姿态。

## 结构

### PFDHorizonMath

只负责姿态到 UI 位姿的纯数学转换。正俯仰使地平线向下移动；正横滚使地平线逆时针旋转。俯仰位移向量会随横滚角旋转。

### PFDHorizonController

挂在 `PFD_Display` 上，自动寻找 `Guide_Horizon` 和 `Final_Horizon`，记录两者的基础位置和基础角度，并将目标姿态应用到两个对象。可选平滑只影响显示过程，不改变刻度换算。

### PFDAttitudeSimulator

挂在同一个 `PFD_Display` 上，提供两种模式：

- 手动模式：在 Inspector 中直接修改俯仰角和横滚角。
- 自动模式：俯仰和横滚使用独立周期的正弦波往复变化。

以后新增 JSBSim 数据源时，只需把 `theta_rad`、`phi_rad` 转成角度后调用 `PFDHorizonController.SetAttitude`。

## 运动规则

- `pitchDeg = +10`、`rollDeg = 0` 时，Horizon 相对基础位置向下移动 `52` 像素。
- `pitchDeg = 0`、`rollDeg = +30` 时，Horizon 相对基础角度逆时针旋转 `30` 度。
- 两者同时存在时，俯仰位移向量也逆时针旋转相同角度。
- `pitchDeg = 0`、`rollDeg = 0` 时，完全恢复基础位置和基础角度。

## 验证

- EditMode 测试验证零姿态、俯仰比例、横滚方向、组合运动和自动模拟曲线。
- Play Mode 中分别检查手动模式、自动模式及 Preview/Final 切换。
- Unity Console 不应出现编译错误、Missing Script 或空引用异常。

## 本次不包含

- JSBSim 实际连接。
- `bank_diamond` 的横滚指针运动。
- 速度、高度、航向和垂直速度滚动带。


# PFD 座舱显示与 JSBSim 数据接入设计

## 目标

把 `PFD_Display.prefab` 显示到波音 737 驾驶舱左右两块 PFD 屏幕内，画面位于屏幕污渍层后方，并接入 JSBSim 的实时空速、俯仰、横滚、航向、迎角、高度和垂直速度数据。

## 显示架构

左右两侧各使用一套独立显示链路：

```text
PFD_Display 实例
→ 专用正交相机
→ 512×512 RenderTexture
→ 专用材质
→ 驾驶舱 PFD Plane
```

左右 PFD Plane 挂到 `B737/驾驶舱/屏幕污渍` 下，与现有 `ND_Plane`、`EICAS1_Plane` 同级。Plane 位于污渍网格后方，因此污渍继续覆盖在仪表画面上。

左右链路使用独立 Layer、相机、RenderTexture 和材质，避免串画面，并为以后机长侧和副驾驶侧独立设置保留空间。

## 数据架构

每个正式 PFD 实例挂载一个 `PFDJsbsimDataDriver`。驱动器订阅 `JsbsimBridge.OnStateUpdated`，收到新状态后调用现有控制器公开接口，不修改仪表内部数学和布局参数。

| PFD 数据 | 数据来源 | 处理 |
|---|---|---|
| 指示空速 | `JsbsimBridge.SpeedKts` | 单位节 |
| 俯仰 | `JsbsimBridge.PitchDeg` | 单位度 |
| 横滚 | `JsbsimBridge.RollDeg` | 单位度 |
| 航向 | `JsbsimBridge.HeadingDeg` | 减去磁差并归一化到 0～360 度 |
| 迎角 | `aero_alpha_deg` | 在桥接器中增加强类型只读属性，单位度 |
| 高度 | `JsbsimBridge.AltitudeFt` | 海平面高度，单位英尺 |
| 垂直速度 | `JsbsimBridge.VerticalSpeedFps` | 乘 60 转为英尺/分钟 |

当前 JSBSim 输出只有真航向，没有磁差。驱动器提供可调磁差，计算规则与现有 ND 保持一致：`磁航向 = 真航向 - 磁差`，东偏磁差为正。

## 模拟数据规则

模拟脚本保留在源 PFD Prefab 中，用于独立预览。正式座舱左右 PFD 实例禁用六个模拟器组件，确保只有 JSBSim 驱动器写入控制器。

首个有效数据包到达前显示零值和水平姿态。数据中断后冻结最后有效值，不自动恢复模拟动画。

## 不在本次范围内

MCP 目标速度、预选高度、预选航向和驾驶员气压设定没有可靠的当前 JSBSim 字段，本次继续使用 PFD 现有默认值。

## 验收标准

- 左右 PFD 均在对应物理屏口内显示，且位于屏幕污渍层后方。
- 左右相机不会拍到对方的 PFD。
- 七项飞行数据按正确字段、符号和单位更新。
- 正式座舱实例中的模拟器不再与实时数据抢写。
- Unity 无新增编译错误，相关 EditMode 测试通过。
- Plane 的位置、比例和深度可在 Inspector 中独立微调。

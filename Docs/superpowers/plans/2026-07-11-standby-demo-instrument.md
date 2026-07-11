# Standby 模拟仪表实施计划

> **执行要求：** 使用测试驱动方式逐项实现，所有代码注释使用中文。

**目标：** 在 `Standby_demo` 场景中完成一个包含模拟数据的 Standby 仪表，正确显示空速、高度、数字滚轮、俯仰和横滚。

**架构：** `StandbyDisplayController` 只负责把输入数据转换为 UI 姿态；`StandbyDemoDataSource` 只生成缓慢的模拟数据；Editor 构建工具负责按 278×278 参考坐标生成 Prefab 层级、遮罩和资源引用。正式控制器不依赖 Demo 数据源，后续可直接替换为 JSBSim 驱动。

**技术栈：** Unity 2022.3、uGUI、RectMask2D、RawImage、NUnit 编辑模式测试、Unity Editor API。

---

### 任务一：显示数学与数字滚轮测试

**文件：**
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/Standby/Scripts/StandbyDisplayMath.cs`
- 新建：`AeroSimUnity/Assets/Scripts/Editor/B737/StandbyDisplayMathTests.cs`

- [ ] 编写空速带和高度带偏移测试。
- [ ] 编写俯仰位移和横滚角度测试。
- [ ] 编写空速三位数字轮测试。
- [ ] 编写高度前三位与末两位五档滚轮测试。
- [ ] 运行测试并确认实现前失败。
- [ ] 实现最小数学代码并确认测试通过。

### 任务二：运行时显示控制器

**文件：**
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/Standby/Scripts/StandbyDisplayController.cs`
- 新建：`AeroSimUnity/Assets/Aircraft/B737/Instruments/Standby/Scripts/StandbyDemoDataSource.cs`

- [ ] 实现 `SetAirspeedKnots`、`SetAltitudeFeet`、`SetAttitudeDegrees` 接口。
- [ ] 移动空速带和高度带 Content。
- [ ] 更新三个空速 RawImage 数字轮的 UV。
- [ ] 更新高度前三位和末两位 RawImage 数字轮的 UV。
- [ ] 根据俯仰移动地平线，根据横滚旋转姿态内容。
- [ ] 模拟数据从空速 40 kt、高度 0 ft、姿态 0° 开始并缓慢往返。

### 任务三：资源转换与 Prefab 构建

**文件：**
- 新建：`AeroSimUnity/Assets/Scripts/Editor/B737/StandbyInstrumentBuilder.cs`
- 修改：`AeroSimUnity/Assets/Aircraft/B737/Instruments/Standby/Prefab/Standby.prefab`

- [ ] 将原始 PNG 配置为 Sprite/RawImage 可用、无压缩、无 Mipmap、Clamp。
- [ ] 生成 278×278 根节点和黑色背景。
- [ ] 建立中央姿态、左侧速度带、右侧高度带、两个数字框的独立遮罩。
- [ ] 高度七段按照 7000 ft 的中心间距排列，保留 200 ft 重叠区域。
- [ ] 绑定控制器和模拟数据源引用。
- [ ] 保存 Prefab 并刷新 `Standby_demo` 实例。

### 任务四：视觉校准与验证

- [ ] 在 `Standby_demo` 中进入 Play，确认速度带和高度带连续滚动。
- [ ] 确认空速数字轮与空速带数值一致。
- [ ] 确认高度数字轮与高度带数值一致。
- [ ] 确认抬头时地平线向下、右滚时地平线按正确方向旋转。
- [ ] 检查 Console 无编译错误和运行时异常。
- [ ] 运行 Standby 编辑模式测试。
- [ ] 截图比对 278×278 参考图并调整零点、遮罩和像素比例。

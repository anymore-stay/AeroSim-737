# UniStorm URP 全局天气着色器实施计划

> **供智能体执行：** 需要按任务逐项实现、编译并在 Unity 中验证。

**目标：** 在 URP 14 中恢复 UniStorm 的降雨湿润与降雪积雪材质效果。

**架构：** 新增一个 URP 前向光照着色器，直接读取 UniStorm 已写入的全局 `_WetnessStrength` 与 `_SnowStrength`。四个 UniStorm 演示材质继续使用各自的主贴图、法线贴图、积雪贴图与参数，仅替换着色器引用。

**技术栈：** Unity 2022.3、URP 14、ShaderLab、HLSL、UniStorm 5.4.1。

---

### 任务 1：新增 URP 全局天气着色器

**文件：**

- 新建：`AeroSimUnity/Assets/Plugins/ThirdParty/UniStormURPCompat/Shaders/UniStormGlobalWeatherLit.shader`

- [ ] 声明 `_BaseMap`、`_BumpMap`、`_SnowLayerTex`、`_SnowLayerBump`、`_SnowLayerColor`、`_SnowDirection`、`_Smoothness`、`_SnowSmoothness`、`_WetnessMultiplier`。
- [ ] 在片元阶段读取 `_WetnessStrength` 与 `_SnowStrength`。
- [ ] 用世界法线与 `_SnowDirection` 生成积雪遮罩；用世界法线向上分量生成湿润遮罩。
- [ ] 使用 URP 前向光照函数输出基础色、法线与平滑度，且保持阴影、主光与环境光可用。

### 任务 2：接入 UniStorm 演示材质

**文件：**

- 修改：`AeroSimUnity/Assets/UniStorm Weather System/Materials/Demo/Rock.mat`
- 修改：`AeroSimUnity/Assets/UniStorm Weather System/Materials/Demo/Sand.mat`
- 修改：`AeroSimUnity/Assets/UniStorm Weather System/Materials/Demo/Dirt.mat`
- 修改：`AeroSimUnity/Assets/UniStorm Weather System/Materials/Demo/Wood.mat`

- [ ] 将四个材质的着色器改为 `UniStorm/URP/Global Weather Lit`。
- [ ] 保留 `_BaseMap`、`_BumpMap`、积雪纹理、积雪颜色与平滑度参数。

### 任务 3：验证

**场景：**

- `AeroSimUnity/Assets/UniStorm Weather System/URP Support/Scene/URP Demo Scene.unity`
- `AeroSimUnity/Assets/UniStorm Weather System/Scenes/Standard Demo.unity`

- [ ] 刷新资源并确认控制台没有 C# 或着色器编译错误。
- [ ] 在晴天、降雨、降雪状态下检查 Rock、Sand、Dirt 的基础贴图、湿润高光和积雪覆盖。
- [ ] 确认场景中不再出现粉色材质。

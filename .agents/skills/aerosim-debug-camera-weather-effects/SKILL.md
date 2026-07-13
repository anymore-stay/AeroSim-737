---
name: aerosim-debug-camera-weather-effects
description: Use when AeroSim-737 中的 UniStorm 雨、雪、冰雹、雾、沙尘、飘动物、闪电、云影或天气声音在驾驶舱、客舱、第三人称相机之间出现密度不一致、缺失、偏移、延迟、覆盖不足、未跟随、过度跟随或只在某个视角正常。
---

# AeroSim-737 多视角天气特效诊断

## 核心原则

先确定天气效果的空间语义，再决定它跟随相机、飞机、AudioListener 或世界坐标。不要把所有天气都套用雨雪的相机中心 Box，也不要用“调大粒子数量”代替覆盖与预算计算。

## 开始前必须执行

1. 从仓库根目录读取 `AGENTS.md` 或 `CLAUDE.md`。
2. 执行 `git status --short`，记录并保护用户现有改动。
3. **完整读取** `../../../Docs/UniStorm-Camera-Weather-Effects-Guide.md`。该文件是本 Skill 的唯一详细事实源。
4. 确认任务是诊断、修改还是仅给出调参建议。诊断请求不自动授权修改。
5. 如果不能控制 Unity，继续完成静态排查，但在交付中明确标注“尚未完成 Play 视觉验证”。

详细指南缺失或无法读取时，停止天气修改并报告路径问题。不得凭通用 Unity 经验替代项目事实。

## 工作清单

复制并跟踪：

```text
天气排查进度：
- [ ] 记录工作区现有改动
- [ ] 完整读取详细指南
- [ ] 记录天气、相机、距离、方向和发生时机
- [ ] 分类主效果、附加效果、全局效果、世界事件或声音
- [ ] 确认实际活动相机与 AudioListener
- [ ] 读取 MainScene 的真实序列化值
- [ ] 读取 WeatherType 资产和粒子 Prefab
- [ ] 计算覆盖、面积密度、寿命和粒子预算
- [ ] 写出会失败的最小测试
- [ ] 实施最小项目级修改
- [ ] 运行静态检查和相关测试
- [ ] 完成人工视角验证或明确交给用户验证
- [ ] 更新详细指南
```

## 固定读取顺序

### 1. 相机链路

读取：

- `AeroSimUnity/Assets/Scripts/Camera/CameraManager.cs`
- `AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab`

确认：

- `CameraManager.ActiveCamera`；
- `Shift+7` 客舱、`Shift+8` 驾驶舱、`Shift+9` 第三人称；
- 第三人称 `18～95 米`范围；
- 当前唯一启用的 `AudioListener`；
- 仪表相机和 RenderTexture 相机没有被误选。

活动主相机优先使用 `CameraManager.ActiveCamera`。不要只用 `Camera.main` 或 `MainCamera` 标签。

### 2. 项目天气控制器

读取：

- `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormPrecipitationAnchor.cs`
- `AeroSimUnity/Assets/Scripts/Editor/B737/B737UniStormPrecipitationAnchorTests.cs`
- `AeroSimUnity/Assets/Scenes/MainScene.unity`

区分四类事实：

1. 已实现的行为；
2. 场景当前生效参数；
3. 只存在但未进入运行时主链路的辅助函数；
4. 尚未覆盖的天气或视角。

特别注意：`CalculateCoverageRadius()` 等方法存在，不代表 `LateUpdate()` 已调用。当前近景半宽实际固定为场景中的 `minimumCoverageRadius`。

### 3. UniStorm 原生链路

读取：

- `AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/UniStormSystem.cs`
- `AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/WeatherType.cs`

确认：

- `UniStorm Effects` 和 `UniStorm Sounds` 的创建与父物体；
- `WeatherEffectsList` 与 `AdditionalWeatherEffectsList`；
- `CurrentParticleSystem` 与 `AdditionalCurrentParticleSystem`；
- `ParticleFadeSequence()` 和 `AdditionalParticleFadeSequence()`；
- 云覆盖等待和 `TransitionSpeed`；
- 当前项目控制器是否在 `LateUpdate` 覆盖原生渐变值。

### 4. 当前天气资产

读取目标 WeatherType `.asset` 及其主、附加 ParticleSystem Prefab。记录：

- `PrecipitationWeatherType`；
- `UseWeatherEffect`；
- `UseAdditionalWeatherEffect`；
- `ParticleEffectAmount`；
- `AdditionalParticleEffectAmount`；
- Shape；
- Simulation Space；
- 生命周期；
- 速度；
- `maxParticles`；
- Collision；
- Sub Emitters。

不要只根据 Prefab 名称判断类别。

### 5. 类别专用文件

- 雾：`B737UniStormFogDistanceController.cs`
- 闪电和雷声：`LightningSystem.cs`
- 云与云影：`UniStormClouds.cs` 及当前 URP Renderer
- 湿润与积雪：WeatherType ShaderControl、目标材质和 Shader
- 天气声音：`UniStormSystem.cs`、当前 `AudioListener` 和 `UniStorm Sounds`

## 空间语义决策

| 类别 | 例子 | 跟随目标 | 关键约束 |
|---|---|---|---|
| 观察者局部连续环境 | 雨、雪、沙尘、花粉 | 当前相机或相机与飞机联合中心 | 覆盖、面积密度、风补偿 |
| 飞机局部物理效果 | 轮胎水雾、地面溅水 | 飞机、轮组或地面 | 碰撞、世界位置 |
| 世界固定事件 | 闪电击中点、火焰 | 生成后的世界坐标 | 不随相机瞬移 |
| 全局天空 | 云、太阳、月亮、星空 | 当前相机参数 | Renderer、far clip、更新时序 |
| 全局雾和材质 | 雾、湿润、积雪 | Renderer / Shader 全局参数 | 不使用粒子锚点 |
| 听者局部声音 | 雨声、风声 | 当前 AudioListener | 唯一 Listener、音量和延迟 |

对 Blowing Snow、树叶、花粉、Mist、Sparks 等保留必要方向性。禁止未经验证将它们全部强制改成垂直降水 Box。

## 必须计算的量

### 联合覆盖

需要同时覆盖相机和飞机时：

```text
中心 = (相机位置 + 飞机覆盖中心) / 2
所需半宽 = 两者距离 / 2 + 飞机安全余量
```

飞机安全余量优先来自 Bounds；使用固定值时说明依据。

### 面积密度

Box：

```text
水平面积 = X 长度 × Z 长度
目标发射率 = 目标单位面积密度 × 水平面积
```

圆形：

```text
水平面积 = π × 半径²
```

### 粒子预算

```text
预计存活粒子数 ≈ 发射率 × 生命周期
预算允许的最大发射率 = 粒子预算 / 生命周期
```

如果预算不足，明确选择缩小范围、降低密度、缩短寿命、降低粒子成本、减少层重叠或改用 Shader。禁止只依赖 `Mathf.Min` 截断后仍宣称密度一致。

### 风补偿

持续风的初始估算：

```text
发射器偏移 = -水平速度 × 生命周期 × 0.5
```

雪、花粉和飘动物的生命周期较长，必须单独计算。

## 实施规则

1. 优先修改项目控制器或新增项目策略组件，不先改 UniStorm 插件本体。
2. 用明确的可序列化策略或 Prefab 引用分类，逐步替代大小写敏感的名称包含判断。
3. 同时检查主粒子和附加粒子。
4. 修改运行时 ParticleSystem 前缓存原始值，并在禁用或销毁时恢复。
5. 明确 Collision 和 Sub Emitters 的取舍；主雨与地面溅水需要时拆成独立层。
6. 驾驶舱和客舱属于内部相机，检查粒子是否穿入机舱。
7. 保留项目要求：降水声音可立即出现，云和视觉过渡按既定设计运行。不要让密度控制无意覆盖渐变协程。
8. 修改 Scene、Prefab、材质或插件资源后检查 Missing Reference，并保留对应 `.meta`。
9. 不修改与当前天气无关的天空盒、体积云材质或场景脏改动。
10. 代码注释、测试名、文档和提交信息使用简体中文。

## 测试要求

先写失败测试，再改实现。至少根据目标补充以下一类：

- 95 米第三人称下联合覆盖；
- Box 扩大后的面积密度守恒；
- 长寿命雪的粒子预算；
- 主效果与附加效果的策略分类；
- Blowing Snow 不被普通雪策略误判；
- 当前相机 far clip 下的雾距离；
- 相机切换后的 `PlayerCamera`、容器和 AudioListener；
- UniStorm 粒子渐变不被 `LateUpdate` 无条件覆盖。

现有 18 个降水 EditMode 测试不等于完整视觉验证。

## 人工验证最小矩阵

至少验证：

- `Shift+8` 驾驶舱；
- `Shift+7` 客舱前、中、后部；
- `Shift+9` 第三人称 `18、48、60、95 米`；
- 第三人称绕机头、机尾、左右翼和上方；
- 切换后的第一帧；
- 等待至少两个粒子生命周期后的稳定状态；
- 主粒子与附加粒子；
- 机舱穿透；
- 近景与广域边界；
- 声音、雾和云是否同时正常。

无法执行 Unity 时，把这组步骤原样交给用户，并说明未完成视觉验证。

## 具体示例：Heavy Snow 附加 Mist 不均匀

1. 不要因为主雪已由 `B737UniStormPrecipitationAnchor` 处理，就判定整个天气已修复。
2. 读取 Heavy Snow WeatherType，确认主效果和 `Mist (Blowing)` 附加效果。
3. 读取 Mist Prefab 后再判断根因。当前 `Mist (Blowing)` 本身已经是 Box，长生命周期和水平速度造成的整体漂移比 Shape 类型更可疑。
4. 分别读取 `WeatherEffectsList` 和 `AdditionalWeatherEffectsList` 的运行时路径。
5. 主雪后续应使用低发射盒、寿命预算和联合覆盖；不要误写成当前运行时已经接入联合覆盖。
6. Mist 保留水平风向，但在世界空间计算相机附近覆盖和风漂移；不要照搬雪的垂直速度与高度，也不要把世界空间速度当成本地坐标。
7. 分别测试主雪和 Mist 的密度边界。
8. 验证驾驶舱、客舱和第三人称，并更新详细指南。

## 停止修改并重新检查的信号

- 只说“把粒子调大”但没有面积与预算计算；
- 把代码默认值当成 MainScene 真实值；
- 看到辅助函数存在就认为运行时已经使用；
- 只检查 `CurrentParticleSystem`；
- 只根据 `Rain` 或 `Snow` 字符串分类；
- 把雾、云或闪电套用粒子锚点；
- 忽略第三人称 95 米距离；
- 忽略机舱内部粒子穿透；
- 测试在实现之后才写；
- 没有运行 Unity 却声称视觉问题已解决；
- 为了本任务还原或覆盖用户无关改动。

出现任一项时，返回详细指南的空间语义、真实配置和验证章节重新排查。

## 交付格式

最终向用户说明：

1. 证据支持的根因；
2. 已修改文件；
3. 实际生效参数及计算；
4. 自动测试结果；
5. Unity Play 视觉验证结果，或明确未验证；
6. 哪些天气已覆盖；
7. 哪些附加效果或相机仍有风险；
8. 是否更新了 `Docs/UniStorm-Camera-Weather-Effects-Guide.md`。

不要把推断写成已验证事实。

---
title: "AeroSim-737 UniStorm 多视角天气特效排查与修复指南"
summary: "说明多相机视角下雨雪密度、覆盖、跟随和方向不一致的根因、当前实现、剩余限制及后续天气修复流程。"
last_updated: 2026-07-13
sources:
  - AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormPrecipitationAnchor.cs
  - AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormFogDistanceController.cs
  - AeroSimUnity/Assets/Scripts/Camera/CameraManager.cs
  - AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/UniStormSystem.cs
  - AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/WeatherType.cs
  - AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/LightningSystem.cs
  - AeroSimUnity/Assets/Scenes/MainScene.unity
  - AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab
  - AeroSimUnity/Assets/Scripts/Aircraft/B737/B737NightVisualController.cs
  - AeroSimUnity/Assets/Scripts/Aircraft/B737/B737ContrailController.cs
---

# AeroSim-737 UniStorm 多视角天气特效排查与修复指南

## 目录

- [文档目标](#文档目标)
- [先给结论](#先给结论)
- [必须先理解的场景差异](#必须先理解的场景差异)
- [原问题的具体表现](#原问题的具体表现)
- [根因分解](#根因分解)
- [本项目已经完成的修复](#本项目已经完成的修复)
- [当前真实配置与密度计算](#当前真实配置与密度计算)
- [当前方案仍存在的限制](#当前方案仍存在的限制)
- [哪些其他天气可能出现同类问题](#哪些其他天气可能出现同类问题)
- [不同类别天气应该怎样解决](#不同类别天气应该怎样解决)
- [供 AI Agent 执行的标准流程](#供-ai-agent-执行的标准流程)
- [建议补充的自动化测试](#建议补充的自动化测试)
- [人工验证矩阵](#人工验证矩阵)
- [常见误判](#常见误判)
- [关键文件索引](#关键文件索引)
- [完成标准](#完成标准)

## 文档目标

本文面向后续处理 AeroSim-737 天气问题的 Codex、Claude 或其他 AI Agent。它不只是解释“雨为什么不均匀”，还规定后续修复其他天气时应如何判断坐标空间、跟随目标、覆盖范围、粒子密度和性能预算。

适用症状包括：

- 驾驶舱有雨，第三人称没有雨；
- 第三人称绕到机头、机尾或机翼另一侧后雨量明显变化；
- 相机切换后旧位置仍有粒子，新位置短时间没有粒子；
- 拉远第三人称相机后，飞机周围只剩少量雨滴；
- 雪只停留在高空，落不到当前视角附近；
- 风把雨雪整体吹到相机一侧；
- 客舱、驾驶舱、第三人称看到的雾、沙尘、飘叶、闪电或天气声音不一致；
- 单纯提高发射率后仍有空洞，或性能突然下降；
- 某个天气修好后，另一个使用附加粒子的天气仍然异常。

本文描述的是当前仓库中的真实实现。修改代码或场景参数后，必须同步更新本文。

## 先给结论

原问题来自 UniStorm Standard Demo 的设计前提与 AeroSim-737 相机结构不匹配；粒子数量不足只是可能出现的表象。

UniStorm Standard Demo 只有一个相机，并且相机紧贴在 `Player` 下。UniStorm 把 `UniStorm Effects` 和 `UniStorm Sounds` 也挂到 `PlayerTransform` 下，因此默认粒子体积始终靠近相机。

AeroSim-737 有三个可切换主相机：

- `Shift+7`：客舱视角；
- `Shift+8`：驾驶舱视角；
- `Shift+9`：第三人称视角。

第三人称相机可在飞机外约 `18～95 米`范围缩放，默认距离约 `48 米`。只有第三人称相机带 `MainCamera` 标签，而 `CameraManager` 默认启用的是驾驶舱相机。UniStorm 初始化时找到的相机与实际渲染相机可能不同，原始粒子半径和半球方向也不足以覆盖自由绕机视角。

本项目已经通过 `B737UniStormPrecipitationAnchor` 完成以下主要修复：

1. 每帧获取真正启用的主相机；
2. 把 UniStorm 的 `PlayerCamera`、特效容器和声音容器同步到当前相机；
3. 把雨、雪、毛毛雨、冰雹等近景粒子改成世界轴对齐的对称 Box；
4. 使用 Local 模拟空间，避免粒子留在旧视角；
5. 补偿水平风漂移；
6. 为雪使用更低的发射高度；
7. 增加相机中心的广域补充层；
8. 根据粒子寿命和预算限制发射率。

但是当前实现仍有两个重要限制：

- 近景 Box 半宽实际固定为 `60 米`，不会随第三人称距离动态扩张；
- 广域 `600 × 600 米`层受到 `10000 粒子/秒`和 `15000 存活粒子`上限限制，单位面积密度远低于近景层，可能形成明显密度断层。

后续 Agent 不得把“已有修复”理解为“所有天气、所有距离、所有相机都已完全解决”。

## 必须先理解的场景差异

### UniStorm Standard Demo

Standard Demo 的核心层级关系是：

```text
Player（Player 标签）
└── Camera（MainCamera 标签，局部位置约 0, 1.04, 0）
```

UniStorm 在初始化时：

1. 按 `Player` 和 `MainCamera` 标签找到玩家和相机；
2. 在 `PlayerTransform` 下创建 `UniStorm Effects`；
3. 在 `PlayerTransform` 下创建 `UniStorm Sounds`；
4. 把所有主天气粒子和附加天气粒子放进 `UniStorm Effects`。

在这种结构里，相机与 Player 的距离接近零。即使天气 Prefab 的覆盖半径只有几十米，相机仍然处于粒子体积中心附近。

### AeroSim-737 MainScene

本项目的核心关系不同：

```text
CesiumGeoreference（Player 标签）
└── B737 Prefab
    ├── CabinCamera
    ├── CockpitCamera
    └── ThirdPersonCamera（MainCamera 标签）
```

`CameraManager` 负责启用其中一个主相机，并关闭其他主相机的 `Camera`、`AudioListener` 和控制脚本。

UniStorm 仍按标签初始化：

- `PlayerTransform` 首先指向 `CesiumGeoreference`；
- `PlayerCamera` 首先可能指向带 `MainCamera` 标签的第三人称相机；
- 实际默认启用相机却是驾驶舱相机。

这会带来三个问题：

1. 插件初始化相机不一定是当前渲染相机；
2. 天气容器默认跟随 Player，而不是跟随当前相机；
3. 第三人称相机可以远离飞机和原始粒子体积。

### 设计前提对比

| 项目 | Standard Demo | AeroSim-737 |
|---|---|---|
| 主相机数量 | 1 | 3 |
| 相机切换 | 无 | `CameraManager` 动态切换 |
| 相机与 Player 距离 | 约 1 米 | 第三人称可达 95 米 |
| `MainCamera` 标签 | 当前唯一相机 | 只有第三人称相机 |
| 天气容器默认父物体 | Player | CesiumGeoreference |
| 原生覆盖体积是否足够 | 通常足够 | 不一定 |
| 是否需要主动同步当前相机 | 否 | 是 |

## 原问题的具体表现

原问题通常表现为下列空间不一致，未必是整个雨系统完全消失：

### 视角中心不一致

粒子体积仍以旧相机、Player 或飞机中心为基准，新相机已经移动到其他位置。

### 水平方向不一致

Rain、Heavy Snow 等原始 Prefab 使用半球或带方向性的 Shape。相机绕到另一侧时，看到的是半球稀疏侧或未覆盖侧。

### 风向导致一侧稀疏

当前 Rain 的速度大致包含：

```text
Y = -100
Z = -20
生命周期约 1.3 秒
```

雨滴在生命周期内会沿 Z 方向漂移。若发射器仍固定在相机正上方，存活粒子的中心会偏到一侧。

### 第三人称距离超过近景层

当前第三人称可拉到 `95 米`，但当前近景 Box 半宽是 `60 米`。拉远超过 60 米后，飞机中心或机体远端可能离开高密度层。

### 两层密度断层

当前近景层很密，广域层很稀。两层重叠区域和纯广域区域之间会出现肉眼可见的密度变化。

## 根因分解

### 根因一：UniStorm 默认跟随 Player，不认识本项目的活动相机

`WeatherType.CreateWeatherEffect()` 和 `CreateAdditionalWeatherEffect()` 都把实例放到 `UniStorm Effects` 下。`UniStorm Effects` 又由 `UniStormSystem` 创建在 `PlayerTransform` 下。

这套逻辑只在“相机始终贴着 Player”时天然成立。

### 根因二：标签相机与实际活动相机可能不是同一个

只有 `ThirdPersonCamera` 带 `MainCamera` 标签。UniStorm 按标签初始化后，`PlayerCamera` 可能先指向第三人称相机，但 `CameraManager` 默认启用驾驶舱相机。

不能使用下面这些方式判断当前主相机：

- 只读 `Camera.main`；
- 只找 `MainCamera` 标签；
- 只使用 UniStorm 初始化时保存的 `PlayerCamera`；
- 选择任意启用的仪表相机或 RenderTexture 相机。

当前正确优先级是：

1. `CameraManager.ActiveCamera`；
2. 当前仍可用的 `UniStormSystem.PlayerCamera`；
3. 带已启用 `AudioListener` 的有效相机。

### 根因三：原始 Shape 不适合自由绕机相机

半球 Shape、定向喷射和不对称速度在单一第一人称 Demo 中可以成立，但不适合围绕大型飞机自由旋转的第三人称相机。

对雨雪这类“观察者周围环境降水”，水平方向应默认满足：

```text
X 正方向覆盖 = X 负方向覆盖
Z 正方向覆盖 = Z 负方向覆盖
```

世界轴对齐 Box 比随相机旋转的半球更稳定。

### 根因四：覆盖范围和密度不能分开调

扩大 Shape 会增加面积。发射率不随面积增加时，单位面积密度一定下降。

Box 的水平面积：

```text
面积 = X 长度 × Z 长度
```

保持单位面积密度时：

```text
新发射率 = 旧发射率 × 新面积 / 旧面积
```

因此不能只把半宽从 `60` 改成 `140`。Box 从 `120 × 120` 变为 `280 × 280` 后，面积约增加 `5.44 倍`；发射率不变时，近景密度会降到原来的约 `18.4%`。

### 根因五：粒子寿命决定预算

近似存活粒子数：

```text
存活粒子数 ≈ 发射率 × 生命周期
```

因此：

```text
预算允许的最大发射率 = 粒子预算 / 生命周期
```

雪的生命周期通常比雨长。相同发射率下，雪更容易达到 `maxParticles`，随后新粒子无法正常产生，画面会表现为密度上不去或局部断流。

### 根因六：慢速雪需要独立的垂直参数

雨可以从较高位置快速落到相机附近，雪不行。若雪的发射器放在 80 米高处，而生命周期和下落速度不足，雪会在到达相机高度前死亡。

### 根因七：其他天气的空间语义不同

雨、雾、闪电、树叶和云不能共用同一套锚点规则：

- 雨雪通常是观察者附近的连续环境粒子；
- 树叶、花粉需要保留一定风向；
- 雾通常是全局 Renderer Feature，不是局部粒子盒；
- 闪电是世界中的物理事件，生成后不应跟相机一起移动；
- 云、太阳、月亮和星空属于全局天空渲染；
- 雷声延迟应根据选定的听者位置计算。

如果不先分类，Agent 很容易把所有天气都强制改成相机中心 Box，从而修好一个问题又破坏另一个效果。

## 本项目已经完成的修复

### 1. `CameraManager` 公开 `ActiveCamera`

相机切换完成后，`CameraManager.SwitchToSlot()` 会更新 `ActiveCamera`。天气控制器不需要猜测当前相机。

切换顺序是：

1. 关闭旧主相机；
2. 启用新主相机；
3. 更新当前槽位；
4. 更新 `ActiveCamera`。

### 2. 降水锚点在 `LateUpdate` 同步

`B737UniStormPrecipitationAnchor` 使用 `DefaultExecutionOrder(100)`，并在 `LateUpdate()` 执行。这样可以在相机控制器和 `CameraManager.Update()` 完成后再同步天气。

每帧主要流程：

```text
取得 ActiveCamera
→ 更新 UniStorm.PlayerCamera
→ 找到 UniStorm Effects / UniStorm Sounds
→ 把容器移动到当前相机
→ 配置近景降水
→ 配置近景密度
→ 更新广域补充降水
```

### 3. 特效和声音容器跟随当前相机

`UniStorm Effects` 与 `UniStorm Sounds` 的世界位置被设置为当前活动相机位置，旋转归零。

效果：

- 主天气粒子跟随新相机；
- 附加天气粒子至少会随共同父容器移动；
- 天气循环声音保持靠近当前 `AudioListener`。

### 4. 近景降水使用对称 Box

脚本对名称包含以下关键字的主粒子进行降水处理：

- `Rain`；
- `Snow`；
- `Drizzle`；
- `Hail`。

处理内容：

- Shape 强制启用；
- Shape 类型改为 `ParticleSystemShapeType.Box`；
- Shape 位置和旋转归零；
- X/Z 使用相同半宽；
- 发射器世界旋转使用单位旋转。

### 5. 使用 Local 模拟空间

近景和广域降水都使用：

```csharp
ParticleSystemSimulationSpace.Local
```

锚点移动或切换相机时，已有粒子会跟随发射器一起移动，不会继续留在旧视角附近。

这是针对“相机切换”的明确取舍。它可能使粒子在硬切相机时整体瞬移，因此仍需要检查切换第一帧和稳定状态。

### 6. 水平风漂移补偿

当前补偿公式：

```text
补偿后发射位置 =
相机上方锚点 - 水平速度 × 平均生命周期 × 0.5
```

Rain 的 Z 速度约为 `-20`，生命周期约 `1.3 秒`，所以发射器会在反方向补偿约 `13 米`。雨滴生命周期中段的平均位置更接近相机中心。

### 7. 雪使用较低发射盒

当前雪的计算：

```text
预计下落距离 = 向下速度 × 生命周期
Box 高度 = clamp(预计下落距离 × 0.75, 20, 30)
发射器高度 = Box 高度 / 2 + 5
```

场景中的 `minimumSnowHeight = 25` 还会保证雪盒不会过薄。

### 8. 增加广域补充降水

脚本会克隆当前主降水 ParticleSystem，并创建 `UniStorm 远景补充降水`：

- 只保留根 ParticleSystem；
- 禁用克隆中的嵌套粒子；
- 禁用碰撞；
- 禁用 Sub Emitters；
- 使用相机中心的世界轴对齐 Box；
- 当前天气不是降水或主粒子变化时销毁并重建。

### 9. 增加粒子预算

近景和广域都会按生命周期限制发射率，避免长期超过存活粒子预算。

### 10. 禁用时恢复原始状态

脚本缓存并恢复：

- 模拟空间；
- Shape；
- 位置和旋转；
- 发射率；
- `maxParticles`；
- 碰撞；
- Sub Emitters；
- 特效和声音容器的初始变换。

这使组件禁用后不会永久污染运行时对象。

### 11. 已有 EditMode 测试

`B737UniStormPrecipitationAnchorTests` 当前包含 18 个测试，覆盖：

- 当前相机锚点；
- Box 尺寸；
- 风补偿；
- 雪盒高度；
- 面积换算；
- 粒子预算；
- Local 模拟空间；
- `CameraManager.ActiveCamera` 属性。

这些测试验证纯计算和接口存在性，但不是完整的多相机运行时测试。

## 当前真实配置与密度计算

### 必须读取场景覆盖值

C# 字段默认值不是最终运行值。`MainScene.unity` 当前覆盖为：

| 参数 | 当前值 | 作用 |
|---|---:|---|
| `minimumCoverageRadius` | 60 | 当前实际近景 Box 半宽 |
| `maximumCoverageRadius` | 140 | 当前只作为 Clamp 上限 |
| `minimumSnowHeight` | 25 | 雪盒最低高度 |
| `nearDensityMultiplier` | 3 | 近景发射率倍数 |
| `maximumNearParticles` | 12000 | 近景存活粒子预算 |
| `distantBoxHalfExtent` | 300 | 广域 Box 半宽 |
| `distantBoxHeight` | 100 | 非雪广域 Box 高度 |
| `distantBoxEmitterHeight` | 80 | 非雪发射中心高度 |
| `distantDensityMultiplier` | 0.35 | 广域面积密度系数 |
| `maximumDistantEmissionRate` | 10000 | 广域发射率硬上限 |
| `maximumDistantParticles` | 15000 | 广域存活粒子预算 |

当前近景半宽的运行时调用是：

```csharp
CalculateDenseNearRadius(minimumCoverageRadius, maximumCoverageRadius)
```

它没有读取相机与飞机的距离。因此当前实际半宽固定为 `60 米`，不是动态的 `60～140 米`。

以下旧接口当前没有接入 `LateUpdate()` 的主链路：

- `CalculateAnchorPosition()`；
- `CalculateCoverageRadius()`；
- `CalculateDistantCoverageRadius()`；
- `CalculateDistantEmissionRate()`。

后续 Agent 不得因为这些函数存在，就误判当前覆盖半径会自动增长。

### Rain 近景计算示例

当前 Rain 类天气基础发射率通常为 `3000`：

```text
请求发射率 = 3000 × 3 = 9000 粒子/秒
预算上限 = 12000 / 1.3 ≈ 9231 粒子/秒
最终近景发射率 ≈ 9000 粒子/秒
```

近景 Box 水平面积：

```text
120 × 120 = 14400 平方米
```

近景单位面积发射率约为：

```text
9000 / 14400 = 0.625
```

### Rain 广域计算示例

广域 Box 水平面积：

```text
600 × 600 = 360000 平方米
```

代码使用近景圆面积作为参考：

```text
面积比 = 360000 / (π × 60²) ≈ 31.83
请求值 = 9000 × 31.83 × 0.35 ≈ 100269
发射率硬上限 = 10000
生命周期预算上限 = 15000 / 1.3 ≈ 11538
最终广域发射率 = 10000 粒子/秒
```

广域单位面积发射率：

```text
10000 / 360000 ≈ 0.0278
```

近景单位面积密度约是纯广域层的：

```text
0.625 / 0.0278 ≈ 22.5 倍
```

由于两个层完全重叠，近景区域实际还会叠加广域粒子。近景重叠区域与纯广域区域之间可能接近 `23.5 倍`差异。

这就是“扩大了覆盖范围，但拉远后仍然局部很稀”的主要原因。

如果把近景半宽直接改为 `140`，并把 `nearDensityMultiplier` 从 `3` 改为 `10`，结果仍会被当前预算限制：

```text
请求发射率 = 3000 × 10 = 30000
预算上限 = 12000 / 1.3 ≈ 9231
新 Box 面积 = 280 × 280 = 78400
实际单位面积发射率 ≈ 9231 / 78400 = 0.118
```

`0.118` 只有当前 `0.625` 的约 `18.8%`。倍数看起来提高很多，实际发射率只增加约 `2.6%`，但覆盖面积增加约 `5.44 倍`。因此“半径 140、倍数 10”会把稀雨扩展到更大范围，不能解决密度连续性。

### 雪的预算示例

假设 Heavy Snow：

```text
基础发射率 = 1000
近景请求值 = 1000 × 3 = 3000
生命周期约 3 秒
近景预算上限 = 12000 / 3 = 4000
最终近景值 = 3000
```

Thunder Snow 的基础值为 `3000` 时：

```text
请求值 = 3000 × 3 = 9000
预算上限 = 4000
最终值 = 4000
```

继续提高 `nearDensityMultiplier` 不会使 Thunder Snow 超过约 `4000 粒子/秒`，除非同时提高预算或缩短寿命。

## 当前方案仍存在的限制

### 1. 近景层没有同时覆盖相机和飞机

当前近景层只以相机为中心，半宽固定 60 米。第三人称相机最远 95 米时，飞机中心已经可能离开高密度区。

更合理的动态方案是：

```text
中心 = 相机位置与飞机覆盖中心的中点
半宽 = 相机到飞机覆盖中心的距离 / 2 + 飞机余量
```

再把结果限制在明确的性能上限内。

### 2. 广域层和近景层密度不连续

当前广域层受到硬上限，不能维持与近景层相同的单位面积密度。

可选修复方向：

1. 使用一个动态高密度 Box，同时覆盖相机和飞机；
2. 缩小广域范围；
3. 把广域层改为不与近景层重叠的四个外围条带；
4. 使用密度渐变或 Shader 远景降水；
5. 明确降低全局目标密度，而不是让两层差异过大。

优先先关闭广域克隆层做 A/B 验证。如果单一动态 Box 已经满足视角需求，就不要保留复杂的双层结构。

### 3. 名称检测脆弱

当前使用大小写敏感的 `Contains("Rain")`、`Contains("Snow")` 等名称判断。

以下变化会导致误判：

- Prefab 改名；
- 中文化名称；
- 插件升级后名称变化；
- 新增不含关键字的降水；
- 非降水效果名称恰好包含 `Snow` 或 `Rain`。

后续应优先使用可序列化策略表、明确 Prefab 引用集合或天气效果类别。

### 4. `Blowing Snow` 会被部分当成普通降雪

`Blowing Snow` 虽然属于非降水天气，但名称包含 `Snow`。当前脚本会把它的主粒子 Shape 改成 Box，并禁用碰撞和 Sub Emitters；但因为天气本身不是 `PrecipitationWeatherType`，它又不会获得完整的近景密度和广域层。

这可能破坏它原本的水平吹雪方向。

### 5. 附加粒子没有专用策略

当前专用逻辑只遍历 `WeatherEffectsList`，没有对 `AdditionalWeatherEffectsList` 做同类配置。

附加粒子会随 `UniStorm Effects` 容器移动，但不会自动获得：

- 对称 Box；
- 独立风补偿；
- 独立密度预算；
- 广域补充；
- 雪的低发射高度。

### 6. 可能绕过 UniStorm 原生粒子渐入

`ApplyNearPrecipitationDensity()` 在每个 `LateUpdate` 直接写当前粒子发射率。UniStorm 原生 `ParticleFadeSequence()` 会等待云量并逐步增加粒子，但锚点脚本可能在同帧末尾覆盖该值。

修改密度逻辑后必须验证：

- 云是否仍按原速度增加；
- 雨滴是否按预期延迟或立即出现；
- 是否只有声音立即出现；
- 天气切换时是否存在发射率争用。

### 7. 地面溅水和碰撞被禁用

为了稳定覆盖和性能，当前近景主粒子禁用了 Collision 和 Sub Emitters。Rain 的地面 Splash 等效果可能因此不再工作。

如果后续恢复溅水，不应直接恢复所有碰撞。应把视觉降雨与地面溅水拆成两套系统：

- 相机附近的无碰撞主雨；
- 飞机或地面附近的低数量碰撞溅水层。

### 8. 机舱内部可能出现穿透粒子

相机中心 Box 会在驾驶舱和客舱周围生成雨雪，且碰撞已关闭。需要检查粒子是否直接穿进机舱。

可选处理：

- 为内外相机提供不同天气显示策略；
- 机舱视角仅在窗外渲染雨雪；
- 使用相机层、Stencil、深度或专用窗面效果；
- 将近景粒子中心放在飞机外部，而不是机舱相机正上方。

### 9. 测试没有覆盖完整运行时组合

现有测试没有验证：

- 第三人称 `18、48、60、95 米`距离；
- 飞机 Bounds 是否仍在高密度区；
- 两层单位面积密度比；
- 相机切换后的第一帧；
- 场景序列化参数与代码组合；
- 附加粒子；
- 客舱内部穿透；
- UniStorm 渐变协程与 `LateUpdate` 写值冲突。

## 哪些其他天气可能出现同类问题

### 风险矩阵

| 天气或系统 | 当前主要实现 | 同类风险 | 推荐处理方向 |
|---|---|---|---|
| Rain / Light Rain / Heavy Rain | 主 Rain 粒子 | 覆盖、密度断层、风漂移 | 动态覆盖相机与飞机；保持面积密度 |
| Drizzle | 主 Drizzle 粒子 | 低发射率下更容易出现空洞 | 使用独立低密度目标，不照搬暴雨参数 |
| Hail | 主 Hail 粒子 | 寿命、碰撞、粒径和机舱穿透 | 单独预算；谨慎保留碰撞 |
| Snow / Light Snow | 主 Snow 粒子 | 下落慢、寿命长、预算受限 | 低发射盒；按寿命控制预算 |
| Heavy Snow / Thunder Snow | 主雪 + 附加 Mist | 主雪修好但附加雾仍偏向一侧 | 同时检查主粒子与附加粒子 |
| Blowing Snow | 非降水定向粒子 | 被名称规则误判；方向性被破坏 | 建立独立“水平吹雪”策略 |
| Dust Storm | Mist (Blowing) | Shape、风向、覆盖不足 | 相机附近大体积定向 Box；保留风向 |
| Blowing Leaves / Pine Needles / Pollen | 局部飘动物 | 转身后消失、风把粒子吹走 | 小范围相机跟随 + 风补偿；保留方向性 |
| Lightning Bugs | 局部粒子 | 夜间某方向密集或相机离开体积 | 低速相机局部体积，不需要降水预算 |
| Fire Rain / Fire Storm | Rain Fire + Sparks | 名称与降水标记不一致；附加火花未处理 | 明确特效类别，避免依赖名称 |
| Foggy / Hazy / Overcast | UniStorm Fog / 全局参数 | 各相机 far clip 不同 | 按活动相机 far clip 限制雾距离 |
| Clouds / Cloud Shadows | 全局 Shader、RenderTexture | `PlayerCamera` 切换时序、相机 URP 设置差异 | 同步活动相机；统一 Renderer 和 Volume |
| Sun / Moon / Stars | 全局天空对象 | 初始化使用旧相机 far clip；切换后一帧旧数据 | 在相机切换事件中刷新依赖参数 |
| Lightning / Thunder | 世界事件 + AudioSource | 生成中心和声音距离基于错误 Player | 明确以飞机还是听者为中心 |
| Weather Sounds | 相机附近 AudioSource | 多 AudioListener、容器未跟随 | 保证唯一 Listener；跟随活动相机 |
| Wetness / Snow Shader | 全局 Shader 参数 | 材质不支持、不同相机 Renderer 差异 | 检查材质 Shader，不使用粒子锚点方案 |
| Night Cesium Tiles | `OnTileGameObjectCreated` + `MaterialPropertyBlock` 清理旧自发光 | 新瓦片如果等扫描后再处理，可能短时间保留旧 `_EmissionColor` 覆盖；全量每帧扫描会明显降帧；切换 `Cesium3DTileset.opaqueMaterial` 或替换瓦片 Renderer 的 `sharedMaterials` 会破坏 Cesium 贴图链路并导致白色地景；如果保留瓦片自带 `KHR_materials_unlit`，部分地景会像自发光贴图一样不受夜晚自然光影响 | 不切换 Cesium 材质；不再给 Cesium 写亮度乘法或白色自发光；主场景 Cesium Tileset 设置 `ignoreKhrMaterialsUnlit = true`，让地景尽量走受光材质；订阅瓦片创建事件后只把 `_EmissionColor` 覆盖清成黑色，并保持底色为材质原始值；现有瓦片按低频预算补注册；Cesium 明暗只由太阳、月光和环境光决定 |
| Night Airport Scenery | 机场静态 Renderer + `MaterialPropertyBlock` 清理旧自发光 | 机场 FBX 不属于 Cesium 瓦片，如果混在普通世界压暗里会比 Cesium 地景更黑；如果环境光每帧基于已压暗的当前值继续插值，会在黄昏过渡中先黑到不可见；机场材质不适合整体自发光，过高 emission 会把跑道和航站楼顶成纯白；如果再叠加独立亮度乘法，会和自然光照方向不一致；22 点后地景和机场一起变亮通常来自 UniStorm 月光曲线，而不是机场或 Cesium 自发光 | `airportDarkeningRoots` 必须直接绑定北京大兴机场 Prefab 根节点，当前主场景为 `beijing-daxing-international-airport` 的 stripped Transform；脚本会忽略没有 Renderer 的错误根并按机场关键词兜底发现真实根；环境光和天空盒颜色必须从缓存的白天基准值插值到夜间目标，不能递归使用当前帧值；机场不使用自发光，也不再使用独立 `airportSurfaceBrightness` 压暗，扫描只负责发现 Renderer 并清理旧 `_EmissionColor` 覆盖；机场明暗只由太阳、月光和环境光决定；当前主场景把 `MoonIntensityCurve` 的夜间补亮关键帧提前到 `21.5`，并把 `maximumMoonLightIntensity` 控制在 `0.05`，避免 22:20 才明显变亮且过亮 |
| B737 Contrails | 飞机局部 ParticleSystem | 白色航迹云不受地景压暗影响，夜晚仍按白天亮度显示；只改材质可能被粒子顶点色抵消 | 在 `B737ContrailController` 中按 UniStorm 时间压暗新发粒子和存量粒子颜色 |

### 当前主场景中尤其需要继续检查的天气

#### Heavy Snow 与 Thunder Snow

它们使用附加 `Mist (Blowing)`。主雪受锚点脚本控制，附加 Mist 只跟随共同父容器，仍保留原始 Shape、风向和密度。

当前 `Mist (Blowing)` 本身已经使用约 `50 × 50 × 20` 的 Box，问题不一定来自 Shape 类型。它的生命周期约 `6.5 秒`，水平速度大致为 `X = 1～2`、`Z = -0.25～-2.5`，并启用了世界空间速度；Z 方向最大漂移约 `16.25 米`，已经超过 Box 的 Z 半深 `10 米`。稳定后粒子群会自然偏向下风侧。

正确目标不是把 Mist 改成完全对称的垂直雪，而是保留风向并在世界空间给发射中心增加独立水平风补偿。不能把世界空间速度直接当成本地坐标，否则相机旋转后会产生新的方向偏移。当前 Heavy Snow 的附加发射率约 `25 粒子/秒`，预计存活粒子约 `162.5`，低于 `1000` 粒子上限，因此首轮应先修位置偏移，不要先提高发射率。

#### Fire Storm 与 Fire Rain

`Rain Fire` 名称包含 `Rain`，但不同天气的 `PrecipitationWeatherType` 配置不同。当前“名称判断 + 天气标记判断”的组合会使两个火雨天气进入不同处理分支。

#### Dust Storm

Dust Storm 使用 `Mist (Blowing)` 作为主效果。它需要大范围、带风向的观察者局部体积，不能直接套用雨的垂直下落 Box。

#### Blowing Snow

需要保留水平吹雪，而当前名称规则会部分按普通雪处理。

#### Foggy / Hazy

本项目三个主相机 far clip 差异很大：

| 相机 | far clip |
|---|---:|
| CabinCamera | 约 1000 米 |
| ThirdPersonCamera | 约 30000 米 |
| CockpitCamera | 约 100000 米 |

当前雾起始距离根据高度在 `8000～60000 米`之间变化。客舱相机的 far clip 小于最低雾起始距离，因此客舱可能几乎看不到远距离雾；第三人称也无法看到 60000 米处的雾起始。

#### Lightning / Thunder

`LightningSystem` 使用 `PlayerTransform` 生成闪电位置，并用它计算雷声延迟。当前 `Player` 标签指向 `CesiumGeoreference`，后续应明确实际目标：

- 如果闪电应围绕飞机生成，使用飞机物理根或飞机 Bounds 中心；
- 如果雷声应以听者感知为准，距离使用当前 `AudioListener`；
- 闪电一旦生成，应保持世界位置，不应跟随相机移动。

## 不同类别天气应该怎样解决

### 第一步：先判断空间语义

| 类别 | 示例 | 应跟随什么 | 推荐模拟空间 |
|---|---|---|---|
| 观察者局部连续环境 | 雨、雪、沙尘、花粉 | 当前活动相机，或相机与飞机联合中心 | Local 或 Custom |
| 飞机局部物理效果 | 机体附近溅水、轮胎水雾 | 飞机或轮组 | Local / Custom |
| 世界固定事件 | 闪电击中点、落地火焰 | 世界坐标 | World |
| 全局天空效果 | 云、太阳、月亮、星空 | 通过当前相机参数渲染 | 非 ParticleSystem |
| 全局雾和材质 | 雾、湿润、积雪 Shader | 当前 Renderer / 全局 Shader | 非 ParticleSystem |
| 听者局部声音 | 雨声、风声 | 当前 AudioListener | AudioSource 跟随或 2D |

### 第二步：选择覆盖中心

#### 只需要围绕观察者

```text
中心 = 当前活动相机
```

适合花粉、萤火虫、轻微沙尘等。

#### 同时需要覆盖相机和整架飞机

```text
中心 = (相机位置 + 飞机覆盖中心) / 2
半宽 = 距离 / 2 + 飞机余量
```

适合第三人称雨雪。

飞机余量应来自飞机 Bounds 或经过验证的固定安全值，不能只凭感觉填写。

#### 世界事件

生成时选择飞机附近或世界目标，生成后保持世界坐标。不要在每帧把闪电、火焰或击中点拉回相机。

### 第三步：保持单位面积密度

对 Box：

```text
目标发射率 = 目标单位面积密度 × X 长度 × Z 长度
```

对圆形覆盖：

```text
目标发射率 = 目标单位面积密度 × π × 半径²
```

如果粒子预算不足，必须显式选择一种取舍：

1. 缩小覆盖；
2. 降低目标单位面积密度；
3. 缩短生命周期；
4. 降低粒子复杂度；
5. 改用 Shader 或屏幕空间远景效果；
6. 分层并减少重叠。

不能只让 `Mathf.Min` 静默截断，然后仍声称密度保持一致。

### 第四步：根据速度补偿

对于持续风：

```text
发射器补偿 = -水平速度 × 生命周期 × 0.5
```

对于明显加速度、噪声风或飞机高速运动，单一平均速度可能不足，应考虑：

- Custom Simulation Space；
- 按实际粒子速度曲线采样；
- 以飞机速度和风速的相对速度计算；
- 降低生命周期，减少漂移距离。

### 第五步：处理内外视角差异

驾驶舱和客舱相机位于机体内部，第三人称位于外部。后续 Agent 必须明确：

- 内部视角是否允许粒子进入机舱；
- 是否只需要窗面雨滴和窗外雨；
- 外部视角是否需要整机被雨雪包围；
- 切换视角时是否要复用粒子，还是快速淡入另一套表现。

不要默认三种相机必须使用完全相同的粒子表现。

## 供 AI Agent 执行的标准流程

### 0. 保护用户现有改动

先执行：

```powershell
git status --short
```

记录所有现有修改。不要还原与本任务无关的场景、材质、天空盒或体积云脏改动。

### 1. 完整读取本指南和项目 Skill

Codex 使用：

```text
.agents/skills/aerosim-debug-camera-weather-effects/SKILL.md
```

Claude Code 使用：

```text
.claude/skills/aerosim-debug-camera-weather-effects/SKILL.md
```

两者都必须继续完整读取本文。

### 2. 明确复现条件

至少记录：

- 天气资产名称；
- 主粒子 Prefab；
- 附加粒子 Prefab；
- 当前相机；
- 第三人称距离；
- 相机朝向；
- 问题发生在切换第一帧还是稳定状态；
- 是否只在机舱内部发生；
- 是否伴随声音、雾或云异常。

### 3. 判断效果类别

回答以下问题：

1. 这是主 ParticleSystem、附加 ParticleSystem、全局 Shader、Renderer Feature、天空对象、世界事件还是声音？
2. 它应该跟随相机、飞机、AudioListener，还是保持世界坐标？
3. 它是否需要水平方向对称？
4. 它是否必须保留风向？
5. 它是否允许碰撞和 Sub Emitters？
6. 它是否需要覆盖整架飞机？

没有完成分类前，不修改 Shape 或 Simulation Space。

### 4. 读取真实运行配置

优先级：

1. `MainScene.unity` 的序列化覆盖值；
2. WeatherType `.asset`；
3. ParticleSystem Prefab；
4. C# 字段默认值。

不要把代码默认值当成场景真实值。

### 5. 检查当前活动相机链路

确认：

- `CameraManager.ActiveCamera` 指向当前主相机；
- 当前相机启用；
- 只有当前相机的 `AudioListener` 启用；
- 仪表相机和 RenderTexture 相机没有被误选；
- `UniStormSystem.PlayerCamera` 已同步；
- 相机切换后下一次 `LateUpdate` 能取得新相机。

### 6. 计算覆盖和预算

必须写出计算，不得只说“调大一点”：

```text
所需半宽
水平面积
目标单位面积密度
目标发射率
粒子生命周期
预计存活粒子数
maxParticles
发射率硬上限
```

如果预算导致目标不可达，应先向用户说明取舍。

### 7. 先写失败测试

针对新问题补最小测试。优先测试纯计算和策略分类，例如：

- 95 米第三人称下相机与飞机都在覆盖范围；
- 扩大 Box 后单位面积密度保持不变；
- 长寿命雪不会超过预算；
- Blowing Snow 不进入普通垂直降雪策略；
- 附加 Mist 使用自己的策略；
- 雾起始距离不大于活动相机 far clip 的合理比例。

测试必须先因缺少目标行为而失败，再修改实现。

### 8. 优先修改项目控制器

优先修改：

- `B737UniStormPrecipitationAnchor.cs`；
- 新增明确的项目天气策略组件；
- 项目自己的测试；
- `MainScene.unity` 中项目组件参数。

只有确认是 Prefab 本身错误时，才修改 UniStorm 插件 Prefab。插件目录引用密集，升级时也更容易冲突。

### 9. 同时检查主粒子和附加粒子

检查：

- `WeatherEffectsList`；
- `AdditionalWeatherEffectsList`；
- `CurrentParticleSystem`；
- `AdditionalCurrentParticleSystem`；
- WeatherType 的 `UseWeatherEffect`；
- WeatherType 的 `UseAdditionalWeatherEffect`；
- WeatherType 的 `PrecipitationWeatherType`。

不要只根据名称判断当前是否应处理。

### 10. 避免破坏天气渐变

检查 UniStorm 的：

- `ParticleFadeSequence()`；
- `AdditionalParticleFadeSequence()`；
- 云覆盖等待；
- `TransitionSpeed`；
- 当前控制器是否每帧覆盖发射率。

如果项目要求“声音立即出现、云和粒子仍渐变”，密度控制器不能无条件把粒子立即写到目标值。

### 11. 静态验证

至少验证：

- 方法和字段引用真实存在；
- Skill 和文档中的路径存在；
- 名称判断覆盖目标 Prefab；
- 没有修改无关文件；
- 场景参数与公式一致；
- 新测试覆盖新行为。

### 12. Unity 验证

如果当前 Agent 可以控制 Unity：

1. 等待编译结束；
2. 运行相关 EditMode 测试；
3. 检查 Console；
4. 进入 Play；
5. 按人工验证矩阵检查。

如果不能控制 Unity：

- 完成静态验证；
- 明确说明没有完成 Play 视觉验证；
- 把具体天气、按键、相机距离和观察点交给用户；
- 不得声称视觉问题已经完全解决。

### 13. 更新本文

修复完成后更新：

- 当前真实参数；
- 已解决范围；
- 剩余限制；
- 新增天气策略；
- 测试覆盖；
- 人工验证结果。

## 建议补充的自动化测试

### 动态联合覆盖测试

输入：

```text
相机距离 = 95
飞机安全余量 = 35
```

断言相机和飞机覆盖中心都位于高密度 Box 内。

### 面积密度守恒测试

把半宽从 60 改为 140 时，断言发射率按面积比例增加，或明确返回“预算不足”状态。

### 密度断层测试

计算近景和广域单位面积密度，断言两者比值不超过项目允许阈值。

### 场景参数集成测试

读取或镜像 `MainScene` 的真实参数，验证：

- 近景实际半宽；
- 广域实际尺寸；
- Rain 最终发射率；
- Heavy Snow 最终发射率。

### 相机切换 PlayMode 测试

依次切换：

```text
驾驶舱 → 第三人称 → 客舱 → 第三人称
```

断言：

- `PlayerCamera` 正确；
- `UniStorm Effects` 位于当前目标位置；
- `UniStorm Sounds` 位于当前目标位置；
- 旧相机不再有启用的 `AudioListener`。

### 天气分类测试

至少覆盖：

- Rain；
- Heavy Snow；
- Drizzle；
- Hail；
- Blowing Snow；
- Mist (Blowing)；
- Sparks；
- Rain Fire。

### 雾距离测试

结合活动相机 far clip 验证雾起始距离，不能只测试脚本默认的高度映射。

## 人工验证矩阵

### 视角

#### 驾驶舱

- `Shift+8`；
- 看前窗、左右窗；
- 检查雨雪是否穿入机舱；
- 检查声音与 Listener。

#### 客舱

- `Shift+7`；
- 移动到客舱前端、机翼附近、后端；
- 检查窗外天气是否一致；
- 检查 far clip 对雾的影响。

#### 第三人称

- `Shift+9`；
- 距离 `18 米`；
- 距离 `48 米`；
- 距离约 `60 米`；
- 距离 `95 米`；
- 环绕机头、机尾、左右翼、上方和下方。

### 天气顺序

1. Rain；
2. Drizzle；
3. Heavy Rain；
4. Thunderstorm；
5. Snow；
6. Heavy Snow；
7. Thunder Snow；
8. Hail；
9. Blowing Snow；
10. Dust Storm；
11. Fire Storm；
12. Foggy / Hazy。

### 时间点

每次检查：

- 切换天气后的第一帧；
- 切换相机后的第一帧；
- 等待至少两个粒子生命周期后的稳定状态；
- 云层完全过渡后；
- 相机快速拉远和绕机后。

### 观察区域

- 相机周围；
- 飞机中心；
- 机头；
- 机尾；
- 两侧翼尖；
- 近景层边界；
- 纯广域层；
- 机舱内部；
- 地面附近。

### 记录格式

```text
天气：
视角：
相机距离：
观察方向：
切换后时间：
相机周围密度：
飞机周围密度：
是否有边界断层：
是否穿入机舱：
声音是否正确：
雾和云是否一致：
Console 错误：
```

## 常见误判

| 误判 | 事实 |
|---|---|
| “有 `CalculateCoverageRadius`，所以运行时会动态扩张” | 当前 `LateUpdate` 没有调用它 |
| “把 60 改成 140 就会更密” | 面积增加约 5.44 倍，发射率不变会更稀 |
| “提高 multiplier 一定有效” | 可能已经被 `maxParticles / lifetime` 限制 |
| “三个相机的层掩码不同导致雨消失” | 当前三个主相机有效剔除层基本一致，不是首要证据 |
| “far clip 导致雨消失” | 三个主相机 far clip 都大于 600 米降水盒；但 far clip 会影响雾 |
| “只要更新 `PlayerCamera` 就够了” | 粒子容器、Shape、模拟空间和密度仍需处理 |
| “所有局部天气都应使用相机中心 Box” | 树叶、吹雪和闪电需要不同空间语义 |
| “附加 Mist 会自动获得主雪的修复” | 它只跟随父容器，没有专用 Shape 和预算 |
| “EditMode 测试通过就是视觉通过” | 当前测试不覆盖真实相机切换和画面密度 |
| “Local 模拟空间永远更真实” | 它解决切换残留，但也会让已有粒子随锚点瞬移 |
| “碰撞关闭没有副作用” | 地面溅水和部分 Sub Emitters 会失效 |
| “声音和粒子应该使用同一延迟” | 项目要求可分别控制；当前降水声音立即出现 |

## 关键文件索引

| 文件 | 作用 | 后续 Agent 重点 |
|---|---|---|
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormPrecipitationAnchor.cs` | 多视角降水锚点、Shape、密度、预算 | 主入口 |
| `AeroSimUnity/Assets/Scripts/Editor/B737/B737UniStormPrecipitationAnchorTests.cs` | 当前 18 个降水计算测试 | 增加联合覆盖和密度连续测试 |
| `AeroSimUnity/Assets/Scripts/Camera/CameraManager.cs` | 主相机切换与 `ActiveCamera` | 确认活动相机 |
| `AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab` | 三个主相机及第三人称距离 | 核对实际距离和 far clip |
| `AeroSimUnity/Assets/Scenes/MainScene.unity` | 所有实际序列化覆盖值 | 不能只看代码默认值 |
| `AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/UniStormSystem.cs` | 插件初始化、天气切换、粒子渐变、声音 | 检查写值冲突 |
| `AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/WeatherType.cs` | 创建主粒子、附加粒子和声音 | 理解父容器和资产字段 |
| `AeroSimUnity/Assets/UniStorm Weather System/Scripts/System/LightningSystem.cs` | 闪电位置与雷声延迟 | 明确飞机/相机语义 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormFogDistanceController.cs` | 高度到雾起始距离 | 加入活动相机 far clip |
| `AeroSimUnity/Assets/UniStorm Weather System/Scenes/Standard Demo.unity` | 插件原始单相机设计参考 | 只作架构对比 |
| `AeroSimUnity/Assets/UniStorm Weather System/Weather Types/` | 每种天气的主/附加效果和发射率 | 逐天气分类 |
| `AeroSimUnity/Assets/UniStorm Weather System/Prefabs/Weather Particle Effects/` | Shape、寿命、速度、碰撞 | 读取真实粒子参数 |

## 完成标准

后续某种天气只有同时满足以下条件，才能声称已修复：

- 当前活动相机识别正确；
- 驾驶舱、客舱、第三人称均按预期显示；
- 第三人称 `18～95 米`范围没有不可接受的覆盖空洞；
- 围绕机头、机尾和两侧机翼时密度符合设计；
- 覆盖范围变化时单位面积密度有明确计算；
- 发射率没有被粒子预算静默截断到完全不同的目标；
- 主粒子和附加粒子都已检查；
- 风向、寿命、发射高度符合该天气类型；
- 内部视角没有不可接受的粒子穿透；
- 雾、云、天空等非粒子效果使用了正确方案；
- 天气渐变与立即播放声音的既有要求没有被破坏；
- EditMode 测试通过；
- Unity Console 没有新增错误；
- 完成人工验证矩阵；
- 没有覆盖用户无关改动；
- 本文已同步更新。

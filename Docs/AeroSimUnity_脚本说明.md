# AeroSimUnity 脚本说明

## 1. 文档目的

这份文档用于说明 `AeroSim-737\AeroSimUnity` 工程里当前脚本的职责分工、使用场景和相互关系，方便后续维护、迁移和交接。

本次说明范围以当前工程中的 `.cs` 脚本为准，共 `22` 个文件，包含：

- `Assets/Scripts/Aircraft/B737/` 下的运行时飞行脚本
- `Assets/Scripts/Camera/` 下的相机脚本
- `Assets/Scripts/World/` 下的世界坐标与 Floating Origin 脚本
- `Assets/Scripts/Editor/B737/` 下的 B737 编辑器工具脚本
- `Assets/Plugins/ThirdParty/AdjustPivot/Editor/` 下的第三方编辑器脚本

## 2. 目录结构总览

当前与脚本直接相关的目录结构如下：

```text
AeroSimUnity/
└── Assets/
    ├── Scripts/
    │   ├── Aircraft/
    │   │   └── B737/
    │   │       ├── JsbsimBridge.cs
    │   │       ├── FlightInput.cs
    │   │       ├── FlightHud.cs
    │   │       ├── B737YokeController.cs
    │   │       ├── B737MechanicalController.cs
    │   │       ├── B737FlapController.cs
    │   │       └── B737EngineSpinner.cs
    │   ├── Camera/
    │   │   ├── CameraManager.cs
    │   │   ├── CockpitCameraController.cs
    │   │   └── SingleAudioListenerEnforcer.cs
    │   ├── World/
    │   │   ├── FloatingOriginManager.cs
    │   │   ├── FloatingOriginObject.cs
    │   │   ├── FloatingOriginRigidbody.cs
    │   │   ├── FloatingOriginParticleSystem.cs
    │   │   ├── FloatingOriginTrailRenderer.cs
    │   │   └── FloatingOriginCinemachine.cs
    │   └── Editor/
    │       └── B737/
    │           ├── AircraftPrefabFbxExportUtility.cs
    │           ├── AircraftPrefabIsolationUtility.cs
    │           ├── B737MaterialAutoFixer.cs
    │           ├── B737SceneMaterialReplacer.cs
    │           └── B737LiveryApplier.cs
    └── Plugins/
        └── ThirdParty/
            └── AdjustPivot/
                └── Editor/
                    └── AdjustPivot.cs
```

## 3. 按目录的简单说明

### 3.1 `Assets/Scripts/Aircraft/B737`

这部分是 B737 飞机本体的运行时脚本，主要处理四类事情：

- 把 JSBSim 的状态同步到 Unity 场景
- 把玩家输入发送给 JSBSim
- 驱动飞行器可视化部件动画
- 在屏幕上显示飞行数据

每个脚本的简单用途如下：

| 脚本 | 简单说明 |
| --- | --- |
| `JsbsimBridge.cs` | Unity 和 JSBSim 之间的核心桥接器，负责收状态、发控制、更新飞机位置姿态。 |
| `FlightInput.cs` | 键盘输入采集器，把 W/A/S/D 等按键转换成升降舵、副翼、方向舵、油门、襟翼和刹车指令。 |
| `FlightHud.cs` | 运行时 HUD，显示速度、高度、姿态、控制量和操作提示。 |
| `B737YokeController.cs` | 驱动驾驶舱操纵盘和操纵柱的 pitch / roll 动画。 |
| `B737MechanicalController.cs` | 通用机械联动控制器，可同时驱动副翼、升降舵、方向舵、起落架和部分驾驶舱联动部件。 |
| `B737FlapController.cs` | 只负责襟翼展开/收回的局部动画控制。 |
| `B737EngineSpinner.cs` | 根据发动机转速驱动发动机风扇叶片旋转。 |

### 3.2 `Assets/Scripts/Camera`

这部分是相机系统脚本，负责多个机位之间切换，以及驾驶舱、客舱、第三人称的相机操作逻辑。

| 脚本 | 简单说明 |
| --- | --- |
| `CameraManager.cs` | 注册多个相机槽位，并用 `Shift + 数字键` 切换机位。 |
| `CockpitCameraController.cs` | 单个相机的具体控制脚本，支持驾驶舱、客舱、第三人称三种模式。 |
| `SingleAudioListenerEnforcer.cs` | 保证场景里同一时刻只有一个 `AudioListener` 被启用。 |

### 3.3 `Assets/Scripts/World`

这部分主要是为大范围飞行时的坐标精度问题服务。飞机离原点过远时，脚本会整体平移世界，以避免模型抖动、粒子错位和相机阻尼异常。

| 脚本 | 简单说明 |
| --- | --- |
| `FloatingOriginManager.cs` | Floating Origin 核心管理器，判断何时需要触发世界平移。 |
| `FloatingOriginObject.cs` | 让普通 `Transform` 跟随原点平移。 |
| `FloatingOriginRigidbody.cs` | 让 `Rigidbody` 物体跟随原点平移。 |
| `FloatingOriginParticleSystem.cs` | 修正世界空间粒子系统在原点平移后的粒子位置。 |
| `FloatingOriginTrailRenderer.cs` | 修正 `TrailRenderer` 的历史轨迹点。 |
| `FloatingOriginCinemachine.cs` | 通知 Cinemachine 相机目标发生了“瞬移”，避免大幅插值抖动。 |

### 3.4 `Assets/Scripts/Editor/B737`

这部分都是 Unity 编辑器工具，不参与运行时打包逻辑，主要服务于机模整理、材质修复、涂装应用和资产快照输出。

| 脚本 | 简单说明 |
| --- | --- |
| `AircraftPrefabFbxExportUtility.cs` | 把当前 Prefab 层级导出成新的 FBX。 |
| `AircraftPrefabIsolationUtility.cs` | 把当前飞机 Prefab 保存成独立快照版本，连同材质、贴图和分享包一起导出。 |
| `B737MaterialAutoFixer.cs` | 按 manifest 或目录自动修复材质贴图和着色器。 |
| `B737SceneMaterialReplacer.cs` | 把一架正常飞机的材质引用复制到场景里的旧飞机/紫模飞机上。 |
| `B737LiveryApplier.cs` | 读取涂装文件夹，把贴图按部位应用到指定飞机或共享材质。 |

### 3.5 `Assets/Plugins/ThirdParty/AdjustPivot/Editor`

这是第三方编辑器工具，不是本项目自研业务逻辑，主要用于修改模型 Pivot 和保存修改后的网格。

| 脚本 | 简单说明 |
| --- | --- |
| `AdjustPivot.cs` | 在 Unity 编辑器里调整模型 Pivot，并可把修改后的网格保存为 `.asset` 或 `.obj`。 |

## 4. 脚本之间的关系

从运行链路上看，当前项目大致是下面这条关系：

1. `JsbsimBridge.cs` 从 JSBSim 接收飞机状态，并把位置/姿态更新到飞机对象。
2. `FlightInput.cs` 把玩家键盘输入发回 JSBSim 控制通道。
3. `FlightHud.cs` 读取 `JsbsimBridge` 和 `FlightInput` 的数据，把当前状态显示到屏幕上。
4. `B737YokeController.cs`、`B737EngineSpinner.cs` 等视觉脚本再根据桥接数据驱动驾驶舱和发动机动画。
5. `CameraManager.cs` 和 `CockpitCameraController.cs` 管理观察视角。
6. `FloatingOriginManager.cs` 及其配套脚本负责飞远后的世界坐标平移，保证长距离飞行时画面稳定。

编辑器链路则是：

1. `AircraftPrefabFbxExportUtility.cs` / `AircraftPrefabIsolationUtility.cs` 负责导出机模结构和版本快照。
2. `B737MaterialAutoFixer.cs` / `B737SceneMaterialReplacer.cs` 负责材质修复和场景替换。
3. `B737LiveryApplier.cs` 负责涂装贴图导入和应用。
4. `AdjustPivot.cs` 负责模型轴心和网格保存。

## 5. 每个脚本的详细说明

### 5.1 `Assets/Scripts/Aircraft/B737/JsbsimBridge.cs`

**脚本定位**

- 项目的飞行桥接核心
- 连接外部 JSBSim 进程与 Unity 飞机对象
- 同时承担“接收飞行状态”和“发送控制命令”两件事

**主要功能**

- 监听 UDP 端口 `5501`，接收 JSBSim 输出的状态数据
- 监听并解析 `<LABELS>` + CSV 数值数据，把它们保存成键值状态表
- 把经纬度、高度、航向、俯仰、滚转转换成 Unity 的局部位置和姿态
- 通过 TCP 端口 `5502` 向 JSBSim 发送 `set property value` 形式的控制命令
- 对外提供 `TryGetValue`、`GetValue`、`Snapshot`、`AvailableKeys` 等读取接口
- 在开启 Floating Origin 时自动挂接 `FloatingOriginManager`
- 响应原点平移事件，修正飞机和兼容对象的位置

**主要用途**

- 这是飞行仿真运行时最关键的基础设施
- 其他飞行相关脚本几乎都直接或间接依赖它

**依赖与配合**

- 被 `FlightInput` 调用，用来发送控制量
- 被 `FlightHud`、`B737YokeController`、`B737EngineSpinner` 等读取飞行状态
- 与 `FloatingOriginManager` 联动，保证长距离飞行时画面稳定

**使用注意**

- `aircraft` 为空时会默认驱动脚本挂载对象本身
- 如果场景里有多个 `JsbsimBridge`，脚本只保留第一个实例作为全局 `Instance`
- 在 Floating Origin 模式下，位置会直接硬切，避免环境和飞机不同步

### 5.2 `Assets/Scripts/Aircraft/B737/FlightInput.cs`

**脚本定位**

- 玩家键盘输入到 JSBSim 控制量之间的转换器

**主要功能**

- 读取按键：
  - `W/S` 控制升降舵
  - `A/D` 控制副翼
  - `Q/E` 控制方向舵
  - `LeftShift` 增加正推油门，`LeftControl` 将油门收向怠速
  - `LeftControl + LeftShift` 在接地后增加反推
  - `F` 循环切换襟翼档位
  - `B` 切换刹车
- 升降舵在 `W/S` 松开后保持当前舵位，副翼和方向舵在松手后自动回中
- 控制量按固定发送频率发给 `JsbsimBridge`
- 对外暴露 `Elevator`、`Aileron`、`Rudder`、`Throttle`、`Flaps`、`Brakes` 属性供 HUD 读取

**主要用途**

- 提供当前阶段的键盘操控方案
- 作为没有完整硬件输入系统前的临时/基础飞行输入层

**依赖与配合**

- 强依赖 `JsbsimBridge`
- 被 `FlightHud` 读取当前控制量
- 在 `B737YokeController` 没有可用 JSBSim 状态时，也可作为回退输入源

### 5.3 `Assets/Scripts/Aircraft/B737/FlightHud.cs`

**脚本定位**

- 运行时调试与演示 HUD

**主要功能**

- 运行时动态创建 Overlay Canvas、文字控件和描边
- 显示速度、高度、离地高度、升降率、航向、俯仰、滚转、转速
- 显示当前油门、反推、舵面、襟翼和刹车状态；反推油门显示为负数
- 显示操作说明和起飞提示
- 按 `Tab` 显示/隐藏 HUD
- 自动跟随当前活动相机的 `targetDisplay`，避免 HUD 出现在错误显示器

**主要用途**

- 方便开发时观察 JSBSim 数据是否正常流入
- 方便录屏、演示和测试阶段确认输入是否生效

**依赖与配合**

- 读取 `JsbsimBridge`
- 读取 `FlightInput`
- 不依赖场景中预先存在的 UI 结构

### 5.4 `Assets/Scripts/Aircraft/B737/B737YokeController.cs`

**脚本定位**

- 驾驶舱操纵盘/操纵柱的专用可视化控制器

**主要功能**

- 驱动整套 yoke 的前后俯仰
- 驱动左右 yoke 把手同步滚转
- 支持优先读取 JSBSim 状态
- 当 JSBSim 没有实时状态时，回退读取 `FlightInput`
- 支持运行时自动创建虚拟 Pivot，解决模型原始轴心不适合旋转的问题
- 提供 `Reset To Neutral` 和可视化 Gizmo 辅助调试

**主要用途**

- 让驾驶舱操作件的动画与真实控制量保持一致
- 专门处理 yoke 这种结构复杂、需要额外 pivot 的模型

**依赖与配合**

- 优先依赖 `JsbsimBridge`
- 次级依赖 `FlightInput`
- 与 `B737MechanicalController` 相比，它更专注于 yoke 本身的表现

### 5.5 `Assets/Scripts/Aircraft/B737/B737MechanicalController.cs`

**脚本定位**

- 通用的 B737 机械部件联动控制器

**主要功能**

- 用统一结构配置副翼、升降舵、方向舵、起落架和驾驶舱 yoke 等部件
- 支持键盘输入模式，也支持外部代码通过 `SetExternalInputs` 直接写入输入值
- 支持记录中立姿态 `CaptureNeutralPose`
- 根据输入值计算各部件绕指定局部轴的旋转
- 控制起落架收放过渡，并通过 `AnimationCurve` 调整过渡手感
- 保存部件相对路径，在引用丢失时尝试自动重新绑定

**主要用途**

- 作为“局部机械动作通用控制层”
- 在没有完整动画系统时，用脚本直接驱动机体控制面和起落架

**依赖与配合**

- 本身不强依赖 `JsbsimBridge`
- 更适合在本地可视化测试、模型调试或后续由桥接代码统一喂数据

**补充说明**

- 这个脚本偏“通用控制器”，覆盖面比 `B737YokeController` 更大
- 但它对 yoke 的处理是通用结构型的，不如 `B737YokeController` 那么专门化

### 5.6 `Assets/Scripts/Aircraft/B737/B737FlapController.cs`

**脚本定位**

- 襟翼专用控制器

**主要功能**

- 维护多个 `FlapPart`
- 记录每个襟翼部件的中立位置和旋转
- 用 `F` / `V` 控制襟翼展开和收回
- 支持通过 `SetFlapInput` 或 `SetFlapExtended` 由外部直接控制
- 根据 `AnimationCurve` 和目标偏移量更新每个襟翼部件
- 支持基于保存路径重新绑定目标部件

**主要用途**

- 把襟翼逻辑从通用机械控制中拆出来，方便单独调试
- 对只有襟翼局部动作的场景更直接

**依赖与配合**

- 不强依赖 JSBSim
- 可以作为 `B737MechanicalController` 之外更轻量的单功能脚本

### 5.7 `Assets/Scripts/Aircraft/B737/B737EngineSpinner.cs`

**脚本定位**

- 发动机风扇/叶片可视化旋转脚本

**主要功能**

- 从 `JsbsimBridge` 读取发动机 `Rpm`
- 支持左右发动机分别配置叶片对象、旋转轴和倍率
- 启动时为叶片自动创建运行时旋转 Pivot
- 根据 RPM 转换成每秒旋转角速度
- 支持低转速不显示旋转的阈值配置

**主要用途**

- 让发动机视觉上有工作状态反馈
- 避免直接绕原始模型轴心旋转造成偏心

**依赖与配合**

- 常规情况下依赖 `JsbsimBridge`
- 与机模层级结构绑定较强，需要 Inspector 正确挂接叶片节点

### 5.8 `Assets/Scripts/Camera/CameraManager.cs`

**脚本定位**

- 多机位管理器

**主要功能**

- 管理一组 `CameraSlot`
- 用 `Shift + 数字键` 快速切换机位
- 切换时关闭旧机位并启用新机位
- 如果目标机位挂的是 `CockpitCameraController`，会优先调用其 `SetActive`
- 可以在运行时通过 `RegisterCamera` 动态注册相机

**主要用途**

- 统一处理客舱、驾驶舱、第三人称等多个固定机位的切换

**依赖与配合**

- 主要配合 `CockpitCameraController`
- 也会顺带管理 `Camera` 组件和 `AudioListener`

### 5.9 `Assets/Scripts/Camera/CockpitCameraController.cs`

**脚本定位**

- 单相机的核心控制器

**主要功能**

- 支持三种模式：
  - `Cockpit`
  - `Cabin`
  - `ThirdPerson`
- 在 `Cockpit` / `Cabin` 模式下：
  - 右键拖动视角
  - 方向键移动
  - `PageUp/PageDown` 上下移动
  - 用本地坐标限制活动范围
- 在 `ThirdPerson` 模式下：
  - 围绕飞机旋转
  - 鼠标滚轮缩放距离
  - 使用包围盒避免相机进入飞机内部
  - 使用 `SphereCast` 做环境避障
- 自动确保近裁剪面合理
- 禁止相机上挂 `Rigidbody` 或 `SphereCollider`，避免高速父物体运动时相机被甩飞

**主要用途**

- 统一处理所有主要机位的具体操作体验
- 减少场景中为不同相机写多套控制代码

**依赖与配合**

- 由 `CameraManager` 控制启停
- 第三人称模式依赖飞机根节点包围盒

### 5.10 `Assets/Scripts/Camera/SingleAudioListenerEnforcer.cs`

**脚本定位**

- 音频监听器冲突解决器

**主要功能**

- 在启用时遍历场景内所有 `AudioListener`
- 只保留当前机位自己的 `AudioListener` 为启用状态
- 提供 `Enforce Single Audio Listener` 上下文菜单

**主要用途**

- 避免 Unity 因多个 `AudioListener` 同时存在而报警
- 保证切换相机后声音来源仍然正确

### 5.11 `Assets/Scripts/World/FloatingOriginManager.cs`

**脚本定位**

- Floating Origin 的核心管理器

**主要功能**

- 持有当前跟踪目标 `target`
- 在 `LateUpdate` 检查目标离中心点是否超过阈值
- 超阈值时广播 `OriginShifted(offset)` 事件
- 支持是否忽略垂直轴
- 支持记录平移日志

**主要用途**

- 解决长距离飞行中浮点精度下降带来的模型抖动、发光异常和相机不稳定

**依赖与配合**

- 被 `JsbsimBridge` 自动使用
- 其他 `FloatingOrigin*` 脚本都监听它的事件

### 5.12 `Assets/Scripts/World/FloatingOriginObject.cs`

**脚本定位**

- 普通场景对象的 Floating Origin 适配器

**主要功能**

- 监听原点平移事件
- 把自身 `transform.position` 加上平移偏移量

**主要用途**

- 用于地形、机场根节点、云层、天空挂点等普通场景对象

### 5.13 `Assets/Scripts/World/FloatingOriginRigidbody.cs`

**脚本定位**

- 刚体对象的 Floating Origin 适配器

**主要功能**

- 监听原点平移事件
- 通过 `rb.position` 而不是普通 `transform.position` 来移动刚体
- 可选调用 `Physics.SyncTransforms()`

**主要用途**

- 避免物理对象在整体世界平移后状态不同步

### 5.14 `Assets/Scripts/World/FloatingOriginParticleSystem.cs`

**脚本定位**

- 粒子系统的 Floating Origin 适配器

**主要功能**

- 监听原点平移事件
- 如果粒子使用世界空间模拟，则把所有活跃粒子整体平移
- 或者按配置直接清空粒子，避免拖尾过长或位置错乱

**主要用途**

- 解决尾气、特效、环境粒子在长距离飞行中漂移的问题

### 5.15 `Assets/Scripts/World/FloatingOriginTrailRenderer.cs`

**脚本定位**

- `TrailRenderer` 的 Floating Origin 适配器

**主要功能**

- 监听原点平移事件
- 可选择：
  - 保留轨迹并整体平移轨迹点
  - 直接清空旧轨迹

**主要用途**

- 避免轨迹线在原点重置时突然拉出一条长线

### 5.16 `Assets/Scripts/World/FloatingOriginCinemachine.cs`

**脚本定位**

- Cinemachine 的 Floating Origin 适配器

**主要功能**

- 监听原点平移事件
- 对虚拟相机调用 `OnTargetObjectWarped`
- 通知 Cinemachine：目标不是在正常运动，而是发生了整体平移

**主要用途**

- 防止带阻尼的 Cinemachine 相机在世界平移后出现大范围追赶和平滑抖动

**依赖与配合**

- 依赖 `Cinemachine`

### 5.17 `Assets/Scripts/Editor/B737/AircraftPrefabFbxExportUtility.cs`

**脚本定位**

- B737 Prefab 到 FBX 的导出工具

**菜单入口**

- `Tools/B737/Export Selected Prefab As FBX`

**主要功能**

- 读取当前选择的 Prefab
- 通过反射调用 Unity 官方 `FBX Exporter` 包的导出 API
- 先导出临时 FBX，再覆盖目标包内的正式 FBX
- 为导出的 FBX 打开 `preserveHierarchy`，关闭 `sortHierarchyByName`
- 用新导出的网格重新回填 Prefab 中的 `MeshFilter` / `SkinnedMeshRenderer`

**主要用途**

- 当 Prefab 层级结构和原始导入的 FBX 结构不一致时，生成一份真正反映当前 Prefab 层级的新 FBX

**依赖与配合**

- 依赖 Unity Package Manager 中的 `FBX Exporter`
- 主要服务于资产整理和后续打包/快照流程

### 5.18 `Assets/Scripts/Editor/B737/AircraftPrefabIsolationUtility.cs`

**脚本定位**

- 版本快照导出工具

**菜单入口**

- `Tools/B737/Save Version Snapshot`

**主要功能**

- 以当前 B737 Prefab 为源，创建一个独立版本快照目录
- 自动建立：
  - 快照 FBX
  - 快照 Prefab
  - `Materials`
  - `Textures`
  - `Models`
  - `.unitypackage`
- 尝试复制源飞机实际使用到的材质和贴图
- 重新连接快照中的材质、网格和贴图引用
- 将快照输出到 `Assets/Aircraft/B737/SavedVersions`

**主要用途**

- 冻结一个“可单独交付、可单独分享、不会污染原工程”的飞机版本
- 适合给别人导出一个稳定版本，或者在大改之前留存快照

**依赖与配合**

- 依赖 `FBX Exporter`
- 与 `AircraftPrefabFbxExportUtility` 有相似的导出逻辑，但目标更完整、更偏版本管理

### 5.19 `Assets/Scripts/Editor/B737/B737MaterialAutoFixer.cs`

**脚本定位**

- 材质自动修复工具

**菜单入口**

- `Tools/B737/Fix Materials From Manifest`

**主要功能**

- 优先查找 `Boeing_B737-800_visual_cockpit_material_texture_manifest.csv`
- 如果找到了 manifest，就按 manifest 的材质名和贴图名修复材质
- 如果没有找到 manifest，就退回到目录扫描模式，自动查找 B737 的材质根目录
- 统一修正贴图导入设置
- 为材质补贴图、切换兼容着色器
- 对玻璃类材质做专门处理

**主要用途**

- 解决旧材质丢贴图、URP 迁移后发紫、法线图导入不正确等问题

**依赖与配合**

- 往往配合 `B737SceneMaterialReplacer` 使用
- 更适合资源级修复，不直接改场景摆放逻辑

### 5.20 `Assets/Scripts/Editor/B737/B737SceneMaterialReplacer.cs`

**脚本定位**

- 场景内材质替换工具

**菜单入口**

- `Tools/B737/Replace Scene Aircraft Materials`
- `Tools/B737/One Click Replace Materials From Selection`

**主要功能**

- 选择一架“目标飞机”和一架“来源飞机”
- 从来源飞机建立渲染器与材质索引
- 按路径、名称、名称+网格、网格名等多重规则匹配目标飞机上的渲染器
- 把来源材质集复制到目标飞机对应渲染器
- 支持包含未激活子物体和 Undo 记录

**主要用途**

- 解决“场景里原来的飞机是紫色/粉色，但新拖进来的飞机正常”的问题
- 适合在不改动场景层级和动画的情况下快速修复材质引用

### 5.21 `Assets/Scripts/Editor/B737/B737LiveryApplier.cs`

**脚本定位**

- 涂装应用工具

**菜单入口**

- `Tools/B737/Apply Livery Folder`

**主要功能**

- 读取一个包含 `objects` 贴图的涂装文件夹
- 识别机身、尾翼、翼梢、发动机、机翼、起落架、透明件、驾驶舱面板等部位的贴图
- 支持 Albedo / Normal / LIT 分开控制是否应用
- 可以只应用到当前选中飞机，也可以应用到共享项目材质
- 默认会克隆材质到 `Assets/Aircraft/B737/Materials/Generated_Livery_Materials`
- 能把外部文件夹中的贴图复制到项目内 `Assets/Aircraft/B737/Liveries`
- 对部分 atlas 贴图做安全检查，避免误用局部贴图

**主要用途**

- 给同一套飞机模型快速切换不同外观
- 支持“一架飞机一套独立材质”的做法，避免多架飞机互相污染

**依赖与配合**

- 依赖项目内现有 B737 材质命名约定
- 常与 `B737MaterialAutoFixer` 配合，先保证材质/着色器兼容，再应用涂装

### 5.22 `Assets/Plugins/ThirdParty/AdjustPivot/Editor/AdjustPivot.cs`

**脚本定位**

- 第三方 Pivot 调整工具

**菜单入口**

- `Window/Adjust Pivot`

**主要功能**

- 以当前选择子物体的位置和旋转作为父物体新的 Pivot
- 当父物体存在 `MeshFilter` 时，直接修改网格顶点、法线、切线数据
- 在必要时创建空父物体，规避非均匀缩放下的旋转轴心问题
- 可选生成 Collider 子物体或 NavMeshObstacle 子物体
- 支持把修改后的网格另存为 `.asset` 或 `.obj`

**主要用途**

- 当外部导入模型的轴心位置不合理时，在 Unity 内快速修正
- 对需要重新设定旋转中心的机轮、舵面、叶片或机舱部件很有用

**说明**

- 这是第三方插件脚本，不建议按业务需求随意改动
- 如果要定制行为，最好先确认不会影响该插件的通用使用方式

## 6. 哪些脚本最关键

如果后续只允许优先理解少数几个脚本，建议按下面顺序看：

1. `JsbsimBridge.cs`
2. `FlightInput.cs`
3. `CockpitCameraController.cs`
4. `FloatingOriginManager.cs`
5. `B737LiveryApplier.cs`
6. `AircraftPrefabIsolationUtility.cs`

原因是这几份脚本分别覆盖了：

- 飞行状态桥接
- 玩家输入
- 观察体验
- 大世界稳定性
- 涂装工作流
- 资产快照/交付工作流

## 7. 总结

当前脚本体系已经形成了比较清晰的分层：

- `Aircraft/B737` 负责飞行和机体可视化
- `Camera` 负责观察与切换
- `World` 负责大坐标稳定性
- `Editor/B737` 负责资源整理、材质修复、涂装应用和版本导出
- `ThirdParty/AdjustPivot` 负责模型轴心编辑

从维护角度看，最核心的运行链路是 `JsbsimBridge -> FlightInput / HUD / 可视化控制脚本 -> Camera / Floating Origin`。
从内容生产角度看，最核心的编辑器链路是 `材质修复 -> 场景替换 -> 涂装应用 -> 快照导出`。

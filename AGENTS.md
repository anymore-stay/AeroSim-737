# AGENTS.md - AeroSim-737

## 项目语言规范

请严格遵守以下规则：

1. 所有对话、解释、建议必须使用**简体中文**。
2. 代码注释必须使用中文。
3. 生成的 Commit Message 必须使用中文。
4. 严禁出现大段未翻译的英文技术名词；API、SDK、Prefab、Shader、RenderTexture 等常用术语可以保留。
5. 请始终使用中文回复用户。

## 项目状态

AeroSim-737 是基于 Unity 和 JSBSim 的 Boeing 737-800 飞行模拟项目。Unity 负责画面、交互、相机、HUD、天气、机场环境、驾驶舱仪表、声音和飞机可视部件动画；JSBSim 作为外部进程负责飞行动力学，并通过仓库根目录下的 `JSBSimBridge/` 与 Unity 双向通信。

截至 **2026-07-20**，当前工程已经完成主功能整合，具备从主场景地面静止、启动双发、手动滑跑起飞、空中操纵到接地反推的完整演示链路，并提供从起飞、五边航线、进近、拉平、接地到滑跑减速的自动驾驶演示。当前阶段是**最终验收、稳定性回归和资源收口**，不再是早期资源整理或主场景搭建阶段。

项目定位仍是可演示、可继续扩展的飞行模拟工程，不应描述为商用级、训练认证级或完整复刻真实 737 全部航电与飞行程序的产品。

## 当前事实源

处理任务时按以下优先级确认事实：

1. 当前代码、Prefab、Scene 和 JSBSim XML 配置。
2. `AGENTS.md`、`CLAUDE.md` 和根目录 `README.md`。
3. `Docs/` 下的专题说明。
4. 历史设计、计划和提交信息。

`Docs/AeroSimUnity_脚本说明.md` 是早期脚本说明，只覆盖当时的部分脚本，不能代替当前代码清单。天气相机问题例外，`Docs/UniStorm-Camera-Weather-Effects-Guide.md` 是该领域的唯一事实源。

## 当前工程入口

- Unity 工程：`AeroSimUnity/`
- Unity 主场景：`AeroSimUnity/Assets/Scenes/MainScene.unity`
- 主飞机 Prefab：`AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab`
- 737 运行时脚本：`AeroSimUnity/Assets/Scripts/Aircraft/B737/`
- 相机脚本：`AeroSimUnity/Assets/Scripts/Camera/`
- 地图脚本：`AeroSimUnity/Assets/Scripts/Map/`
- 大世界脚本：`AeroSimUnity/Assets/Scripts/World/`
- 编辑器工具与测试：`AeroSimUnity/Assets/Scripts/Editor/B737/`
- JSBSim 桥接：`JSBSimBridge/`
- 项目图片：`Pictures/`
- 项目说明：`README.md`

打开 Unity 时必须选择 `AeroSimUnity/`，不要把仓库根目录直接作为 Unity 工程打开。

## 已完成功能基线

- 主场景已整合 B737、北京大兴机场环境、Cesium 地理参考、Floating Origin 和 UniStorm URP 天气系统。
- JSBSim 在 Windows 上默认随 Unity Play 自动启动，使用 UDP `5501` 向 Unity 输出状态，使用 TCP `5502` 接收控制命令；退出 Play 时会清理由 Unity 启动的进程树。
- 飞机以地面静止、双发运转、油门怠速、刹车锁定状态开始，可完成滑跑、起飞、转弯、配平、襟翼/扰流板、起落架、刹车和反推操作。
- 键盘与图马思特 TCA/A320 侧杆可并行输入；侧杆支持横滚、俯仰、扭转方向舵、油门轴、POV 视角和断线重连。
- 飞控包含协调转弯、地面前轮转向、坡度角保护、侧滑保护、起落架低空收起限制和油门推起时自动松刹车。
- 按 `O` 可启用五边自动驾驶演示，自动完成起飞、上风边、侧风边、下风边、基准边、最后进近、拉平、接地和滑跑减速；再次按 `O` 立即恢复手动控制。
- 驾驶舱操纵盘、操纵柱、油门杆、舵面、襟翼、起落架、机轮和发动机风扇等可视部件已接入状态动画。
- HUD 已显示 JSBSim 连接、速度、高度、姿态、发动机 `N1`、控制量和操作提示。
- PFD、ND、EICAS、备用仪表、Clock ET 和可点击 FMS 显示已接入驾驶舱。
- 独立飞行地图按 `M` 打开，支持航迹、飞机朝向、缩放、拖动、平移和窗口尺寸调整，并提供本地地图背景。
- 驾驶舱、客舱、第三人称相机统一由 `CameraManager` 管理并强制互斥；重新切回驾驶舱时恢复初始机位，第三人称具备环绕、缩放、避障、机体包围盒保护和近地视角限制。
- UniStorm 已提供昼夜、云层、雨雪、雷暴、雾、天气声音和右上角天气/时间菜单；夜间机场和机体外部灯光已适配。
- 发动机声音、飞行气流、接地/起落架声音、天气声音、航迹云、发动机热浪和按空速、迎角、襟翼及湿度驱动的翼尖涡流等效果已接入。
- 讯飞语音控制按 `Y` 使用，支持油门、起落架、襟翼、扰流板、刹车、配平和暂停指令；语音反推出于安全原因保持禁用。

## 默认操作基线

| 按键/输入 | 当前行为 |
| --- | --- |
| `W` / `S` | 低头 / 抬头；升降舵松键后保持当前值 |
| `A` / `D` | 左滚 / 右滚；松键后平滑回中 |
| `Q` / `E` | 左偏航 / 右偏航，并参与协调转弯和地面前轮转向 |
| `Z` / `X` | 抬头配平 / 低头配平；配平独立保持 |
| `LeftShift` | 增加正推；退出反推后重新建立正推 |
| `LeftControl` | 将油门收向怠速 |
| `LeftControl + LeftShift` | 满反推；代码允许全时触发，实际操作应在接地后使用 |
| `F` / `V` | 襟翼增加 / 减少一级 |
| `R` / `T` | 扰流板增加 / 减少一级 |
| `G` | 起落架收放；低于约 `10 ft AGL` 时拒绝收起 |
| `B` | 刹车开关；正推超过阈值时自动松刹车 |
| `Esc` | 同时暂停 / 恢复 Unity 与 JSBSim |
| `O` | 启用 / 退出五边自动驾驶演示；退出后恢复手动控制 |
| `F5` | 自动驾驶启用时手动切换到下一航段，仅用于调试和验收 |
| `Shift + 7/8/9` | 客舱 / 驾驶舱 / 第三人称相机 |
| 鼠标右键拖动 | 转动当前视角 |
| 方向键、`PageUp/PageDown` | 驾驶舱和客舱内移动视角 |
| 鼠标滚轮 | 第三人称缩放；鼠标位于地图上时缩放地图 |
| 侧杆 POV 帽 | 转动驾驶舱、客舱或第三人称视角 |
| `Tab` | 显示 / 隐藏 HUD |
| `1` | 驾驶舱操纵杆显示 / 隐藏 |
| `2` | 天气和时间菜单 |
| `M` | 飞行地图 |
| 按住 `Y` | 录制语音，松开后识别并执行指令 |

## 运行链路

```text
键盘 / TCA A320 侧杆 / 讯飞语音
                    ↓
                FlightInput
                    ↓ TCP 5502
                 JSBSim
                    ↓ UDP 5501
              JsbsimBridge
        ┌───────────┼───────────┐
        ↓           ↓           ↓
   飞机位置姿态   HUD/仪表    舵面/发动机/声音
```

Unity Play 的默认启动顺序是：`JsbsimBridge` 先监听 UDP，再由 `JsbsimProcessLauncher` 运行 `JSBSimBridge/start_jsbsim.bat`，最后持续连接 TCP 控制端口。

## 目录约定

```text
AeroSim-737/
├── AeroSimUnity/
│   ├── Assets/
│   │   ├── Aircraft/B737/
│   │   │   ├── Prefabs/        # 正式飞机 Prefab
│   │   │   ├── Models/         # FBX 与模型资源
│   │   │   ├── Materials/      # 飞机材质
│   │   │   ├── Textures/       # 飞机贴图
│   │   │   ├── Liveries/       # 涂装资源
│   │   │   └── Instruments/    # PFD、ND、EICAS、FMS、备用仪表等
│   │   ├── Environment/        # 机场与环境资源
│   │   ├── Resources/          # 运行时加载资源，包括地图背景
│   │   ├── Scenes/             # Unity 场景，当前运行入口为 MainScene
│   │   ├── Scripts/
│   │   │   ├── Aircraft/B737/  # 飞机运行时逻辑和 Voice 子目录
│   │   │   ├── Camera/         # 相机与 AudioListener 管理
│   │   │   ├── Map/            # 飞行地图
│   │   │   ├── World/          # Floating Origin 与帧率显示
│   │   │   └── Editor/B737/    # 编辑器工具和 EditMode 测试
│   │   ├── UniStorm Weather System/
│   │   ├── Plugins/ThirdParty/
│   │   └── Settings/
│   ├── Packages/
│   └── ProjectSettings/
├── JSBSimBridge/               # 启动脚本、初始条件和双向通信配置
├── XplaneAssets/               # 原始机模/场景导入源与辅助脚本
├── Pictures/                   # README 图片
├── Docs/                       # 专题说明、设计和历史实施计划
├── AGENTS.md
├── CLAUDE.md
└── README.md
```

## 通用开发规则

- 737 运行时逻辑放在 `AeroSimUnity/Assets/Scripts/Aircraft/B737/`，语音逻辑放在其 `Voice/` 子目录。
- 通用相机逻辑放在 `AeroSimUnity/Assets/Scripts/Camera/`。
- Floating Origin 和大世界逻辑放在 `AeroSimUnity/Assets/Scripts/World/`。
- 地图逻辑放在 `AeroSimUnity/Assets/Scripts/Map/`。
- B737 编辑器工具和 EditMode 测试放在 `AeroSimUnity/Assets/Scripts/Editor/B737/`。
- 第三方插件保留原目录结构，不要为了整理目录拆散 UniStorm、Cesium 或其他插件资源。
- 脚本文件名与类名保持一致，使用 `PascalCase`。
- 新注释使用中文，只在公共 API、非显然约束或复杂算法前补充简短说明。
- 修改行为时优先延续现有组件边界，不为一次性需求增加大范围抽象或无关重构。
- 测试范围随风险扩大：纯文案可做静态检查；共享飞控、桥接、相机、天气或仪表链路必须补充对应测试和 Play 模式验证。

## Unity 资源规则

- 主场景中的飞机必须来自 `B737.prefab`，不要长期使用裸 FBX 或复制出的临时飞机对象开发。
- 移动、重命名或复制 Unity 资源时必须连同 `.meta` 文件一起处理。
- 修改 Prefab、Scene、材质、RenderTexture 或贴图后，必须回 Unity 检查 Missing Script、Missing Reference、材质丢失和 Console 编译错误。
- `MainScene.unity` 和 `B737.prefab` 是当前演示链路的核心资产，改动前后都要核对引用稳定性。
- `AeroSimUnity/Assets/Settings/`、`AeroSimUnity/ProjectSettings/` 和 `Packages/packages-lock.json` 属于工程配置区，没有明确需求时不要手动改动。
- UniStorm、Cesium、天空盒和体积云材质可能在 Unity Play 或保存场景后产生自动脏改动；提交前逐项确认是否属于本次任务。
- 不要为了消除 YAML 尾随空格而批量格式化 Scene、Prefab 或材质文件，这会制造难以审查的大型无意义差异。

## JSBSim 约定

- 默认状态端口为 UDP `5501`，控制端口为 TCP `5502`，不要在单侧擅自修改。
- `JsbsimBridge.startJsbsimAutomatically` 在主 Prefab 中默认为开启；自动启动仅支持 Windows。手动启动时应先进入 Unity Play，再运行 `JSBSimBridge/start_jsbsim.bat`。
- 本机 JSBSim 路径优先通过 `JSBSIM_DIR` 配置，可指向包含 `JSBSim.exe` 的目录或直接指向该可执行文件。
- `JSBSimBridge/unity_air.xml` 当前是地面静止初始状态；`b737_unity.xml` 会让双发进入运行状态并保持怠速。
- 油门 `0%` 表示发动机怠速，不表示关车。需要做停发状态时必须单独设计发动机启停链路。
- `unity_output.xml` 中兼容字段 `rpm` 实际对应 `propulsion/engine[0]/n1`，数值单位是 N1 百分比，不是真实 RPM。
- 发动机风扇当前按 `100% N1 -> 5175 × 90% = 4657.5 RPM` 映射。Prefab 基线为 `rpmToDegreesPerSecond=6`、左右 `rpmMultiplier=46.575`、`minVisibleRpm=1`、`useAbsoluteRpm=true`。
- 修改 JSBSim 输出属性时必须保证 caption 与 Unity 读取名称一致，并检查 UDP 数据列能被解析。
- Unity 退出 Play 时只清理由当前 Unity 会话启动的 JSBSim 进程，不要把清理逻辑扩大到用户手动启动的无关进程。

## 飞控与输入约定

- 键盘输入和 TCA/A320 侧杆输入是叠加关系；侧杆断开后键盘仍可用，不要改成互斥模式。
- 升降舵采用保持型键盘修正，副翼和方向舵采用松键回中；俯仰配平是独立保持状态。
- 反推使用带符号油门，负值表示反推。语音反推必须继续禁用，除非另有明确的安全设计和测试。
- 五边自动驾驶通过 `FlightInput` 的外部控制接口接管舵面、油门、襟翼、扰流板和刹车；`O` 退出后必须清除外部控制并恢复键盘和侧杆输入。
- 自动驾驶接管期间仍允许 `G` 手动切换起落架；不要把当前五边航线演示描述为真实 737 自动驾驶、自动油门或认证级自动着陆系统。
- 起落架收起必须保留动画进行中门控和最低离地高度限制。
- 坡度角保护、侧滑保护、协调转弯和地面前轮转向属于当前手感基线，修改时必须同步检查键盘和侧杆两条输入链路。
- 驾驶舱油门摇杆当前是 `FlightInput.Throttle` 的可视反馈，不是独立鼠标拖动输入控件。

## 相机与声音约定

- 相机统一由 `CameraManager` 管理，客舱、驾驶舱和第三人称默认对应 `Shift+7/8/9`。
- 每次切换必须关闭其他所有已注册相机。重新激活驾驶舱相机时恢复初始本地位置和旋转；客舱保留当前机位；第三人称重新初始化环绕状态。
- 高速跟随飞机的驾驶舱和客舱相机不能挂 `Rigidbody`；其移动和转向必须继续使用父物体本地 Transform。
- 第三人称相机必须每帧跟随飞机，并保留避障、机体包围盒保护和低于 `100 ft AGL` 时禁止转到飞机下方的限制。
- 任意时刻只能有一个可用的 `AudioListener`。增加相机时必须接入 `CameraManager` 和 `SingleAudioListenerEnforcer`。
- 天气、发动机、接地和机舱声音修改后要在三种相机视角分别试听，不能只检查当前视角。

## 天气系统约定

- 当前主天气系统为 `UniStorm URP System`。
- 处理驾驶舱、客舱、第三人称之间的雨、雪、冰雹、雾、沙尘、飘动物、闪电、云影或声音密度、覆盖、偏移、跟随、延迟不一致时，必须先完整读取：
  - Codex：`.agents/skills/aerosim-debug-camera-weather-effects/SKILL.md`
  - Claude Code：`.claude/skills/aerosim-debug-camera-weather-effects/SKILL.md`
  - 详细指南：`Docs/UniStorm-Camera-Weather-Effects-Guide.md`
- 两个 Skill 入口必须保持同步，详细指南是该类问题的唯一事实源。
- 天气菜单由 UniStorm 生成，项目脚本 `B737UniStormWeatherMenuController` 负责右上角布局和中文显示；不要直接硬改运行时生成的 UI 对象。
- 修改降水跟随相机、雾距离、太阳盘或天气声音时，优先检查 `B737UniStormPrecipitationAnchor.cs`、`B737UniStormFogDistanceController.cs`、`B737UniStormSunDiscAnchor.cs` 和 `UniStormSystem.cs`。
- 当前声音要求是：切换到降水天气后立即听到目标声音，云层、雨滴和湿润 Shader 继续按原过渡速度变化。

## 语音、地图与仪表约定

- 讯飞凭据只能从 `XFYUN_APP_ID`、`XFYUN_API_KEY`、`XFYUN_API_SECRET` 用户环境变量读取，严禁写入 C#、Prefab、Scene、文档示例真实值或提交记录。
- 语音接入细节以 `Docs/讯飞语音控制接入说明.md` 为准。改动解析规则时同步更新 `B737VoiceCommandParserTests.cs`。
- 飞行地图由 `FlightMapOverlay` 在运行时自动创建，按 `M` 显示。修改 UI 时保留拖动、地图平移、边缘缩放、航迹和本地背景降级能力。
- PFD、ND、EICAS、FMS、Clock 和备用仪表使用独立相机、Canvas 或 RenderTexture 的部分较多；改动时必须检查层级、Culling Mask、RenderTexture 和主相机裁剪是否仍正确。
- FMS 当前是可点击显示与基础页面演示，不应描述为完整 FMC 航路计算或自动驾驶系统。

## Git 和 LFS

- 仓库使用 Git LFS 管理 FBX、贴图、音视频、压缩包和文档类大资源。首次拉取执行 `git lfs install` 和 `git lfs pull`。
- 如果资源内容是 LFS 文本指针，先运行 `git lfs pull`，不要误以为资源损坏后重新导入。
- 不提交 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/`、`.vs/`、`*.csproj`、`*.sln`。
- `AeroSimUnity/Captures/` 是运行时生成的自动驾驶遥测和临时验证截图目录，不提交其中的 CSV、截图或其他验证产物；需要长期保留的结论应整理到 `Docs/`。
- Commit Message 使用中文，建议格式为 `<类型>：<简述>`，类型可用 `功能`、`修复`、`资源`、`脚本`、`文档`、`配置`、`性能`、`测试`。
- 仓库可能存在用户尚未提交的修改。只处理本次任务相关文件，不回退、不覆盖、不顺手提交无关改动。

## 当前维护重点

- 保持 `MainScene.unity`、`B737.prefab`、JSBSim 配置和三种相机引用稳定。
- 以完整演示流程做回归：打开 `MainScene`、JSBSim 自动启动、松刹车滑跑、起飞、转弯、配平、襟翼/起落架、地图、天气、语音、接地和反推。
- 单独回归 `O` 五边自动驾驶全流程，检查首次接通时跑道基准捕获、各航段切换、最后进近、拉平、主轮接地、扰流板/刹车和退出后手动接管。
- 在键盘与 TCA/A320 侧杆两套输入下检查飞控手感和保护逻辑。
- 检查 PFD、ND、EICAS、FMS、备用仪表、HUD 和地图在驾驶舱/客舱/第三人称之间的可见性与性能。
- 检查翼尖涡流在不同空速、迎角、襟翼、湿度和 Floating Origin 重定位条件下的出现、消散与连续性。
- 继续处理最终光照、声音混音、材质、Missing Reference 和 Unity 自动脏改动，不主动扩张到新的大型系统。
- 更新 README 或协作文档时使用仓库相对路径，不写个人电脑绝对路径；状态性说明应标注核对日期。

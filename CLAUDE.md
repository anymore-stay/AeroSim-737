# CLAUDE.md - AeroSim-737

## 项目语言规范

请严格遵守以下规则：

1. 所有对话、解释、建议必须使用**简体中文**。
2. 代码注释必须使用中文。
3. 生成的 Commit Message 必须使用中文。
4. 严禁出现大段未翻译的英文技术名词；API、SDK、Prefab、Shader、RenderTexture 等常用术语可以保留。
5. 请始终使用中文回复用户。

## 项目定位

AeroSim-737 是基于 Unity 的 Boeing 737-800 飞行模拟项目。Unity 负责画面、交互、相机、HUD、天气、机场环境、驾驶舱仪表和飞机可视部件动画；JSBSim 作为外部进程负责飞行物理，并通过仓库根目录下的 `JSBSimBridge/` 与 Unity 对接。

当前项目已经从“资源整理阶段”推进到“主场景功能整合与视觉调试阶段”：主飞机 Prefab、JSBSim 桥接、驾驶舱/客舱/第三人称相机、HUD、UniStorm 天气系统、北京大兴机场环境、基础仪表显示和部分音频逻辑已经接入主场景。

## 当前工程入口

- 仓库根目录：当前工作区为 `D:\Desktop\AeroSim-737`
- Unity 工程目录：`AeroSimUnity/`
- Unity 主场景：`AeroSimUnity/Assets/Scenes/MainScene.unity`
- 主飞机 Prefab：`AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab`
- JSBSim 桥接目录：`JSBSimBridge/`
- 项目图片目录：`Pictures/`
- 项目说明文档：`README.md`

打开 Unity 时使用 `AeroSimUnity/`，不要把仓库根目录直接当成 Unity 工程打开。

## 当前功能进度

- 主场景 `MainScene.unity` 已整合 B737、北京大兴机场环境、Cesium 地理参考和 UniStorm URP 天气系统。
- UniStorm 天气系统已作为真实场景对象存在，包括太阳、月亮、风区、体积云、云影、星空和天气菜单。
- 主场景原有 Directional Light 已不作为主太阳光使用，避免和 UniStorm 太阳叠光。
- 天气菜单默认使用按键 `2` 打开，右上角显示天气选择、时间条和当前时间文本，天气名称已中文化。
- HUD 已提示 `2 打开天气和时间选择`。
- 天气声音已调低，场景中 `WeatherSoundsVolume` 当前为 `0.3`；降水天气切换后雨声立即到目标音量，云层和雨滴视觉仍按原过渡速度变化。
- 北京大兴机场地面材质已接入 UniStorm 草地纹理，当前地面 tiling 调到 `100 x 100`，用于降低近景模糊感。
- 相机系统通过 `CameraManager` 管理客舱、驾驶舱和第三人称视角，并确保当前相机拥有唯一可用的 `AudioListener`。
- JSBSim 桥接、键盘飞控、起落架、襟翼、扰流板、HUD 数据、PFD/ND/EICAS 等基础链路已具备回归测试入口。

## 目录约定

```text
AeroSim-737/
├── AeroSimUnity/
│   ├── Assets/
│   │   ├── Aircraft/B737/
│   │   │   ├── Prefabs/        # 飞机正式 Prefab
│   │   │   ├── Models/         # FBX 与 .fbm 模型资源
│   │   │   ├── Materials/      # 材质资源
│   │   │   ├── Textures/       # 主贴图资源
│   │   │   ├── Liveries/       # 涂装资源
│   │   │   └── Instruments/    # 驾驶舱仪表资源与仪表系统
│   │   ├── Environment/        # 机场与环境资源
│   │   ├── Scenes/             # 场景资源，当前主场景为 MainScene
│   │   ├── Scripts/
│   │   │   ├── Aircraft/B737/  # B737 专属运行时脚本
│   │   │   ├── Camera/         # 通用相机脚本
│   │   │   ├── Editor/B737/    # B737 编辑器工具与测试
│   │   │   ├── Map/            # 地图/航图覆盖层
│   │   │   └── World/          # Floating Origin 等世界脚本
│   │   ├── UniStorm Weather System/
│   │   ├── Plugins/ThirdParty/
│   │   └── Settings/
│   ├── Packages/
│   └── ProjectSettings/
├── JSBSimBridge/
├── Pictures/
├── Docs/
└── README.md
```

## 开发规则

- 737 运行时逻辑统一放在 `AeroSimUnity/Assets/Scripts/Aircraft/B737/`。
- 通用相机逻辑放在 `AeroSimUnity/Assets/Scripts/Camera/`。
- Floating Origin 和大世界相关逻辑放在 `AeroSimUnity/Assets/Scripts/World/`。
- 地图/航图覆盖层相关逻辑放在 `AeroSimUnity/Assets/Scripts/Map/`。
- B737 编辑器工具和编辑器测试放在 `AeroSimUnity/Assets/Scripts/Editor/B737/`。
- 第三方插件放在 `AeroSimUnity/Assets/Plugins/ThirdParty/` 或插件自身要求的目录；不要随意拆散插件目录。
- 仪表资源和后续仪表系统放在 `AeroSimUnity/Assets/Aircraft/B737/Instruments/`。
- 脚本文件名与类名保持一致，使用 `PascalCase`。
- 注释以中文为主，只在公共 API 或非显然实现处补充简短说明。

## Unity 资源规则

- 场景中主飞机应来自 `B737.prefab`，不要长期直接拖 FBX 或裸 GameObject 到主场景里开发。
- 移动、重命名或复制 Unity 资源时，必须连同 `.meta` 文件一起处理。
- 修改 Prefab、Scene、材质、贴图后，需要回到 Unity 检查 Missing Script / Missing Reference。
- 材质和贴图尽量保持稳定命名；如果必须改名，要同步检查 Prefab、Scene 和 Editor 工具里的路径引用。
- `AeroSimUnity/Assets/Settings/` 和 `AeroSimUnity/ProjectSettings/` 属于工程配置区域，除非明确要调整全局设置，否则尽量少手动改。
- `AeroSimUnity/Packages/packages-lock.json` 由 Unity Package Manager 维护，不要手写改依赖锁定结果。
- UniStorm、Cesium 等插件目录中资源引用较密，修改前先确认是否为运行时自动写入的脏改动。

## 天气系统约定

- 当前主天气系统为 `UniStorm URP System`。
- 处理驾驶舱、客舱、第三人称之间的天气密度、覆盖、偏移、跟随、雾或声音不一致问题时，必须先读取当前 Agent 对应的项目 Skill：
  - Codex：`.agents/skills/aerosim-debug-camera-weather-effects/SKILL.md`
  - Claude Code：`.claude/skills/aerosim-debug-camera-weather-effects/SKILL.md`
- 两个 Skill 入口必须保持一致，并继续完整读取 `Docs/UniStorm-Camera-Weather-Effects-Guide.md`。详细指南是该类问题的唯一事实源。
- 天气菜单使用 UniStorm 自带菜单，项目脚本 `B737UniStormWeatherMenuController` 负责右上角布局和中文显示。
- 按键 `2` 打开天气和时间选择。
- 修改天气 UI 时优先改 `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormWeatherMenuController.cs`，不要直接硬改运行时生成的 UI 对象。
- 修改降水跟随相机、雾距离、天气声音时，优先检查：
  - `B737UniStormPrecipitationAnchor.cs`
  - `B737UniStormFogDistanceController.cs`
  - `UniStormSystem.cs`
- 降水声音当前要求：切换到降水天气后立即听到声音；云、雨滴、湿润 Shader 仍保持原视觉过渡。

## Git 和 LFS

- 这个仓库使用 Git LFS 管理大体积 Unity 资源，包括 FBX、贴图、音视频、压缩包和文档类资源。
- 新成员首次拉取前应先安装 Git LFS，并执行 `git lfs install`。
- 如果资源打开后是 LFS 文本指针，进入仓库根目录执行 `git lfs pull`。
- 不提交 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/`、`.vs/`、`*.csproj`、`*.sln`。
- Commit Message 建议格式：`<类型>：<简述>`，类型可用 `资源`、`脚本`、`修复`、`文档`、`配置`。

## JSBSim 约定

- JSBSim 配置、启动脚本和安装包集中放在 `JSBSimBridge/`。
- Unity 侧通过 B737 运行时脚本与 JSBSim 通信，不要把外部桥接配置散落到 `Assets/` 根目录。
- 团队成员本机 JSBSim 安装路径可以不同，优先通过 `JSBSIM_DIR` 环境变量指向包含 `JSBSim.exe` 的目录。
- 如果不启动 JSBSim，仍可以测试本地动画、相机、HUD、天气、时间选择和部分仪表 UI。

## 当前开发重点

- 保持 `MainScene.unity` 和 `B737.prefab` 引用稳定。
- 每次 Unity Play 后注意是否产生场景、天空盒、体积云材质等自动脏改动；提交前只保留本次任务真正需要的修改。
- 先保证本地键盘输入、起落架、襟翼、舵面、发动机、HUD、相机切换、天气切换、天气声音可回归。
- 继续细调太阳直射、机舱阴影、天气音量、机场地面材质和仪表可读性。
- 更新 README 时使用相对路径，不要写入个人电脑的绝对路径。

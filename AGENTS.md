# AGENTS.md - AeroSim-737

## 项目定位

AeroSim-737 是基于 Unity 的 Boeing 737-800 飞行模拟项目。Unity 负责画面、交互、相机、HUD 和飞机可视部件动画；JSBSim 作为外部进程负责飞行物理，并通过仓库根目录下的 `JSBSimBridge/` 与 Unity 对接。

当前目标是把项目稳定成适合小组协作的 Unity 工程：资源路径清晰、Prefab 和场景引用可维护、Git LFS 能正确管理大文件，后续再逐步完善 JSBSim 闭环、仪表系统和完整五边飞行流程。

## 当前工程入口

- 仓库根目录：`D:\OneDrive\Desktop\AeroSim-737`
- Unity 工程目录：`AeroSimUnity/`
- Unity 主场景：`AeroSimUnity/Assets/Scenes/MainScene.unity`
- 主飞机 Prefab：`AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab`
- JSBSim 桥接目录：`JSBSimBridge/`
- 项目说明文档：`README.md`

打开 Unity 时使用 `AeroSimUnity/`，不要把仓库根目录直接当成 Unity 工程打开。

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
│   │   │   └── Instruments/    # 驾驶舱仪表资源与后续仪表系统
│   │   ├── Scenes/             # 场景资源，当前主场景为 MainScene
│   │   ├── Scripts/
│   │   │   ├── Aircraft/B737/  # B737 专属运行时脚本
│   │   │   ├── Camera/         # 通用相机脚本
│   │   │   ├── Editor/B737/    # B737 编辑器工具
│   │   │   └── World/          # Floating Origin 等世界脚本
│   │   ├── Plugins/ThirdParty/ # 第三方插件
│   │   └── Settings/           # 渲染与工程内资源设置
│   ├── Packages/
│   └── ProjectSettings/
├── JSBSimBridge/
├── Docs/
└── README.md
```

## 开发规则

- 737 运行时逻辑统一放在 `AeroSimUnity/Assets/Scripts/Aircraft/B737/`
- 通用相机逻辑放在 `AeroSimUnity/Assets/Scripts/Camera/`
- Floating Origin 和大世界相关逻辑放在 `AeroSimUnity/Assets/Scripts/World/`
- B737 编辑器工具放在 `AeroSimUnity/Assets/Scripts/Editor/B737/`
- 第三方插件放在 `AeroSimUnity/Assets/Plugins/ThirdParty/`
- 仪表资源和后续仪表系统放在 `AeroSimUnity/Assets/Aircraft/B737/Instruments/`
- 脚本文件名与类名保持一致，使用 `PascalCase`
- 注释以中文为主，只在公共 API 或非显然实现处补充简短说明

## Unity 资源规则

- 场景中主飞机应来自 `B737.prefab`，不要长期直接拖 FBX 或裸 GameObject 到主场景里开发
- 移动、重命名或复制 Unity 资源时，必须连同 `.meta` 文件一起处理
- 修改 Prefab、Scene、材质、贴图后，需要回到 Unity 检查 Missing Script / Missing Reference
- 材质和贴图尽量保持稳定命名；如果必须改名，要同步检查 Prefab、Scene 和 Editor 工具里的路径引用
- `AeroSimUnity/Assets/Settings/` 和 `AeroSimUnity/ProjectSettings/` 属于工程配置区域，除非明确要调整全局设置，否则尽量少手动改
- `AeroSimUnity/Packages/packages-lock.json` 由 Unity Package Manager 维护，不要手写改依赖锁定结果

## Git 和 LFS

- 这个仓库使用 Git LFS 管理大体积 Unity 资源，包括 FBX、贴图、音视频、压缩包和文档类资源
- 新成员首次拉取前应先安装 Git LFS，并执行 `git lfs install`
- 如果资源打开后是 LFS 文本指针，进入仓库根目录执行 `git lfs pull`
- 不提交 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/`、`.vs/`、`*.csproj`、`*.sln`
- Commit Message 建议格式：`<类型>：<简述>`，类型可用 `资源`、`脚本`、`修复`、`文档`、`配置`

## JSBSim 约定

- JSBSim 配置、启动脚本和安装包集中放在 `JSBSimBridge/`
- Unity 侧通过 B737 运行时脚本与 JSBSim 通信，不要把外部桥接配置散落到 `Assets/` 根目录
- 团队成员本机 JSBSim 安装路径可以不同，优先通过 `JSBSIM_DIR` 环境变量指向包含 `JSBSim.exe` 的目录
- 如果不启动 JSBSim，仍可以测试部分本地动画、相机、HUD 和输入逻辑

## 当前开发重点

- 保持 `MainScene.unity` 和 `B737.prefab` 引用稳定
- 先修路径、材质、脚本引用和资源归类，再做视觉细调
- 先保证本地键盘输入、起落架、襟翼、舵面、发动机、HUD、相机切换可回归
- 仪表系统已经有目录入口，但默认不接入当前主飞行链路，等需求明确后再开发

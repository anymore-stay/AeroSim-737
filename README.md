# AeroSim-737

基于 Unity 的 Boeing 737-800 飞行模拟项目。当前主工程为 [AeroSimUnity](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity)，目标是将飞机视觉、场景表现与 JSBSim 飞行物理解耦，通过外部桥接实现可持续维护的团队开发工作流。

当前机型：`Boeing 737-800`
当前主场景：`MainScene`
仓库地址：`https://github.com/anymore-stay/AeroSim-737.git`
外部物理桥接目录：`JSBSimBridge/`

---

## 当前状态

> 阶段：项目结构已定型，当前正在进行功能校正、资源整理与团队规范固化。

已完成：
- 737 主资源已归档到 `Assets/Aircraft/B737`
- `Assets/Aircraft/B737/Instruments/` 已建立，作为后续仪表系统入口目录
- 主 prefab 已统一为 `B737.prefab`
- 主场景已统一为 `MainScene.unity`
- JSBSim 外部文件已迁到仓库根下的 `JSBSimBridge/`
- B737 相关运行时脚本、编辑器工具、第三方插件已按新结构归类

进行中：
- prefab / 场景引用校正
- 飞机各部件动画与交互回归验证
- 材质、贴图、涂装工具路径统一
- 团队开发规范固化

---

## 项目结构

```text
AeroSim-737/
├── AeroSimUnity/                         # 正式 Unity 工程根目录
│   ├── Assets/
│   │   ├── Aircraft/
│   │   │   └── B737/
│   │   │       ├── Prefabs/             # 飞机正式 Prefab
│   │   │       ├── Models/              # FBX 与 .fbm 模型资源
│   │   │       ├── Materials/           # 材质资源
│   │   │       ├── Textures/            # 主贴图资源
│   │   │       ├── Liveries/            # 涂装资源
│   │   │       └── Instruments/         # 仪表系统目录，当前为后续开发预留
│   │   ├── Scenes/                      # 场景资源，当前主场景为 MainScene
│   │   ├── Scripts/
│   │   │   ├── Aircraft/B737/           # 所有 B737 专属运行时脚本
│   │   │   ├── Camera/                  # 通用相机脚本
│   │   │   ├── World/                   # Floating Origin 等世界脚本
│   │   │   └── Editor/B737/             # B737 编辑器工具
│   │   ├── Plugins/ThirdParty/          # 第三方插件
│   │   ├── Environment/                 # 环境资源
│   │   └── Settings/                    # 渲染与工程内资源设置
│   ├── Packages/
│   └── ProjectSettings/
├── JSBSimBridge/                        # 外部桥接配置与启动脚本
├── Docs/                                # 项目文档
└── README.md
```

---

## 关键资源

- 主场景：
  [MainScene.unity](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Scenes\MainScene.unity)

- 主飞机 prefab：
  [B737.prefab](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Aircraft\B737\Prefabs\B737.prefab)

- 主模型：
  [B737_Aircraft.fbx](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Aircraft\B737\Models\B737_Aircraft.fbx)

- 模型贴图目录：
  [B737_Aircraft.fbm](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Aircraft\B737\Models\B737_Aircraft.fbm)

- 仪表系统目录：
  [Instruments](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Aircraft\B737\Instruments)

- 外部 JSBSim 配置：
  [b737_unity.xml](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\b737_unity.xml)
  [unity_output.xml](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\unity_output.xml)
  [start_jsbsim.bat](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\start_jsbsim.bat)

---

## 脚本归类规范

所有和 737 直接相关的运行时逻辑，统一放在：

- [Assets/Scripts/Aircraft/B737](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Scripts\Aircraft\B737)

包括但不限于：
- 飞行动画与控制
- JSBSim 桥接
- HUD
- 发动机旋转
- 起落架、襟翼、驾驶盘、舵面逻辑

通用脚本按职责分开：

- `Assets/Scripts/Camera`
  只放相机控制与切换相关脚本
- `Assets/Scripts/World`
  只放 Floating Origin 和大世界相关脚本
- `Assets/Scripts/Editor/B737`
  只放 B737 编辑器工具

`Assets/Aircraft/B737/Instruments` 单独保留给驾驶舱仪表相关资源、材质、贴图和后续专属脚本；当前阶段可以整理目录和放置占位资源，但默认不接入主飞行流程。

---

## 团队开发规范

### 1. 新增资源放哪里

- 新增 737 prefab：放到 `Assets/Aircraft/B737/Prefabs`
- 新增 737 模型：放到 `Assets/Aircraft/B737/Models`
- 新增 737 材质：放到 `Assets/Aircraft/B737/Materials`
- 新增 737 贴图：放到 `Assets/Aircraft/B737/Textures`
- 新增 737 涂装：放到 `Assets/Aircraft/B737/Liveries/<LiveryName>/objects`
- 新增仪表相关资源：放到 `Assets/Aircraft/B737/Instruments`
- 新增 737 运行时脚本：放到 `Assets/Scripts/Aircraft/B737`
- 新增 B737 编辑器工具：放到 `Assets/Scripts/Editor/B737`
- 新增通用相机脚本：放到 `Assets/Scripts/Camera`
- 新增通用世界脚本：放到 `Assets/Scripts/World`
- 新增第三方插件：放到 `Assets/Plugins/ThirdParty`

### 2. 不要怎么放

- 不要把 737 资源直接丢到 `Assets/` 根目录
- 不要把运行时脚本混放到 `Editor` 目录
- 不要把通用脚本和 B737 专属脚本混在一起
- 不要直接把 `.fbx` 拖进场景长期使用，场景里应优先使用 prefab

### 3. 命名规范

- 脚本文件名与类名一致，使用 `PascalCase`
  例如：`B737EngineSpinner.cs`
- Prefab 使用简洁、稳定命名
  例如：`B737.prefab`
- 场景使用明确语义命名
  例如：`MainScene.unity`
- 贴图和资源文件尽量保留稳定命名，避免大面积断引用

### 4. 修改资源时的原则

- 材质、贴图等资源若无明确必要，不要随意改名，减少引用断裂
- 如果必须改名，要同时检查：
  - prefab 引用
  - scene 引用
  - editor 工具中的硬编码路径
  - 相关 `.meta` 是否一起处理

### 5. `.meta` 文件规则

- Unity 资源复制、移动、重命名时必须连同 `.meta` 一起处理
- 不要手工删除 `.meta`
- 不要只复制资源文件不复制 `.meta`

### 6. 禁止提交的内容

- `Library/`
- `Temp/`
- `Logs/`
- `obj/`
- `UserSettings/`
- `.vs/`
- `*.csproj`
- `*.sln`

### 7. Unity 版本与 Cesium 包一致性

- 仓库推荐版本是 `Unity 2022.3.62f3c1`
- `2022.3.62` 同家族版本可运行
  例如：`2022.3.62f1c1`、`2022.3.62f3c1`
- 如果要改场景、Prefab、材质、ProjectSettings，仍建议尽量使用推荐版本再提交
- 项目已内置 `AeroSimCesiumPackageGuard`，会在启动时检查版本，并自动把 `CesiumDefaultTilesetMaterial.mat` 同步回团队基线，减少 `immutable package unexpectedly altered` 报错
- 如果 Console 里再次出现 Cesium 包材质相关错误，可在 Unity 菜单执行 `AeroSim/Cesium/Sync Package Material Baseline`
- 这个自愈逻辑只修复当前已知的 Cesium 包材质漂移，不代表跨大版本或跨补丁版本可以随意混用

### 8. 修改前优先检查

在动任何资源前，先确认它属于哪一类：

- 飞机资源 -> `Assets/Aircraft/B737/...`
- 仪表资源 -> `Assets/Aircraft/B737/Instruments`
- 737 脚本 -> `Assets/Scripts/Aircraft/B737`
- 通用脚本 -> `Assets/Scripts/Camera` 或 `Assets/Scripts/World`
- 编辑器工具 -> `Assets/Scripts/Editor/B737`
- 外部桥接文件 -> `JSBSimBridge/`

### 9. 团队协作建议

- 每次提交只做一类改动
- 资源整理、脚本改动、材质调整不要混成一个提交
- 先修路径和引用，再做效果优化
- 改 prefab 或场景后，一定进入 Unity 检查是否有 Missing Script / Missing Reference

---

## JSBSim 相关约定

外部桥接文件统一放在：

- [JSBSimBridge](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge)

不要把这些文件散落进 Unity `Assets/` 目录里。
Unity 项目通过脚本和端口配置对接它们，仓库根目录只负责集中保存桥接配置与启动脚本。

---

## 当前主流程

1. 打开 [AeroSimUnity](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity)
2. 加载 [MainScene.unity](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Scenes\MainScene.unity)
3. 场景中主飞机实例来自 [B737.prefab](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Aircraft\B737\Prefabs\B737.prefab)
4. 如需外部飞行物理，使用 [start_jsbsim.bat](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\start_jsbsim.bat)

---

## 操作按键

### 飞行控制

| 按键 | 作用 |
| --- | --- |
| `W` | 升降舵前推 / 低头 |
| `S` | 升降舵后拉 / 抬头 |
| `A` | 副翼左滚 |
| `D` | 副翼右滚 |
| `Q` | 方向舵左偏航 |
| `E` | 方向舵右偏航 |
| `LeftShift` | 增加油门 |
| `LeftControl` | 减少油门 |
| `B` | 切换轮刹 |
| `G` | 起落架收放 |

### 襟翼

| 按键 | 作用 |
| --- | --- |
| `F` | 放下襟翼 |
| `V` | 收回襟翼 |

说明：
- 当前可视襟翼动画按本地键盘控制。
- 如果后续襟翼逻辑改为完全跟随 JSBSim 控制通道，需要同步更新本节说明。

### 相机切换

| 按键 | 作用 |
| --- | --- |
| `Shift + 7` | 客舱视角 |
| `Shift + 8` | 驾驶舱视角 |
| `Shift + 9` | 第三人称视角 |

建议：
- 如果 `LeftShift` 正在用于油门操作，可优先使用 `RightShift + 7/8/9` 切换相机。

### 驾驶舱 / 客舱视角控制

| 按键 | 作用 |
| --- | --- |
| `鼠标右键拖动` | 转动视角 |
| `方向键` | 前后左右移动视角 |
| `PageUp / PageDown` | 上下移动视角 |

### 第三人称视角控制

| 按键 | 作用 |
| --- | --- |
| `鼠标右键拖动` | 环绕飞机旋转视角 |
| `鼠标滚轮` | 缩放远近 |

### HUD

| 按键 | 作用 |
| --- | --- |
| `Tab` | 显示 / 隐藏 HUD |

---

## 快速上手

仓库地址：

```text
https://github.com/anymore-stay/AeroSim-737.git
```

### 第一步：准备环境

请先确认本机已安装：
- Unity 2022.3 LTS
- Git LFS
- 可正常运行 `.bat` 脚本
- JSBSim 1.3.x

### Git LFS 一键安装与拉取

本项目使用 Git LFS 管理 FBX、贴图、音频、视频、压缩包和文档等大文件。小组成员首次拉取仓库前，建议先安装 Git LFS，否则 Unity 里看到的资源可能只是几行文本指针。

Windows 推荐在 PowerShell 中执行下面这一条命令：

```powershell
winget install --id GitHub.GitLFS -e --source winget; git lfs install; git clone https://github.com/anymore-stay/AeroSim-737.git; cd AeroSim-737; git lfs pull
```

如果本机已经安装 Git LFS，也可以直接执行：

```powershell
git lfs install; git clone https://github.com/anymore-stay/AeroSim-737.git; cd AeroSim-737; git lfs pull
```

macOS 如果使用 Homebrew，可以执行：

```bash
brew install git-lfs && git lfs install && git clone https://github.com/anymore-stay/AeroSim-737.git && cd AeroSim-737 && git lfs pull
```

Linux 如果使用 Debian / Ubuntu，可以执行：

```bash
sudo apt update && sudo apt install -y git-lfs && git lfs install && git clone https://github.com/anymore-stay/AeroSim-737.git && cd AeroSim-737 && git lfs pull
```

如果仓库已经 clone 过，但当时没有安装 Git LFS，请在仓库根目录执行：

```powershell
git lfs install; git lfs pull
```

验证 Git LFS 是否可用：

```powershell
git lfs version
git lfs track
```

正常情况下，`git lfs track` 会列出 `.gitattributes` 中配置的 `*.fbx`、`*.png`、`*.jpg`、`*.dds`、`*.wav`、`*.mp4`、`*.zip` 等大文件规则。

如果看到资源文件内容类似下面这样，说明 LFS 资源还没真正拉下来：

```text
version https://git-lfs.github.com/spec/v1
oid sha256:...
size ...
```

解决方式是在仓库根目录重新执行：

```powershell
git lfs pull
```

### JSBSim 安装包位置

项目已经在下面这个目录提供了安装包：

- [Install](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\Install)
- [JSBSim-1.3.1-1837-setup.exe](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\Install\JSBSim-1.3.1-1837-setup.exe)

建议每位团队成员先安装 JSBSim，再继续下面的启动步骤。

### 第二步：打开 Unity 工程

用 Unity 2022.3 LTS 打开：

- [AeroSimUnity](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity)

首次打开时请等待 Unity 完成：
- Package 导入
- 脚本编译
- 资源重导入

首次打开时间较长属于正常现象。

### 第三步：打开主场景

主场景：

- [MainScene.unity](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Scenes\MainScene.unity)

同时确认场景中的主飞机实例来自：

- [B737.prefab](D:\OneDrive\Desktop\AeroSim-737\AeroSimUnity\Assets\Aircraft\B737\Prefabs\B737.prefab)

### 第四步：先安装并确认 JSBSim

如果本机还没有安装 JSBSim，请先使用项目内提供的安装包完成安装：

- [Install](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\Install)
- [JSBSim-1.3.1-1837-setup.exe](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\Install\JSBSim-1.3.1-1837-setup.exe)

安装完成后，再确认：
- `JSBSIM_DIR` 或 `DEFAULT_JSBSIM_DIR` 指向的目录下存在 `JSBSim.exe`
- 该安装目录的 `aircraft\737\` 下存在 `737.xml` 和 `unity_air.xml`

### 第五步：启动 JSBSim（推荐）

如果需要真实飞行物理和实时飞行数据，请在进入 Unity Play 前先运行：

- [start_jsbsim.bat](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\start_jsbsim.bat)

相关配置文件：
- [b737_unity.xml](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\b737_unity.xml)
- [unity_output.xml](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\unity_output.xml)

说明：
- 不启动 JSBSim 时，部分本地动画、相机和界面功能仍可测试。
- 启动 JSBSim 后，`JsbsimBridge` 会通过既定端口与 Unity 通信。

### JSBSim 安装路径不一致怎么办

团队里不同成员的机器上，JSBSim 安装路径可能不同。
当前 [start_jsbsim.bat](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\start_jsbsim.bat) 已支持两种方式：

1. 推荐方式：设置 `JSBSIM_DIR` 环境变量
   例如把它设成你机器上的 `JSBSim.exe` 所在目录。

2. 本地方式：直接修改 `start_jsbsim.bat` 里的 `DEFAULT_JSBSIM_DIR`
   只改你本机使用的路径，不影响 Unity 工程结构。

要求：
- `JSBSIM_DIR` 或 `DEFAULT_JSBSIM_DIR` 必须指向 **包含 `JSBSim.exe` 的目录**
- 不要把这个路径写成仓库里的相对路径伪装成本地安装目录

如果运行 `start_jsbsim.bat` 后提示：

```text
[ERROR] JSBSim.exe not found at: ...
```

优先检查：
- 是否已经安装 JSBSim
- 安装目录里是否真的存在 `JSBSim.exe`
- `JSBSIM_DIR` / `DEFAULT_JSBSIM_DIR` 是否指到了正确位置

### 第六步：进入 Play

点击 Unity 顶部 `Play` 后，建议按下面顺序检查：

1. 切到驾驶舱视角或第三人称视角
2. 确认 HUD 是否显示正常
3. 测试 `F / V` 襟翼
4. 测试 `G` 起落架
5. 测试 `W / S / A / D / Q / E` 飞行控制
6. 测试 `Shift + 7 / 8 / 9` 相机切换
7. 如果已启动 JSBSim，确认姿态、高度、空速与 HUD 数据是否持续更新

### 第七步：首次开发前建议检查

在正式开始改动前，建议先检查：
- Console 是否存在编译错误
- `B737.prefab` 是否有 Missing Script
- `MainScene` 中主飞机实例是否引用了正确 prefab
- 材质与贴图是否完整加载
- `JSBSimBridge` 目录中的外部文件是否齐全

### 常见情况

- 如果看不到 HUD：先按 `Tab`
- 如果相机切不动：确认是否按了 `Shift + 数字键`
- 如果只显示模型但没有飞行数据更新：确认是否已启动 `start_jsbsim.bat`
- 如果 `start_jsbsim.bat` 报找不到 `JSBSim.exe`：检查 `JSBSIM_DIR` 或 `DEFAULT_JSBSIM_DIR`
- 如果资源显示为文本指针：重新执行 `git lfs pull`

---

## 后续建议

- 先做一轮引用体检
- 再做一轮功能回归检查
- 最后再继续做材质、动画和交互细调

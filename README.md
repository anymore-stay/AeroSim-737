# AeroSim-737

基于 Unity 的 Boeing 737-800 飞行模拟项目。目标是把 X-Plane 11 的机模与机场地景转换为 Unity 可用资源，通过 JSBSim 物理引擎驱动真实飞行数据，最终实现一次完整的五边飞行。

当前机型：**Boeing 737-800**　|　当前机场：**北京大兴国际机场（ZBAD）**

---

## 当前状态

> **阶段：初始化完成，尚未导入任何资源**

Unity 工程已创建，目录结构已规划，URP 渲染管线已配置。Assets 下各目录骨架（`Aircrafts/B737-800/`、`Scenes/`、`Settings/`）已建立，但机模 FBX、材质、贴图、脚本均尚未添加。

已完成：
- Unity 2022.3 LTS 工程初始化，URP 渲染管线配置（Performant / Balanced / HighFidelity 三档预设）
- `Assets/` 目录结构规划：`Aircrafts/B737-800/{Model,Prefabs,Scripts,Instruments}/`
- `Main.unity` 主场景创建

待开始：
- X-Plane OBJ → Blender → FBX 转换，机模导入
- 机体各部件运动脚本（副翼、方向舵、升降舵、起落架等）
- JSBSim 接入与 UDP 通信
- Cesium for Unity 地景填充
- 涂装切换系统

---

## 核心设计

- **物理引擎分离**：JSBSim 作为独立进程在后台运行，通过 UDP 与 Unity 双向通信。Unity 只负责操作输入与视觉表现，不做飞行物理计算。
- **混合地景**：机场核心区使用 X-Plane OBJ 高清资源，外围由 Cesium for Unity 补全地形与影像。
- **Floating Origin**：解决大坐标浮点抖动，飞机偏离超过 1000m 时触发原点重置。
- **运行时涂装切换**：默认Air China涂装，支持运行中切换。

---

## 技术栈

| 类别 | 选型 | 状态 |
| --- | --- | --- |
| 游戏引擎 | Unity 2022.3.62f3c1 LTS | 已安装 |
| 渲染管线 | URP 14.0.12 | 已配置 |
| 物理引擎 | JSBSim（独立进程，UDP 通信） | 待接入 |
| 地景填充 | Cesium for Unity | 待安装 |
| 机模转换 | XPlane2Blender（Blender 插件） | 待使用 |

---

## 目录结构

```
AeroSim-737/
├── AeroSimUnity/                        # Unity 工程根目录
│   ├── Assets/                          # 所有项目资源（唯一允许手动编辑的目录）
│   │   ├── Aircrafts/                   # 飞机资源，按机型隔离
│   │   │   └── B737-800/
│   │   │       ├── Model/               # 静态资源，只读导入，禁止在 Unity 内手动修改
│   │   │       │   ├── FBX/             # 从 Blender 导出的 FBX 文件（由流水线生成，不手动编辑）
│   │   │       │   ├── Liveries/        # 各涂装贴图（DDS/PNG），命名规范：<航司>_<部位>.png
│   │   │       │   ├── Materials/       # URP Lit 材质，每套涂装对应一组材质，禁止共用
│   │   │       │   └── Textures/        # 公用基础贴图（AO、法线、金属度等）
│   │   │       ├── Prefabs/             # 飞机 Prefab，场景中只允许拖入 Prefab，禁止直接拖 FBX
│   │   │       ├── Scripts/             # 飞机相关 C# 脚本
│   │   │       │   ├── Movement/        # 各部件运动驱动（副翼、方向舵、起落架等）
│   │   │       │   └── Cameras/         # 座舱视角、外部跟随等摄像机控制
│   │   │       └── Instruments/         # 仪表系统（第二阶段开发，现阶段禁止写入）
│   │   ├── Environment/                 # 场景环境资源
│   │   │   └── Airport/                 # 机场地景：X-Plane OBJ 转换后的 FBX、材质、贴图
│   │   ├── Simulation/                  # 飞行仿真对接层
│   │   │   └── JSBSim/                  # JSBSim UDP 通信脚本与数据结构定义
│   │   ├── Scenes/
│   │   │   └── Main.unity               # 唯一主场景，所有开发在此场景进行
│   │   └── Settings/                    # URP 渲染管线配置（自动生成，禁止手动编辑）
│   │       ├── URP-Performant.asset      # 低配质量预设
│   │       ├── URP-Balanced.asset        # 中配质量预设（默认）
│   │       ├── URP-HighFidelity.asset    # 高配质量预设
│   │       └── UniversalRenderPipelineGlobalSettings.asset
│   ├── Packages/
│   │   └── manifest.json                # 包依赖清单，新增包在此声明，禁止直接改 packages-lock.json
│   ├── ProjectSettings/                 # Unity 工程设置（由 Unity 自动管理，禁止手动编辑）
│   └── Library/                         # Unity 本地缓存（已 .gitignore，禁止提交）
├── Docs/                                # 项目文档
│   ├── 开发文档.md                       # 开发过程记录、决策说明
│   └── superpowers/specs/               # 设计文档与技术规格
└── README.md                            # 项目总览（本文件）
```

---

## 开发规范

### 资源导入

- **FBX 只放 `Model/FBX/`**，不直接拖入场景，统一通过 `Prefabs/` 使用。
- 贴图文件统一放对应的 `Liveries/` 或 `Textures/` 目录，命名使用英文小写加下划线（如 `china_southern_fuselage.png`）。
- 新机型资源放 `Aircrafts/<机型>/`，禁止混放在 B737-800 目录下。

### 脚本

- 脚本文件名与类名保持一致，使用 PascalCase（如 `AileronController.cs`）。
- `Movement/` 只放部件运动逻辑，禁止在此写 UI 或通信代码。
- `Cameras/` 只放摄像机控制逻辑。
- JSBSim 通信相关代码统一放 `Simulation/JSBSim/`，不散落到其他目录。

### 场景

- **只有一个主场景 `Main.unity`**，禁止新建其他场景提交。
- 场景中的飞机只能是 Prefab 实例，禁止直接放 FBX 或裸 GameObject。
- 提交前必须保存场景（`Ctrl+S`），避免提交到一半的场景状态。

### Git 提交

- **禁止提交 `Library/`、`Temp/`、`obj/`、`UserSettings/`**，已在 `.gitignore` 中排除。
- **禁止提交 `*.csproj`、`*.sln`** 等 IDE 生成文件。
- 每次提交只做一件事，Commit Message 格式：`<类型>：<简述>`，例如：
  - `资源：导入 B737-800 机体 FBX`
  - `脚本：实现副翼运动驱动`
  - `修复：起落架动画权重错误`
- 大体积资源（FBX、贴图）通过 Git LFS 管理，拉取前执行 `git lfs pull`。

### 禁止改动的内容

| 路径 | 原因 |
| --- | --- |
| `Assets/Settings/` | URP 管线配置，改动影响全局渲染 |
| `ProjectSettings/` | Unity 工程设置，由 Unity 自动维护 |
| `Packages/packages-lock.json` | 包锁定文件，只通过 Unity Package Manager 更新 |
| `Assets/Aircrafts/B737-800/Instruments/` | 第二阶段才开发，现阶段禁止写入 |

---

## 开发路线图

| 阶段 | 目标 | 状态 |
| --- | --- | --- |
| 第一步（当前） | 机模与地景导入，各部件按数据正确运动，涂装可切换 | 进行中 |
| 第二步 | JSBSim 完整接入 + 玩家操作 + 完整五边飞行 | 未开始 |
| 后续 | 仪表系统、音效系统 | 未开始 |

第一步不含仪表系统，使用键盘/测试数据验证部件运动。

---

## 环境要求

| 依赖 | 说明 |
| --- | --- |
| Unity 2022.3.62f3c1 LTS | 需启用 URP |
| Blender + XPlane2Blender | 导入 X-Plane OBJ，导出 FBX |
| JSBSim | 飞行物理计算，后台独立运行 |
| Cesium Ion 账号 | Cesium for Unity 拉取地形与影像数据所需 |
| Git LFS | 拉取大体积资源前需安装 |

---

## 快速上手

```bash
git clone <仓库地址>
cd AeroSim-737
git lfs pull
```

用 Unity 2022.3.62f3c1 LTS 打开 `AeroSimUnity/` 工程，加载 `Assets/Scenes/Main.unity`。

> 若资源文件显示为文本指针，执行 `git lfs pull` 即可。

# CLAUDE.md — AeroSim-737

## 项目概述

Boeing 737-800 飞行模拟器。Unity 负责视觉与操作输入，JSBSim 作为独立进程通过 UDP 提供飞行物理数据。目标是完成一次完整的五边飞行（起飞、爬升、巡航、进近、着陆）。

**当前阶段**：从 Built-in 渲染管线旧项目（`AeroSim-737-pre`）迁移到 URP 新项目（`AeroSim-737`）。旧项目路径：`D:\OneDrive\Desktop\AeroSim-737-pre`，新项目路径：`D:\OneDrive\Desktop\AeroSim-737`。

---

## 工程结构

Unity 工程在子目录 `AeroSimUnity/`，**不在项目根目录**。所有 Unity 相关操作的路径基准是 `AeroSimUnity/Assets/`。

```
AeroSim-737/
├── AeroSimUnity/              ← Unity 工程根，用 Unity 打开这个目录
│   ├── Assets/
│   │   ├── Aircrafts/B737-800/
│   │   │   ├── Model/FBX/     ← Blender 导出的 FBX，只放不改
│   │   │   ├── Model/Liveries/ ← 涂装贴图，命名：<航司>_<部位>.png
│   │   │   ├── Model/Materials/ ← URP Lit 材质，每套涂装独立一组
│   │   │   ├── Model/Textures/ ← 公用基础贴图（AO、法线、金属度）
│   │   │   ├── Prefabs/       ← 飞机 Prefab，场景只拖 Prefab
│   │   │   ├── Scripts/Movement/ ← 部件运动脚本（副翼、舵面、起落架等）
│   │   │   ├── Scripts/Cameras/  ← 摄像机控制脚本
│   │   │   └── Instruments/   ← 仪表系统，第二阶段开发，现阶段禁止写入
│   │   ├── Environment/Airport/ ← 机场地景资源
│   │   ├── Simulation/JSBSim/   ← JSBSim UDP 通信脚本与数据结构
│   │   ├── Scenes/Main.unity    ← 唯一主场景
│   │   └── Settings/            ← URP 配置，禁止手动编辑
│   └── Packages/manifest.json   ← 包依赖，只在这里增删包
└── AeroSim-737-pre/           ← 旧 Built-in 项目，只读参考，禁止修改
```

---

## 旧项目参考（AeroSim-737-pre）

迁移时从这里读取脚本和资源，**只读，不写**。

| 旧路径 | 内容 | 迁移目标 |
| --- | --- | --- |
| `Boeing_737_800/Assets/Scripts/` | 运行时脚本 | `Aircrafts/B737-800/Scripts/` |
| `Boeing_737_800/Assets/Editor/` | Editor 工具脚本 | `Editor/`（工程根级） |
| `Boeing_737_800/Assets/Prefabs/` | 飞机 Prefab | `Aircrafts/B737-800/Prefabs/` |
| `Boeing_737_800/Assets/B737_Liveries/` | 涂装资源 | `Aircrafts/B737-800/Model/Liveries/` |

旧项目使用 **Built-in 渲染管线**，迁移脚本时需将材质/着色器引用从 Standard Shader 改为 URP Lit。

### 旧项目现有脚本清单

| 文件 | 功能 |
| --- | --- |
| `B737YokeController.cs` | 操纵轮 Pitch/Roll 控制，虚拟 Pivot Anchor 方案 |
| `ElevatorControl.cs` | 左右升降舵铰链同步旋转，AnimationCurve 映射 |
| `CockpitCameraController.cs` | 驾驶舱/客舱/第三人称三合一相机控制器 |
| `CameraManager.cs` | 多相机管理，Shift+数字键切换 |
| `B737SceneMaterialReplacer.cs` | Editor 工具：批量替换场景材质 |
| `AircraftPrefabFbxExportUtility.cs` | Editor 工具：Prefab 导出 FBX |
| `AircraftPrefabIsolationUtility.cs` | Editor 工具：Prefab 隔离查看 |

---

## 技术约定

### 渲染管线
新项目使用 **URP 14.0.12**，Shader 统一用 `Universal Render Pipeline/Lit`。迁移脚本时如遇 `Standard` 材质引用，改为 URP Lit。三档质量预设：Performant（低）/ Balanced（默认）/ HighFidelity（高）。

### 脚本编写规范
- 类名与文件名一致，PascalCase
- `Movement/` 只放部件运动逻辑，不写 UI 和通信代码
- `Cameras/` 只放摄像机控制逻辑
- `Simulation/JSBSim/` 放所有 UDP 通信和数据结构
- 注释用中文，公共 API 写简短说明即可，私有实现逻辑只在非显然时注释

### 场景规范
- 只有一个主场景 `Main.unity`
- 飞机只能以 Prefab 实例形式存在于场景，禁止直接拖 FBX 或裸 GameObject
- 提交前必须保存场景（`Ctrl+S`）

### Floating Origin
飞机偏离原点超过 1000m 时触发原点重置，解决大坐标浮点抖动。实现脚本放 `Simulation/` 目录。

### 涂装系统
支持运行时切换涂装，每套涂装对应 `Liveries/` 下独立一组贴图 + `Materials/` 下独立一组材质，禁止多套涂装共用材质实例。

---

## 禁止改动

| 路径 | 原因 |
| --- | --- |
| `AeroSimUnity/Assets/Settings/` | URP 管线配置，影响全局渲染 |
| `AeroSimUnity/ProjectSettings/` | Unity 工程设置，由 Unity 自动维护 |
| `AeroSimUnity/Packages/packages-lock.json` | 只通过 Package Manager 更新 |
| `Assets/Aircrafts/B737-800/Instruments/` | 第二阶段才开发 |
| `AeroSim-737-pre/` 整个目录 | 只读参考，不写入 |

---

## Git 规范

- `Library/`、`Temp/`、`obj/`、`UserSettings/` 已在 `.gitignore` 排除，禁止提交
- Commit Message 格式：`<类型>：<简述>`，类型为：资源 / 脚本 / 修复 / 文档 / 配置
- 大体积资源通过 Git LFS 管理，拉取前执行 `git lfs pull`
- 推送需要代理，git 走 `gh auth setup-git` 配置的凭据助手

---

## 开发路线图

| 阶段 | 目标 | 状态 |
| --- | --- | --- |
| 第一步（当前） | 机模与地景迁移到 URP，各部件运动正确，涂装可切换 | 进行中 |
| 第二步 | JSBSim 完整接入 + 玩家操作 + 完整五边飞行 | 未开始 |
| 后续 | 仪表系统、音效系统 | 未开始 |

第一步不含仪表系统，用键盘/测试数据验证部件运动。

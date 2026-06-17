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
├── AeroSimUnity/                        # Unity 工程
│   ├── Assets/
│   │   ├── Aircrafts/B737-800/
│   │   │   ├── Model/{FBX,Liveries,Materials,Textures}/
│   │   │   ├── Prefabs/
│   │   │   ├── Scripts/{Movement,Cameras}/
│   │   │   └── Instruments/
│   │   ├── Scenes/Main.unity
│   │   └── Settings/                    # URP 渲染管线配置
│   └── Packages/manifest.json
└── README.md
```

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

# UniSky 设计规格文档：X-Plane 资源转 Unity（第一步）

- 日期：2026/06/16
- 状态：已确认
- 适用范围：第一步（机模与地景导入、部件运动、涂装切换），不含仪表系统

## 1. 项目目标

UniSky 是一个基于 Unity 的模拟飞行项目。最终目标是完成一次完整的五边飞行（起飞、爬升、巡航、进近、着陆），飞机各组件的运动与真实飞机及 X-Plane 11 中的呈现一致，飞行数据来自 JSBSim 物理引擎。

本规格只覆盖**第一步**：把 X-Plane 11 的 Boeing 737-800 机模和北京大兴机场（ZBAD）地景转换为 Unity 可编辑资源，使各部件能按数据正确运动、有动画，地景显示正常，涂装可切换。第一步不接入 JSBSim 完整飞行，用键盘/测试数据验证部件运动。

## 2. 已确认的关键决策

| 方面 | 决策 | 理由 |
|------|------|------|
| 机模导入 | XPlane2Blender 插件导入 X-Plane OBJ，保留 ANIM 动画数据，导出 FBX | X-Plane OBJ 的 `ANIM_rotate`/`ANIM_trans` 指令记录了部件旋转轴、平移范围和对应 dataref，导入后变成 Blender Action 关键帧，导出 FBX 一并带走，省去手工还原运动逻辑 |
| 运动逻辑 | 解析 ANIM 的 dataref 语义，映射到数据源驱动 | dataref 即运动逻辑的语义来源 |
| JSBSim 接入 | UDP Socket，JSBSim 独立进程后台运行 | 业界主流（参考微软 AirSim），避免编译 DLL 的跨平台与 C++/C# 互操作麻烦 |
| 地景策略 | 机场核心区用 X-Plane OBJ 资源，资源没有的区域用 Cesium 兜底 | X-Plane 地景包细节好看，清晰度高于卫星底图 |
| 坐标 | 机场对齐到合理本地原点，不强求真实经纬度 | 降低复杂度 |
| 精度方案 | Floating Origin，超 1000m 触发原点重置 | 消除大坐标浮点抖动 |
| 涂装 | 运行时替换贴图材质，默认中国南方航空 | 资源包已含多套涂装 |

## 3. 整体架构

```
┌─────────────────────────────────────────────────┐
│                   Unity 主场景                   │
│                                                  │
│  ┌─────────────────────────────────────────┐    │
│  │              飞机系统                    │    │
│  │  键盘/鼠标 → 输入管理器                  │    │
│  │       │                                 │    │
│  │       ▼                                 │    │
│  │  数据映射层 DataMapper                   │    │
│  │       │                                 │    │
│  │       ▼                                 │    │
│  │  部件驱动层（动画 / Transform）          │    │
│  └─────────────────────────────────────────┘    │
│                                                  │
│  ┌──────────┐  ┌──────────┐                     │
│  │  地景系统 │  │  相机系统 │                     │
│  └──────────┘  └──────────┘                     │
│                                                  │
│  ┌──────────────────────────────────────────┐   │
│  │          Floating Origin Manager         │   │
│  │        （全局，所有子系统共用）           │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
         ▲  控制指令（UDP）    结果数据（UDP）  ▲
         └──────────────────────────────────────┘
                    JSBSim.exe（后台进程，第二步接入）
```

五个子系统：飞机系统、地景系统、相机系统（第一步实现），仪表系统（暂跳过）、音效系统（后续）。

数据流：Unity 里键盘/鼠标操作 → 通过 UDP 发控制指令给后台 JSBSim → JSBSim 计算物理 → 通过 UDP 返回结果数据 → Unity 驱动部件动画。JSBSim 是唯一数据源，在后台独立进程运行；玩家输入只是发给 JSBSim 的控制指令，不直接控制部件。

## 4. 数据映射层（关键设计）

X-Plane OBJ 的 ANIM 动画指令由 `sim/flightmodel2/*` 系列 dataref 驱动，这是 X-Plane 的**部件显示数据**（表示部件当前应显示的状态）。而项目的数据源是 **JSBSim**，字段为 `fcs/*`、`propulsion/*`、`gear/*`。两套命名体系不同，因此架构上必须有一个**数据映射层（DataMapper）**：

```
JSBSim 输出（fcs/elevator-pos-deg 等）
      │
      ▼
DataMapper（把 JSBSim 字段换算/映射为 X-Plane flightmodel2 语义值）
      │
      ▼
部件驱动层（按映射值驱动 FBX 动画曲线 / Transform）
```

第一步阶段（不接 JSBSim）时，用键盘测试值直接喂给映射层下游验证部件动作。这样第二步接入 JSBSim 时，只需补齐 DataMapper 的 JSBSim 输入端，下游部件驱动无需改动。

## 5. 机模部件 Prefab 结构

```
B737_800.prefab（根节点）
├── Body（机身）
├── Wings（机翼）
├── Engines/
│   ├── Engine_L（左发，风扇旋转动画）
│   └── Engine_R
├── ControlSurfaces/
│   ├── Rudder（方向舵）
│   ├── Elevator_L / Elevator_R（升降舵）
│   ├── Aileron_L / Aileron_R（副翼）
│   ├── Flap_L / Flap_R（襟翼）
│   ├── Slat_*（前缘缝翼）
│   └── Spoiler_L / Spoiler_R（扰流板/减速板）
├── LandingGear/
│   ├── NoseGear（前起，收放 + 转向 + 滚转）
│   └── MainGear_L / MainGear_R（收放 + 滚转 + 减震）
├── Cockpit（驾驶舱，不含仪表）
│   ├── Yoke_L / Yoke_R（操纵杆）
│   └── RudderPedals（方向舵踏板）
└── Livery（涂装层，材质可替换）
```

## 6. 部件 → X-Plane dataref 映射（从 OBJ 实测）

| 部件 | X-Plane dataref | 说明 |
|------|------|------|
| 副翼 | `sim/flightmodel2/wing/aileron1_deg[4][5]` | 左右副翼，单位度 |
| 襟翼 | `sim/flightmodel2/wing/flap1_deg[N]`、`flap2_deg[N]` | 多段襟翼，单位度 |
| 升降舵 | `sim/flightmodel2/wing/elevator1_deg[8][9]` | 单位度 |
| 水平安定面 | `sim/flightmodel2/controls/stabilizer_deflection_degrees` | 单位度 |
| 扰流板/减速板 | `sim/flightmodel2/wing/speedbrake1_deg[N]` | 单位度 |
| 前缘缝翼 | `sim/flightmodel2/controls/slat1_deploy_ratio`、`slat2_deploy_ratio` | 0~1 比例 |
| 起落架收放 | `sim/flightmodel2/gear/deploy_ratio[0/1/2]` | [0] 前轮、[1][2] 主轮，0~1 |
| 轮胎转动 | `sim/flightmodel2/gear/tire_rotation_angle_deg[N]` | 单位度，随地速滚转 |
| 前轮转向 | `sim/flightmodel2/gear/tire_steer_actual_deg[0]` | 单位度 |
| 轮胎减震 | `sim/flightmodel2/gear/tire_vertical_deflection_mtr[N]` | 单位米 |
| 发动机风扇转角 | `sim/flightmodel2/engines/engine_rotation_angle_deg[0/1]` | 单位度 |
| 发动机风扇转速 | `sim/flightmodel2/engines/engine_rotation_speed_rad_sec[0/1]` | 弧度/秒 |
| 反推 | `sim/flightmodel2/engines/thrust_reverser_deploy_ratio[0/1]` | 0~1 |
| 各类舱门 | `sim/flightmodel2/misc/door_open_ratio` | 0~1 |

注：方向舵 dataref（预期 `sim/flightmodel2/wing/rudder1_deg`）在已抽样 OBJ 中未直接出现，待在其它分件 OBJ 核实。驾驶舱操纵杆/踏板用 `sim/cockpit2/*` 系列，第二步细化。运动角度范围为草案，边做边对照 X-Plane 实机校准。

## 7. JSBSim → X-Plane dataref 换算（草案）

| JSBSim 字段 | 对应 X-Plane dataref | 换算 |
|------|------|------|
| `fcs/left-aileron-pos-deg` | `wing/aileron1_deg` | 直接取角度 |
| `fcs/elevator-pos-deg` | `wing/elevator1_deg` | 直接取角度 |
| `fcs/flap-pos-deg` | `wing/flap1_deg` | 直接取角度 |
| `fcs/speedbrake-pos-norm` | `wing/speedbrake1_deg` | norm × 最大角 |
| `gear/gear-pos-norm` | `gear/deploy_ratio` | 直接 0~1 |
| `propulsion/engine[N]/n1` | `engines/engine_rotation_speed_rad_sec` | n1% → 转速换算 |

换算细节边做边完善。

## 8. 输入键位（草案）

| 操作 | 发送给 JSBSim 的控制指令 |
|------|-------------|
| 鼠标 X 轴（激活后） | `fcs/aileron-cmd-norm` |
| 鼠标 Y 轴（激活后） | `fcs/elevator-cmd-norm` |
| A / D | `fcs/rudder-cmd-norm` |
| F / G | `fcs/flap-cmd-norm`（收/放一档） |
| 上 / 下方向键 | `fcs/throttle-cmd-norm` |
| 右键单击 | 激活/取消鼠标控制操纵杆 |
| G | `gear/gear-cmd-norm`（收放起落架） |

键位边做边完善。

## 9. 地景系统

```
机场核心区（X-Plane OBJ 资源）          外围/远景（Cesium）
  跑道、滑行道、停机坪、地面标线           地形高程
  航站楼、塔台、主要建筑                   城市建筑、远景填充
        │                                    │
        └──────────────┬─────────────────────┘
                       ▼
              统一本地坐标系（机场原点对齐）
                       ▼
              Floating Origin 偏移
```

策略：机场资源包里有的全用 OBJ 资源（同样经 Blender 转 FBX 导入），资源里没有的区域由 Cesium for Unity 兜底。

## 10. 相机系统

第一步提供：外部环绕视角（观察部件运动）、追尾视角、跑道侧拍视角。驾驶舱视角因不含仪表，第一步以外部视角为主。

## 11. Floating Origin

全局单例，监控飞机距世界原点距离，超 1000m 时把场景所有根节点（地景、飞机、Cesium）整体平移回原点，飞机相对位置不变，消除远距离浮点抖动。地景和 Cesium 都需注册到该管理器。参考 https://manuel-rauber.com/2022/04/06/floating-origin-in-unity/

## 12. 第一步路线图

1. Blender 环境 + XPlane2Blender 插件配置，转换 B737 模型
2. 模型导入 Unity，按部件拆分 Prefab
3. 部件驱动脚本（先用键盘测试数据驱动，验证每个部件能动）
4. 涂装切换系统（默认南航）
5. 地景 OBJ 转换导入 + Cesium 接入
6. Floating Origin 接入
7. 相机系统
8. 集成验证：场景能跑、部件能动、地景正常、涂装能换

第二步（不在本规格范围）：JSBSim 完整接入 + 玩家操作 + 五边飞行。后续：仪表系统、音效系统。

## 13. 坐标系与单位注意事项

- X-Plane OBJ 为 OBJ8 格式（头部 `A 800 OBJ`），单位为米，右手系、Z 轴朝后。
- Unity 为左手系、Y 轴朝上。导入需做坐标系转换（通常 Blender 导入导出环节处理）。
- 大文件（FBX、OBJ、贴图）已配置 Git LFS。

## 14. 验收标准（第一步）

- B737-800 模型在 Unity 中正确显示，无明显破面、贴图错位。
- 各可动部件（方向舵、升降舵、副翼、襟翼、扰流板、起落架、前轮转向、发动机风扇等）能按输入数据正确运动，运动方向和范围与 X-Plane 11 基本一致。
- 大兴机场地景在 Unity 中正确显示，机场核心区用 X-Plane 资源，外围由 Cesium 填充。
- 涂装可在运行时切换，默认中国南方航空。
- 大范围移动时飞机部件无浮点抖动（Floating Origin 生效）。

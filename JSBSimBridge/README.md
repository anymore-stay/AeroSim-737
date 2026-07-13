# Boeing 737 + JSBSim + Unity 实时飞行仿真

用 JSBSim 做飞行动力学解算,通过 socket 实时驱动 Unity 里的 Boeing 737 模型。
JSBSim 算物理,Unity 只负责可视化和接收键盘操控。

---

## 1. 架构总览

```
   键盘输入                                    UDP 5501 (状态)
  ┌─────────┐   TCP 5502 (控制命令)         ┌──────────────────┐
  │  Unity  │ ───────────────────────────▶  │     JSBSim       │
  │ B737模型│                                │  (飞行动力学解算) │
  │         │ ◀───────────────────────────  │                  │
  └─────────┘    飞机状态(经纬度/姿态/速度)  └──────────────────┘
```

- **JSBSim → Unity(UDP 5501)**:JSBSim 每秒 60 次把飞机状态(经纬度、海拔、姿态角、速度)以 CSV 文本发到本机 5501 端口。Unity 端 `JsbsimBridge` 接收并驱动 B737 的位置和姿态。
- **Unity → JSBSim(TCP 5502)**:Unity 把键盘操控转成 telnet 风格命令(如 `set fcs/aileron-cmd-norm 0.5\n`)发给 JSBSim。
- UDP 是无连接的,Unity 不在线也不会卡住 JSBSim;TCP 用于可靠下发控制。

---

## 2. 环境与文件

### 需要的软件
- Unity(项目当前使用的版本)
- JSBSim 1.3.x

### 安装包

安装包已放在：

- [Install](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\Install)
- [JSBSim-1.3.1-1837-setup.exe](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\Install\JSBSim-1.3.1-1837-setup.exe)

建议先安装，再确认 `JSBSim.exe` 的实际安装目录。

### 安装路径说明

团队成员机器的 JSBSim 安装路径可以不同。
当前 [start_jsbsim.bat](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\start_jsbsim.bat) 已支持：

1. 通过环境变量 `JSBSIM_DIR` 指定安装目录
2. 或直接修改脚本里的 `DEFAULT_JSBSIM_DIR`

要求：
- `JSBSIM_DIR` 或 `DEFAULT_JSBSIM_DIR` 必须指向包含 `JSBSim.exe` 的目录
- 如果脚本提示找不到 `JSBSim.exe`，优先检查这里

### 关键文件清单

| 文件 | 作用 |
|------|------|
| `<JSBSIM_DIR>\\aircraft\\737\\737.xml` | 波音 737 飞行动力学模型(JSBSim 自带，Dave Culp/Aeromatic，GPL，仅供学习娱乐） |
| `<JSBSIM_DIR>\\aircraft\\737\\unity_air.xml` | 自定义初始状态：5000ft、约296kt 低空巡航起步 |
| `JSBSimBridge\b737_unity.xml` | JSBSim 运行脚本：用 737 模型、启动双发、配平、开 TCP 5502 控制口 |
| `JSBSimBridge\unity_output.xml` | UDP 输出配置：定义往 5501 发哪些属性 |
| `JSBSimBridge\start_jsbsim.bat` | 一键启动 JSBSim 的批处理 |
| `AeroSimUnity\Assets\Scripts\Aircraft\B737\JsbsimBridge.cs` | Unity 端核心：收 UDP 状态驱动飞机、发 TCP 控制 |
| `AeroSimUnity\Assets\Scripts\Aircraft\B737\FlightInput.cs` | 键盘 → 控制量映射 |
| `AeroSimUnity\Assets\Scripts\Aircraft\B737\FlightHud.cs` | 屏幕 HUD 显示高度/速度/姿态 |
| `AeroSimUnity\Assets\Scripts\Camera\CockpitCameraController.cs` | 驾驶舱/客舱/第三人称相机控制 |
| `AeroSimUnity\Assets\Scripts\Camera\CameraManager.cs` | 相机切换管理（Shift+数字键） |

---

## 3. 场景结构

```
MainScene
├── Directional Light
├── Terrain
├── B737                                   ← 飞机本体(顶层)
│   │   组件: CameraManager, JsbsimBridge, FlightInput, FlightHud
│   ├── CockpitCamera     (Shift+8, 默认)
│   ├── CabinCamera       (Shift+7)
│   ├── ThirdPersonCamera (Shift+9)
│   └── ... (机体模型子物体)
└── ChaseCamera   ← 已禁用(保留在场景但 inactive)
```

**关键点：JSBSim 直接驱动 B737 本体，不再用额外的空容器。**
`JsbsimBridge`/`FlightInput`/`FlightHud` 三个脚本直接挂在飞机本体上，
`JsbsimBridge.aircraft` 指向飞机自己的 Transform（留空也会自动指向本物体）。

---

## 4. 复现步骤

### 4.1 准备
1. 如果本机还没有安装 JSBSim，先运行：
   - [Install\JSBSim-1.3.1-1837-setup.exe](D:\OneDrive\Desktop\AeroSim-737\JSBSimBridge\Install\JSBSim-1.3.1-1837-setup.exe)
2. 确认 JSBSim 已安装，且 `JSBSIM_DIR` 或 `DEFAULT_JSBSIM_DIR` 指向的目录下有 `JSBSim.exe`。
3. 确认该安装目录的 `aircraft\737\` 下有 `737.xml` 和 `unity_air.xml`。
4. 确认 `JSBSimBridge\` 下有 `b737_unity.xml`、`unity_output.xml`、`start_jsbsim.bat`。
5. 在 Unity 打开 `MainScene`。

### 4.2 检查 Inspector（首次复现时核对）
选中 `B737`：
- `JsbsimBridge`：stateUdpPort=5501，controlTcpPort=5502，controlHost=127.0.0.1，
  aircraft=飞机自己，smoothing=15，altitudeOffset=0
- `CameraManager`：三个槽位 Shift+7/8/9 分别对应 Cabin/Cockpit/ThirdPerson，defaultSlotIndex=1（驾驶舱）
确认 `ChaseCamera` 处于 **inactive**（场景里灰色）。

### 4.3 启动（顺序很重要）
1. **先点 Unity ▶ Play**（Unity 先监听 UDP 5501、并开始尝试连 TCP 5502）。
2. **再双击 `JSBSimBridge\start_jsbsim.bat`**（启动 JSBSim，开始解算并发数据）。
3. 约 1 秒后飞机完成配平，进入约 5000ft 平飞。Unity 里飞机开始飞行。

> 顺序反了一般也能连上（TCP 每秒重连一次），但推荐先 Play。

### 4.4 操控按键

| 按键 | 作用 |
|------|------|
| `W` / `S` | 升降舵：W 推杆低头，S 拉杆抬头 |
| `A` / `D` | 副翼：A 左滚，D 右滚 |
| `Q` / `E` | 方向舵：Q 左偏航，E 右偏航 |
| `Shift` | 增加正推油门 |
| `Ctrl` | 将油门收向怠速 |
| `Ctrl` + `Shift` | 接地后增加反推，HUD 油门显示为负数 |
| `F` | 放下襟翼 |
| `V` | 收回襟翼 |
| **方向键 ↑↓←→** | **相机视角移动**（不控制飞机） |
| **鼠标右键(按住)** | **相机转视角** |
| **鼠标滚轮** | 第三人称相机缩放距离 |
| `Shift`+`7/8/9` | 切换 客舱 / 驾驶舱 / 第三人称 相机 |

### 4.5 停止
关闭 JSBSim 的命令行窗口（或 bat 窗口），再停 Unity Play。

---

## 5. 踩过的坑（重要，复现/排错必看）

下面每一条都是实际遇到并修复过的问题。换机型、改配置时很容易再次踩中。

### 坑 1：飞机完全不动，但 socket 显示已连接 ★最隐蔽★
**现象**：Unity Console 显示 "UDP 状态接收已启动" 和 "控制通道已连接"，
JSBSim 也在正常跑（elapsed time 在涨），但飞机 Transform 停在原点不动。

**根因**：`unity_output.xml` 里输出了一个 **当前机型不存在的属性**。
最初这里写的是 `propulsion/engine/propeller-rpm`（螺旋桨转速）——
**737 是涡扇喷气机，没有螺旋桨**，JSBSim 会打印警告 "No property ... has been defined.
This property will not be logged." 并**跳过该列**。

于是实际发出的 CSV 列数和 `<LABELS>` 声明的列数对不上，
`JsbsimBridge.ParsePacket` 里 `vals.Length != labels.Length` 判断不通过，
**整包数据被丢弃**，飞机收不到任何状态。

**修复**：把该属性换成机型实际拥有的。737 用 N1 转速：
```xml
<property caption="rpm"> propulsion/engine[0]/n1 </property>
```

**排错方法**：临时把 `JsbsimBridge.logState` 勾上，看 Console 有没有
`[JSBSim] alt=... spd=...` 日志。没有 → 数据没进来或被丢；
再去 JSBSim 命令行窗口看有没有 "will not be logged" 警告。

> **通用教训**：换任何机型，都要核对 `unity_output.xml` 里每个属性在该机型存在。
> 活塞机有 `propeller-rpm`、`mixture` 等，喷气机有 `n1`/`n2`、没有混合比。

### 坑 2：初始化文件名必须和 initialize 属性完全对应
**现象**：JSBSim 启动即 `FATAL ERROR ... could not be read`。

**根因**：脚本里 `<use aircraft="737" initialize="unity_air"/>`，
JSBSim 会去找 `aircraft/737/unity_air.xml`。
我一开始把文件命名为 `unity_air_init.xml`，名字对不上就报错。

**修复**：初始化文件名 = initialize 的值 + `.xml`，即 `unity_air.xml`。

### 坑 3：相机右键转视角 / 方向键移动「没反应」
**现象**：相机挂在高速飞行的飞机下，按右键拖动、按方向键，视角纹丝不动。

**根因**：相机原本用 **运动学刚体的 `rb.MovePosition` / `rb.MoveRotation`**
（物理世界坐标指令）来移动。但相机是高速移动飞机的子物体（同坑 4 的刚体问题），
刚体的世界坐标目标和父子变换打架，输入量被淹没，看起来就是没反应。

**修复**（`CockpitCameraController.cs`）：
- Cockpit/Cabin 改用 **transform 本地坐标**（`localPosition`/`localRotation`），
  在机体坐标系内移动和转头，天然跟随飞机。
- ThirdPerson 改用直接设世界坐标 `transform.position/rotation`，并**每帧跟随**。
- 移动逻辑从 `FixedUpdate` 移到 `Update`，更跟手。

> **通用教训**：作为高速移动物体子节点的相机，不要用刚体物理移动，
> 直接操作 Transform（本地或世界坐标），避免和父子变换冲突。

### 坑 4：相机从飞机上「漂移甩出」、离飞机几千米看不见 ★最关键★
**现象**：一启动 JSBSim 驱动飞机，挂在飞机上的相机就被甩到几千米外，
完全看不见飞机。静止（没启动 JSBSim）时相机还好好贴在飞机上。

**根因**：相机被强制挂了**运动学 Rigidbody**（`CockpitCameraController`
原来在 `EnsurePhysicsComponents` 里 `AddComponent<Rigidbody>()`）。
这些相机是飞机的子物体，而 `JsbsimBridge` 用 `transform.position = ...`
（Transform 直接赋值）高速驱动飞机。**运动学刚体的世界位姿由 PhysX 接管，
不会跟随父物体的 Transform 直接赋值**，于是飞机飞走、相机被 PhysX 锁在原来的
世界坐标，本地坐标被反算成几千米的漂移值。

> 验证方法：Play + 启动 JSBSim 后，读激活相机的 **localPosition**。
> 正常应固定在初始值（如驾驶舱相机约 `(3.5, 3.2, -29.9)`）；
> 若漂成几千米（如 `(-6733, 2124, 3704)`），就是这个坑。

**修复**（`CockpitCameraController.cs`）：**彻底移除相机上的 Rigidbody 和 SphereCollider**。
`EnsurePhysicsComponents` 改为主动 `Destroy` 掉这两个组件，相机完全用纯 Transform 控制。

> **通用教训**：作为「被 Transform 直接赋值驱动」的高速物体的子节点，相机绝不能挂 Rigidbody。
> 运动学刚体会让 PhysX 接管世界位姿，与父物体的 Transform 驱动冲突，相机必被甩开。
> 这一条比"调刚体参数"更根本——直接不要刚体。

### 坑 5：第三人称相机被飞机甩开
**现象**：切到第三人称，没有任何输入时，相机被高速飞机落下。

**根因**：`ApplyThirdPersonOrbit` 开头有 `if (!orbitDirty) return;`——
没有鼠标输入时 orbitDirty=false 就跳过更新，可飞机一直在动，相机没跟上。

**修复**：去掉这个 early-return，让第三人称相机**每帧重新计算**相对飞机的位置。

### 坑 6：独立追逐相机和自带相机体系冲突
**现象**：场景里同时有两个激活的相机，画面/AudioListener 打架；
"只能切换三个相机"，多出来的那个不受 Shift 数字键管理。

**根因**：曾加过一个独立的 `ChaseCamera`（带 `ChaseCameraFollow`），
它不在 `CameraManager` 管理范围内却默认 enabled，和自带三相机争抢。

**修复**：把 `ChaseCamera` 整个物体设为 inactive，
相机体系统一交给 `CameraManager`（Shift+7/8/9）。

> 附带提醒：追逐相机若对位置做指数平滑，在喷气机这种 150m/s 高速下会**追不上、被越拉越远**。
> 如果将来要重新启用追逐相机，位置应当硬跟随（零滞后），平滑只用于朝向。

### 坑 7：控制方向反了
**现象**：上下、左右操控和预期相反。

**根因**：JSBSim 舵面命令的正负号约定与直觉相反（如 elevator 正为低头）。

**修复**（`FlightInput.cs`）：对 elevator 和 aileron 的输入取反，
使 W=低头、S=抬头、A=左滚、D=右滚。换机型若手感再次相反，调这里的符号即可。

### 坑 8：方向键被飞机和相机抢用
**现象**：方向键既想控制飞机又想控制相机，冲突。

**修复**：`FlightInput.cs` 里**移除方向键对飞机的绑定**，
飞机只用 WASD/QE；方向键专门留给相机视角移动。

### 坑 9：飞行中飞机模型在抖动（发光贴图尤其明显）
**现象**：飞机飞起来后，在驾驶舱内近距离看，飞机模型表面在高频抖动/闪烁，
发光贴图（高对比度表面）最明显。相机贴在飞机上却看到飞机自身在抖。

**根因**：**Unity 大世界坐标下的 float32 渲染精度不足**（floating origin 问题）。
关键判断依据：相机是飞机的子物体、刚性绑定，驾驶舱看飞机自身几何本不该有任何
相对运动；既然舱内还在抖，就只能是 GPU 顶点变换的浮点精度问题，而非平滑或帧率。

原因链：
- JSBSim 用真实经纬度驱动，飞机几十秒就飞到离世界原点上万米（实测 20 秒约 3 公里且持续累积）。
- Unity 位置是 32 位浮点，坐标越大小数精度越差，几千米处只剩毫米级。
- GPU 每帧做「世界坐标→相机坐标」的大数相减，舍入逐帧跳变 → 表面高频闪烁。
- 高对比度的发光贴图对这种亚像素抖动最敏感，所以最明显。

**修复**（`JsbsimBridge.cs`）：实现**浮动原点（floating origin）**。
飞机水平距离超过阈值（默认 2000m）时，把飞机**连同地形等环境物体一起**平移回原点附近。
大家平移相同量，相对关系不变（视觉无跳变），但绝对坐标变小，渲染精度恢复。
- 新增 Inspector 配置：`useFloatingOrigin`（开关，默认开）、
  `floatingOriginThreshold`（触发距离，默认 2000m）、
  `floatingOriginObjects`（需随飞机一起平移的环境物体，**飞机本身不要放进去**）。
- `MaintainFloatingOrigin()` 每帧检测并重定位，同步处理平滑插值目标 `targetPos`
  和累计偏移 `accumulatedOriginShift`（`ApplyState` 算出的绝对目标坐标要加上此偏移，
  否则下一帧又被插回大坐标）。
- 已把场景里的 **Terrain** 配置进 `floatingOriginObjects`。

> 验证方法：Play + 启动 JSBSim 飞行 20 秒后，读飞机 `position`。
> 正常应被持续拉回原点附近（水平 x/z 很小，只有高度 y 在涨）；
> 若 x/z 涨到几千上万米，说明浮动原点没生效（检查 `useFloatingOrigin` 是否开、
> `floatingOriginObjects` 里的引用是否为 null）。

> **取舍提醒**：地形是有限大小的，飞机持续飞远时地形会逐渐移出脚下、地面最终"消失"。
> 对万米高空巡航的喷气机通常无影响（地面细节本就看不清）。若需长时间贴地飞行、
> 要地面一直在，需另上「地形流式加载 / 无限地形」方案。

---

## 6. 换机型时的检查清单

以后若要从 737 换成别的机型，按这个顺序过一遍能避开上面大部分坑：

1. 确认目标机型在 `D:\jsbsim\JSBSim\aircraft\<机型>\` 下存在。
2. 新建/复制初始化文件，文件名 = 脚本里 `initialize` 值 + `.xml`（坑 2）。
3. 改运行脚本 `<use aircraft="..." initialize="..."/>`。
4. **逐条核对 `unity_output.xml` 的每个属性在新机型存在**（坑 1，最关键）。
   - 喷气机 ↔ 活塞机的发动机属性差异最大（n1/n2 ↔ propeller-rpm/mixture）。
5. 启动引擎方式：喷气机 `propulsion/set-running -1`；活塞机要点火/混合比/磁电机/起动机。
6. 试飞后若操控方向相反，调 `FlightInput.cs` 的符号（坑 7）。
7. 用 `logState` 确认数据进来、用 JSBSim 窗口确认无 "will not be logged" 警告。
8. 换了场景/地形后，把新的环境物体重新拖进 `JsbsimBridge.floatingOriginObjects`（坑 9）。

---

## 7. 常见排错速查

| 现象 | 先查 |
|------|------|
| 飞机不动，但 socket 已连接 | `unity_output.xml` 有无机型不存在的属性（坑 1），开 `logState` 看日志 |
| JSBSim 启动即 FATAL ERROR could not be read | 初始化文件名是否和 `initialize` 值一致（坑 2） |
| 相机右键/方向键无反应 | 相机是否还在用 rb.MovePosition（坑 3） |
| 相机漂离飞机/离飞机几千米看不见 | 相机是否挂了 Rigidbody，必须移除（坑 4）；第三人称是否每帧更新（坑 5） |
| 同时两个相机画面打架 | 是否有 CameraManager 之外的相机被激活（坑 6） |
| 操控方向相反 | `FlightInput.cs` 舵面符号（坑 7） |
| 飞行中飞机模型抖动/闪烁(发光贴图明显) | 浮动原点是否开启、`floatingOriginObjects` 引用是否为 null（坑 9） |
| 控制连不上(TCP) | JSBSim 是否在跑、5502 端口是否被占、防火墙 |
| 收不到状态(UDP) | 5501 端口、`unity_output.xml` 的 port/rate、是否先 Play |

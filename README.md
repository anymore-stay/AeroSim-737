# AeroSim-737

AeroSim-737 是一个基于 Unity 与 JSBSim 的 Boeing 737-800 飞行模拟项目。Unity 负责飞机与机场画面、驾驶舱交互、仪表、相机、天气、地图、声音和可视动画；JSBSim 作为外部飞行动力学进程，通过本仓库的双向桥接与 Unity 实时通信。

> 当前状态（2026-07-20）：主功能链路已经完成，可从主场景的地面静止状态开始滑跑、起飞、空中操纵并使用接地反推；同时支持五边自动驾驶演示、动态天气、三类相机、驾驶舱仪表、飞行地图、A320 侧杆和讯飞语音控制。项目现已进入最终验收、稳定性回归和资源收口阶段。

本项目用于飞行模拟演示、课程/小组项目和后续技术扩展，不是商用级或训练认证级飞行模拟器。

## 项目概览

| 项目 | 当前配置 |
| --- | --- |
| 机型 | Boeing 737-800 |
| Unity | `2022.3.62f3c1` |
| 渲染管线 | URP `14.0.12` |
| 飞行动力学 | JSBSim `1.3.x`，仓库附带 `1.3.1` 安装包 |
| 地理/大世界 | Cesium for Unity `1.24.0` + Floating Origin |
| 天气系统 | UniStorm URP |
| 相机 | 驾驶舱、客舱、第三人称 |
| 主场景 | `AeroSimUnity/Assets/Scenes/MainScene.unity` |
| 主飞机 | `AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab` |
| 推荐平台 | Windows 10/11 |

## 已完成功能

| 功能域 | 当前能力 |
| --- | --- |
| 飞行动力学 | JSBSim 地面起步、双发怠速、UDP 状态输出、TCP 控制输入、Unity 自动启动与退出清理 |
| 飞行控制 | 俯仰、滚转、偏航、油门、俯仰配平、襟翼、扰流板、起落架、刹车、正推和反推 |
| 操纵保护 | 协调转弯、坡度角保护、侧滑保护、地面前轮转向、起落架低空收起限制、推油门自动松刹车 |
| 五边自动驾驶 | `O` 接通后自动完成起飞、上风边、侧风边、下风边、基准边、最后进近、拉平、接地和滑跑减速；`F5` 可手动推进航段 |
| 输入设备 | 键盘与图马思特 TCA/A320 侧杆并行输入，支持侧杆油门、扭转方向舵、POV 帽和断线重连 |
| 语音控制 | 讯飞语音听写，支持油门、起落架、襟翼、扰流板、刹车、配平和暂停指令 |
| 飞机动画 | 操纵盘/操纵柱、驾驶舱油门杆、舵面、襟翼、起落架、机轮、发动机风扇和外部灯光 |
| 驾驶舱仪表 | PFD、ND、EICAS、备用仪表、Clock ET、可点击 FMS 基础页面和 HUD |
| 飞行地图 | `M` 键打开，显示航迹、飞机朝向和飞行数据，支持缩放、拖动、平移和调整窗口尺寸 |
| 相机系统 | 驾驶舱、客舱、第三人称强制互斥切换，驾驶舱重新进入时复位，支持自由观察、POV 控制、第三人称环绕、避障和近地限制 |
| 天气与环境 | 北京大兴机场、Cesium 地理参考、昼夜变化、云层、雨雪、雷暴、雾、天气菜单和夜间视觉 |
| 声音与特效 | 发动机、飞行气流、接地/起落架、天气声音、航迹云、发动机热浪、翼尖涡流和机体外部灯光 |
| 性能与回归 | 仪表相机优化、机体视角裁剪、Floating Origin，以及覆盖飞控、仪表、天气、声音和桥接的 EditMode 测试 |

## 画面展示

### 飞行视角

![飞行视角](Pictures/飞行.png)

### 驾驶舱

![驾驶舱](Pictures/机舱.png)

### 客舱

![客舱](Pictures/客舱.png)

### 雨天天气

![雨天天气](Pictures/下雨.png)

### 黄昏

![黄昏](Pictures/黄昏.png)

### 夜晚

![夜晚](Pictures/夜晚.png)

### 仪表显示

![仪表显示](Pictures/仪表.png)

### 日落

![日落](Pictures/日落.png)

### 天空与云层

![天空与云层](Pictures/天空.png)

## 环境准备

建议准备以下环境：

- Windows 10/11。
- Git 与 Git LFS。
- Unity Hub 和 Unity `2022.3.62f3c1`。
- JSBSim `1.3.x`。
- 可选：图马思特 TCA/A320 侧杆。
- 可选：麦克风、网络连接和已开通语音听写 WebAPI 的讯飞账号。

## 快速开始

### 1. 克隆仓库

```powershell
git clone https://github.com/anymore-stay/AeroSim-737.git
cd AeroSim-737
```

### 2. 拉取 Git LFS 资源

项目中的 FBX、贴图、音频、图片和其他大文件由 Git LFS 管理。首次打开 Unity 前执行：

```powershell
git lfs install
git lfs pull
```

如果资源文件内容只有下面这种文本，说明当前拿到的是 LFS 指针：

```text
version https://git-lfs.github.com/spec/v1
oid sha256:...
size ...
```

重新执行 `git lfs pull` 即可。

### 3. 安装并配置 JSBSim

仓库附带安装包：

```text
JSBSimBridge/Install/JSBSim-1.3.1-1837-setup.exe
```

安装后建议设置用户环境变量 `JSBSIM_DIR`。它可以指向包含 `JSBSim.exe` 的目录，也可以直接指向 `JSBSim.exe`：

```powershell
[Environment]::SetEnvironmentVariable(
    "JSBSIM_DIR",
    "<JSBSim 安装目录或 JSBSim.exe 路径>",
    "User")
```

设置后重新启动 Unity Hub 和 Unity Editor，让新进程读取环境变量。

### 4. 打开 Unity 工程

在 Unity Hub 中打开：

```text
AeroSimUnity/
```

不要把仓库根目录直接作为 Unity 工程打开。首次打开时等待包导入、脚本编译和资源缓存重建完成。

### 5. 在编辑器中运行

打开飞行主场景：

```text
AeroSimUnity/Assets/Scenes/MainScene.unity
```

进入 Play 后，`JsbsimBridge` 默认会在 Windows 上自动运行 `JSBSimBridge/start_jsbsim.bat`。正常情况下会出现 JSBSim 命令行窗口，HUD 随后显示状态和控制通道均已连接。

如果自动启动失败，可保持 Unity Play 状态，再手动运行：

```text
JSBSimBridge/start_jsbsim.bat
```

停止 Play 时，Unity 会清理由当前会话自动启动的 JSBSim 进程。

### 6. 可选：配置讯飞语音控制

语音凭据只从用户环境变量读取，具体配置和支持的口令见 [讯飞语音控制接入说明](Docs/讯飞语音控制接入说明.md)。

## 第一次起飞

进入 Play 后，飞机默认处于地面静止状态：双发已经运转，油门怠速，起落架放下，刹车锁定。

1. 按 `B` 松开刹车，或直接推油门，正推超过阈值后会自动松刹车。
2. 按住 `LeftShift` 增加油门并开始滑跑。
3. 空速接近约 `150 kt` 时按 `S` 抬轮。
4. 离地超过约 `10 ft AGL` 后按 `G` 收起起落架。
5. 使用 `F/V` 调整襟翼，使用 `Z/X` 调整俯仰配平。
6. 接地后同时按住 `LeftControl + LeftShift` 使用满反推，再按 `B` 刹车。

反推代码当前允许随时触发，但实际操作应只在接地后使用。

## 五边自动驾驶演示

主飞机 Prefab 已接入 `B737PatternAutopilot`。进入 Play 后按 `O` 接通，系统会在首次接通时记录当前位置和航向作为跑道基准，随后按上风边、侧风边、下风边、基准边和最后进近顺序飞行。Prefab 默认启用进近拉平、主轮接地检测、扰流板和滑跑刹车；再次按 `O` 会退出外部控制并恢复键盘与侧杆。

`F5` 只在自动驾驶启用时推进到下一航段，用于调试和验收。自动驾驶运行时的 CSV 遥测写入 `AeroSimUnity/Captures/Autopilot/`，该目录属于本地生成输出，已加入 Git 忽略规则，不应提交。

## 操作按键

### 飞行控制

| 按键 | 功能 |
| --- | --- |
| `W` | 推杆低头；松键后保持当前升降舵修正 |
| `S` | 拉杆抬头；松键后保持当前升降舵修正 |
| `A` / `D` | 左滚 / 右滚，松键后副翼平滑回中 |
| `Q` / `E` | 左偏航 / 右偏航，同时参与协调转弯和地面前轮转向 |
| `Z` / `X` | 抬头配平 / 低头配平 |
| `LeftShift` | 增加正推；退出反推后重新建立正推 |
| `LeftControl` | 将油门收向怠速 |
| `LeftControl + LeftShift` | 满反推 |
| `F` / `V` | 襟翼增加 / 减少一级 |
| `R` / `T` | 扰流板增加 / 减少一级 |
| `G` | 起落架收放；过低或动画进行中时可能拒绝指令 |
| `B` | 刹车开关 |
| `Esc` | 暂停 / 恢复 Unity 与 JSBSim |
| `O` | 启用 / 退出五边自动驾驶演示 |
| `F5` | 自动驾驶启用时切换到下一航段 |

### 相机、界面与语音

| 按键/输入 | 功能 |
| --- | --- |
| `Shift + 7` | 客舱视角 |
| `Shift + 8` | 驾驶舱视角，并恢复驾驶舱初始机位 |
| `Shift + 9` | 第三人称视角 |
| 鼠标右键拖动 | 转动当前视角 |
| 方向键 | 在驾驶舱或客舱内前后左右移动 |
| `PageUp / PageDown` | 在驾驶舱或客舱内上下移动 |
| 鼠标滚轮 | 第三人称缩放；光标位于地图内时缩放地图 |
| `Tab` | 显示 / 隐藏 HUD |
| `1` | 驾驶舱操纵杆显示 / 隐藏 |
| `2` | 打开 / 关闭天气和时间菜单 |
| `M` | 打开 / 关闭飞行地图 |
| 按住 `Y` 说话，松开 `Y` | 发送语音识别并执行飞行指令 |

## TCA/A320 侧杆

项目在 Windows 上通过系统多媒体摇杆接口读取图马思特 TCA/A320 侧杆，默认匹配硬件 ID `044F:0406`，并支持名称匹配和单控制器降级连接。

- X/Y 轴控制横滚和俯仰。
- 扭转轴控制方向舵。
- 侧杆油门轴控制正推油门。
- POV 帽控制当前相机视角。
- 设备断开后会自动重连，断开期间键盘仍可继续控制。
- 键盘修正量会与侧杆输入叠加，油门键盘输入可暂时接管侧杆油门。

该输入实现依赖 Windows，其他平台不会启用侧杆接口。

## 讯飞语音控制

运行 `MainScene` 后，按住 `Y` 说话，松开后发送本次录音。识别结果和执行结果会显示在屏幕顶部，并写入 Unity Console。

当前支持的指令类型包括：

- 设置油门百分比、怠速或最大油门。
- 放下/收起起落架。
- 增减或全收/全放襟翼。
- 增减或全收/全开扰流板。
- 开启/解除刹车。
- 抬头/低头配平。
- 暂停/继续飞行。

语音反推暂时禁用。起落架和刹车指令仍受本地安全条件限制。讯飞凭据严禁写入代码、Prefab、Scene 或 Git 提交；正式对外发布时应把签名和请求代理放到自己的服务端。

## 飞行地图

按 `M` 打开独立飞行地图。地图会在运行时自动创建，不需要手动把 UI 拖入场景。

- 显示飞机位置、平滑后的朝向、飞行数据、航迹和航点。
- 鼠标滚轮调整显示范围。
- 拖动标题栏移动窗口。
- 在地图区域拖动可平移地图中心。
- 拖动窗口边缘可调整地图尺寸。
- 默认提供本地地图背景；代码也保留 Cesium 场景底图和在线瓦片配置入口。

## 天气与时间

项目使用 UniStorm URP 天气系统。主场景中已接入太阳、月亮、风区、体积云、云影、星空、降水、雾和天气声音。

按 `2` 打开右上角天气/时间菜单，可以选择天气、调节时间并查看当前时间。切换到降水天气后，声音会立即到达目标音量，云层、雨滴和地面湿润效果仍保持自然过渡。

驾驶舱、客舱和第三人称相机之间的降水跟随、雾距离、太阳盘和天气声音已做统一处理。相关排查方法见 [UniStorm 相机天气效果指南](Docs/UniStorm-Camera-Weather-Effects-Guide.md)。

## JSBSim 桥接

Unity 与 JSBSim 的数据流如下：

```text
键盘 / TCA A320 侧杆 / 讯飞语音 / 五边自动驾驶
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

- JSBSim 以 `120 Hz` 进行物理步进，并以 `60 Hz` 向 Unity 输出状态。
- Unity 默认监听 UDP `5501`，控制连接使用 TCP `5502`。
- 初始条件来自 `JSBSimBridge/unity_air.xml`，当前为地面静止起步。
- `JSBSimBridge/b737_unity.xml` 会启动双发并将油门保持在怠速。
- HUD 中的发动机转速显示为 `N1 %`，不是 RPM。
- 发动机风扇视觉速度按 `100% N1 -> 4657.5 RPM` 映射，即 CFM56 约 `5175 RPM` 最大 N1 转速的 `90%`。

油门为 `0%` 时风扇仍旋转是正常现象，因为这表示双发正在怠速运转，并不表示发动机已经关车。

## 项目结构

```text
AeroSim-737/
├── AeroSimUnity/                         # Unity 工程
│   ├── Assets/
│   │   ├── Aircraft/B737/               # 飞机模型、Prefab、材质、涂装和仪表
│   │   ├── Environment/                 # 北京大兴机场与环境资源
│   │   ├── Resources/                   # 运行时资源，包括地图背景
│   │   ├── Scenes/                      # Unity 场景，当前运行入口为 MainScene
│   │   ├── Scripts/
│   │   │   ├── Aircraft/B737/           # 737 运行时逻辑与 Voice 子目录
│   │   │   ├── Camera/                  # 相机切换、视角和 AudioListener 管理
│   │   │   ├── Map/                     # 飞行地图
│   │   │   ├── World/                   # Floating Origin 与大世界逻辑
│   │   │   └── Editor/B737/             # 编辑器工具与 EditMode 测试
│   │   ├── UniStorm Weather System/     # 天气系统资源
│   │   └── Settings/                    # URP 和工程内设置
│   ├── Packages/
│   └── ProjectSettings/
├── JSBSimBridge/                        # JSBSim 安装包、初始条件、启动和通信配置
├── XplaneAssets/                        # 原始机模/场景导入源与辅助脚本
├── Pictures/                            # README 图片
├── Docs/                                # 专题说明与历史设计/实施记录
├── AGENTS.md                            # Codex 协作基线
├── CLAUDE.md                            # Claude Code 协作基线
└── README.md
```

## 关键文件

| 文件/目录 | 说明 |
| --- | --- |
| `AeroSimUnity/Assets/Scenes/MainScene.unity` | Unity 主场景 |
| `AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab` | 主飞机 Prefab 和运行配置基线 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/JsbsimBridge.cs` | JSBSim 状态接收、控制连接、位置姿态与进程管理入口 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/JsbsimProcessLauncher.cs` | Windows 下自动启动和清理 JSBSim |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/FlightInput.cs` | 键盘、侧杆叠加、飞控保护和控制发送 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737PatternAutopilot.cs` | 五边航线、进近拉平、接地检测、滑跑控制和遥测输出 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737WingtipVortexController.cs` | 根据飞行状态驱动翼尖涡流粒子效果 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/ThrustmasterA320SidestickInput.cs` | TCA/A320 侧杆读取、映射和重连 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/Voice/` | 讯飞语音客户端、命令解析和执行 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/FlightHud.cs` | HUD 状态、N1 与操作提示 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737EngineSpinner.cs` | 发动机风扇视觉转速 |
| `AeroSimUnity/Assets/Scripts/Camera/CameraManager.cs` | 三类相机切换 |
| `AeroSimUnity/Assets/Scripts/Camera/CockpitCameraController.cs` | 驾驶舱/客舱移动和第三人称环绕、避障、近地限制 |
| `AeroSimUnity/Assets/Scripts/Map/FlightMapOverlay.cs` | 运行时飞行地图 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormWeatherMenuController.cs` | 天气菜单中文化和布局 |
| `AeroSimUnity/Assets/Aircraft/B737/Instruments/` | PFD、ND、EICAS、FMS、备用仪表等资源 |
| `JSBSimBridge/start_jsbsim.bat` | JSBSim 启动脚本 |
| `JSBSimBridge/b737_unity.xml` | JSBSim 运行脚本 |
| `JSBSimBridge/unity_air.xml` | 地面初始条件 |
| `JSBSimBridge/unity_output.xml` | UDP 状态输出字段 |

## 测试与验收

EditMode 测试位于：

```text
AeroSimUnity/Assets/Scripts/Editor/B737/
```

当前测试覆盖 JSBSim 文本解析、地面防穿透、升降舵保持、反推、侧杆输入、坡度/侧滑保护、五边自动驾驶数学逻辑、翼尖涡流数学逻辑、仪表、天气、声音、灯光、相机相关配置和 HUD 文案等关键行为。

在 Unity 中打开 Test Runner，运行 EditMode 测试。最终验收还应打开 `MainScene`，完成一次完整 Play 流程：

1. JSBSim 自动启动并建立 UDP/TCP 通信。
2. 地面静止、刹车、滑跑和起飞正常。
3. 键盘与侧杆输入均可用，飞控保护没有明显异常。
4. 起落架、襟翼、扰流板、油门杆、发动机和声音反馈一致。
5. HUD、PFD、ND、EICAS、FMS、备用仪表和地图显示正常。
6. 三种相机、天气切换、昼夜、降水和天气声音表现一致。
7. 接地后反推和刹车可用，退出 Play 后 JSBSim 进程被正确清理。
8. 按 `O` 可完成一次五边自动驾驶演示，接通和退出后手动输入均正常；`F5` 只推进当前自动驾驶航段。

## 当前边界

- FMS 当前提供可点击页面和基础状态显示，不是完整 FMC 航路规划系统。
- 项目仅提供演示用途的五边自动驾驶、进近拉平和滑跑控制，不是完整真实 737 自动驾驶、自动油门、FMC 航路计算或认证级自动着陆系统；同时没有完整实现全部电气/液压/气源系统和标准运行程序。
- JSBSim 自动启动和 TCA/A320 侧杆读取依赖 Windows。
- 讯飞语音需要网络、麦克风和用户自己的 WebAPI 凭据；语音反推保持禁用。
- 天气、Cesium、机模、机场、音频和其他第三方资源各自受原授权约束。仓库当前没有统一的根级开源许可证，公开发布或二次分发前必须逐项核对授权。

## 开发约定

- 737 运行时脚本放在 `AeroSimUnity/Assets/Scripts/Aircraft/B737/`。
- 相机、地图和大世界逻辑分别放在 `Scripts/Camera/`、`Scripts/Map/` 和 `Scripts/World/`。
- 编辑器工具与 EditMode 测试放在 `AeroSimUnity/Assets/Scripts/Editor/B737/`。
- 主场景飞机统一使用 `B737.prefab`，不要长期直接编辑裸 FBX。
- `AeroSimUnity/Captures/` 只保存运行时生成的自动驾驶遥测和临时验证输出，不提交其中的 CSV、截图或其他产物；需要长期保留的验证结论应整理到 `Docs/`。
- 移动、复制或重命名 Unity 资源时必须连同 `.meta` 文件一起处理。
- 修改 Scene、Prefab、材质、RenderTexture 或贴图后，回 Unity 检查 Missing Script、Missing Reference 和 Console 错误。
- 提交前确认 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/`、`.vs/`、`*.csproj`、`*.sln` 没有进入版本控制。
- Commit Message 使用中文，建议格式为 `<类型>：<简述>`。

更完整的协作和子系统约束见 [AGENTS.md](AGENTS.md) 与 [CLAUDE.md](CLAUDE.md)。

## 常见问题

### 图片、模型或音频变成文本

Git LFS 资源没有完整拉取，执行：

```powershell
git lfs pull
```

### 进入 Play 后 JSBSim 没有启动

检查：

- `JSBSIM_DIR` 是否指向正确目录或 `JSBSim.exe`。
- `JSBSimBridge/start_jsbsim.bat` 是否存在。
- Unity Console 是否显示启动脚本或端口错误。
- UDP `5501`、TCP `5502` 是否被其他程序占用。

保持 Play 状态手动运行 `JSBSimBridge/start_jsbsim.bat`，可以直接看到启动脚本的错误提示。

### HUD 一直显示等待 JSBSim 数据

确认 JSBSim 命令行窗口正在运行，并检查 `unity_output.xml` 是否仍向 UDP `5501` 输出。HUD 的状态连接和控制连接是两条独立链路，二者都应显示已连接。

### 油门是 0%，发动机为什么还在转

当前启动脚本会让双发保持运行。油门 `0%` 表示怠速，不是发动机停止，因此 HUD 仍会显示非零 N1，发动机风扇也会继续旋转。

### 天气菜单或地图打不开

- 天气菜单按 `2`。
- 飞行地图按 `M`。
- HUD 按 `Tab`。

如果按键无反应，先检查 Unity Console 是否存在脚本编译错误，以及当前 Game 视图是否获得键盘焦点。

### 语音控制没有反应

确认系统检测到麦克风，并已设置 `XFYUN_APP_ID`、`XFYUN_API_KEY`、`XFYUN_API_SECRET`。设置环境变量后需要完全退出并重新启动 Unity Hub 和 Unity Editor。

### 侧杆没有连接

在 Windows“设置 USB 游戏控制器”中确认设备可见。项目默认识别图马思特 TCA/A320 的 `044F:0406`，设备断开后每秒尝试重连；未连接时键盘控制仍然有效。

## 相关文档

- [讯飞语音控制接入说明](Docs/讯飞语音控制接入说明.md)
- [UniStorm 相机天气效果指南](Docs/UniStorm-Camera-Weather-Effects-Guide.md)
- [AeroSimUnity 早期脚本说明](Docs/AeroSimUnity_脚本说明.md)，该文档是阶段性历史资料，当前行为以代码、Prefab 和本 README 为准。

## 维护方向

主功能已经完成，后续工作以回归和收口为主：

- 保持 `MainScene.unity`、`B737.prefab`、JSBSim 和三类相机引用稳定。
- 完成键盘、TCA/A320 侧杆和讯飞语音三条输入链路的联合验收。
- 完成 `O` 五边自动驾驶从接通、航段切换、拉平、接地到退出接管的回归，并检查翼尖涡流在空速、迎角、襟翼、湿度和 Floating Origin 重定位下的表现。
- 继续微调最终光照、机舱阴影、天气过渡、音量混合和仪表可读性。
- 清理 Missing Reference、材质异常和 Unity 自动产生的无关脏改动。
- 在对外发布前补充统一许可证、第三方资源授权清单和构建发布说明。

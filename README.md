# AeroSim-737

AeroSim-737 是一个基于 Unity 的 Boeing 737-800 飞行模拟项目。项目使用 Unity 负责飞机可视化、驾驶舱/客舱视角、HUD、机场环境、天气系统和仪表界面，使用 JSBSim 作为外部飞行动力学进程，通过本仓库的 `JSBSimBridge/` 与 Unity 主场景通信。

项目当前重点是把 737 飞机、北京大兴机场环境、动态天气、驾驶舱仪表和 JSBSim 飞行链路整合成一个可演示、可继续迭代的飞行模拟原型。

## 项目概览

| 项目 | 内容 |
| --- | --- |
| 机型 | Boeing 737-800 |
| 引擎 | Unity 2022.3 LTS |
| 渲染管线 | URP |
| 飞行动力学 | JSBSim |
| 天气系统 | UniStorm URP |
| 地理/大世界 | Cesium for Unity |
| 主场景 | `AeroSimUnity/Assets/Scenes/MainScene.unity` |
| Unity 工程 | `AeroSimUnity/` |

## 功能亮点

- 737-800 飞机模型与主 Prefab 管理。
- 驾驶舱、客舱、第三人称三类视角切换。
- HUD 实时显示飞行数据、控制状态和操作提示。
- UniStorm 动态天气：晴天、雨天、雷暴、昼夜和时间调节。
- 右上角天气/时间菜单，按 `2` 打开，支持中文天气名称和当前时间显示。
- 北京大兴机场环境，地面材质已按近景飞行视角重新调整。
- PFD、ND、EICAS、备用仪表等驾驶舱显示系统的基础实现入口。
- JSBSim 外部飞行动力学桥接，支持 Unity 接收飞行状态并发送控制指令。
- Cesium 地理参考与大世界坐标支持，用于后续扩展真实地景。

> 说明：项目仍在开发中，目前更适合作为飞行模拟原型、课程/小组项目展示和后续开发基础，不是完整商用级飞行模拟器。

---

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

---

## 当前开发状态

当前项目已经完成主场景基本整合：

- 主飞机统一使用 `B737.prefab`。
- 主场景统一为 `MainScene.unity`。
- JSBSim 桥接文件集中在 `JSBSimBridge/`。
- 天气系统已真实合入主场景，不再依赖临时运行时注入。
- HUD、相机切换、天气菜单、时间调节和基础仪表显示已经接入。
- 降水天气声音已调整为切换后立即出现，云层和雨滴视觉仍保持自然过渡。
- 机场地面材质已改为项目内资源路径，避免依赖本机绝对路径。

仍在继续打磨：

- 太阳直射、机舱阴影和整体光照表现。
- 飞机起飞、地面接触和 JSBSim 姿态同步细节。
- 仪表数据覆盖和显示精度。
- 天气、发动机和座舱音频的混音平衡。
- 场景资源和 Unity 自动脏改动的提交筛选。

---

## 快速开始

### 1. 克隆仓库

```powershell
git clone https://github.com/anymore-stay/AeroSim-737.git
cd AeroSim-737
```

### 2. 拉取 Git LFS 资源

项目中的 FBX、贴图、音频、图片等大文件由 Git LFS 管理。首次打开 Unity 前请执行：

```powershell
git lfs install
git lfs pull
```

如果资源文件打开后只看到类似下面的文本，说明 LFS 文件还没有真正拉下来：

```text
version https://git-lfs.github.com/spec/v1
oid sha256:...
size ...
```

重新执行 `git lfs pull` 即可。

### 3. 打开 Unity 工程

用 Unity 打开下面这个目录：

```text
AeroSimUnity/
```

推荐 Unity 版本：

```text
Unity 2022.3.62f3c1
```

首次打开时 Unity 会导入包、编译脚本和重建资源缓存，等待完成即可。

### 4. 打开主场景

主场景路径：

```text
AeroSimUnity/Assets/Scenes/MainScene.unity
```

进入 Play 后，如果只想看画面、切相机、调天气和测试部分本地交互，可以不启动 JSBSim。  
如果要测试真实飞行动力学和 HUD 飞行数据，请继续启动 JSBSim。

### 5. 启动 JSBSim（可选但推荐）

项目提供了启动脚本：

```text
JSBSimBridge/start_jsbsim.bat
```

JSBSim 安装包位于：

```text
JSBSimBridge/Install/JSBSim-1.3.1-1837-setup.exe
```

如果团队成员本机安装路径不同，推荐设置环境变量：

```text
JSBSIM_DIR=<包含 JSBSim.exe 的目录>
```

然后再运行 `start_jsbsim.bat`。

---

## 操作按键

### 飞行控制

| 按键 | 功能 |
| --- | --- |
| `W` | 低头 |
| `S` | 抬头 |
| `A` | 左滚 |
| `D` | 右滚 |
| `Q` | 左偏航 |
| `E` | 右偏航 |
| `LeftShift` | 增加正推油门 |
| `LeftControl` | 将油门收向怠速 |
| `LeftControl + LeftShift` | 接地后增加反推，HUD 油门显示为负数 |
| `B` | 切换刹车 |
| `G` | 起落架收放 |
| `F` | 放下襟翼 |
| `V` | 收回襟翼 |
| `R` | 增加扰流板 |
| `T` | 减少扰流板 |

### 相机与视角

| 按键 | 功能 |
| --- | --- |
| `Shift + 7` | 客舱视角 |
| `Shift + 8` | 驾驶舱视角 |
| `Shift + 9` | 第三人称视角 |
| `鼠标右键拖动` | 转动视角 |
| `方向键` | 移动视角 |
| `PageUp / PageDown` | 上下移动视角 |
| `鼠标滚轮` | 第三人称缩放 |

### 界面与天气

| 按键 | 功能 |
| --- | --- |
| `Tab` | 显示 / 隐藏 HUD |
| `1` | 操纵杆显示 / 隐藏 |
| `2` | 打开 / 关闭天气和时间选择 |

---

## 项目结构

```text
AeroSim-737/
├── AeroSimUnity/                         # Unity 工程
│   ├── Assets/
│   │   ├── Aircraft/B737/               # 737 飞机资源、Prefab、仪表资源
│   │   ├── Environment/                 # 机场与环境资源
│   │   ├── Scenes/                      # 主场景
│   │   ├── Scripts/
│   │   │   ├── Aircraft/B737/           # 737 运行时脚本
│   │   │   ├── Camera/                  # 相机切换与视角控制
│   │   │   ├── Map/                     # 地图/航图覆盖层
│   │   │   ├── World/                   # 大世界与原点管理
│   │   │   └── Editor/B737/             # 编辑器工具与测试
│   │   ├── UniStorm Weather System/     # 天气系统资源
│   │   └── Settings/                    # 渲染和工程内设置
│   ├── Packages/
│   └── ProjectSettings/
├── JSBSimBridge/                        # JSBSim 配置、安装包和启动脚本
├── Pictures/                            # README 图片
├── Docs/                                # 项目文档
├── AGENTS.md                            # Codex 协作说明
├── CLAUDE.md                            # Claude 协作说明
└── README.md
```

---

## 关键文件

| 文件/目录 | 说明 |
| --- | --- |
| `AeroSimUnity/Assets/Scenes/MainScene.unity` | Unity 主场景 |
| `AeroSimUnity/Assets/Aircraft/B737/Prefabs/B737.prefab` | 主飞机 Prefab |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/JsbsimBridge.cs` | JSBSim 与 Unity 的状态/控制桥接 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/FlightInput.cs` | 键盘输入与控制发送 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/FlightHud.cs` | HUD 显示 |
| `AeroSimUnity/Assets/Scripts/Aircraft/B737/B737UniStormWeatherMenuController.cs` | 天气菜单中文化和布局 |
| `AeroSimUnity/Assets/Scripts/Camera/CameraManager.cs` | 相机切换 |
| `JSBSimBridge/start_jsbsim.bat` | JSBSim 启动脚本 |
| `JSBSimBridge/b737_unity.xml` | JSBSim 场景配置 |
| `JSBSimBridge/unity_output.xml` | JSBSim 输出配置 |

---

## 天气系统

项目当前使用 UniStorm URP 天气系统。主场景中包含：

- `UniStorm URP System`
- `UniStorm Sun`
- `UniStorm Moon`
- `UniStorm Windzone`
- `UniStorm Volumetric Clouds`
- `Clouds Shadows`
- `UniStorm Stars`

天气菜单按 `2` 打开，位于右上角。可以选择天气、调节时间，并直接看到当前时间文本。降水天气的声音会在切换后立即出现，但云层、雨滴和地面湿润效果仍保持逐渐变化，让画面过渡更自然。

---

## JSBSim 桥接

Unity 与 JSBSim 的职责分工如下：

```text
键盘输入 -> Unity FlightInput -> JSBSim 控制通道
JSBSim 飞行动力学 -> UDP 状态输出 -> Unity JsbsimBridge -> 飞机姿态/HUD/仪表
```

常用文件：

- `JSBSimBridge/start_jsbsim.bat`
- `JSBSimBridge/b737_unity.xml`
- `JSBSimBridge/unity_output.xml`
- `AeroSimUnity/Assets/Scripts/Aircraft/B737/JsbsimBridge.cs`

不启动 JSBSim 时，仍可以测试画面、天气、相机、HUD 开关和部分本地动画；启动 JSBSim 后，HUD 和仪表数据会随飞行动力学状态更新。

---

## 开发约定

- 737 运行时脚本放在 `AeroSimUnity/Assets/Scripts/Aircraft/B737/`。
- 相机脚本放在 `AeroSimUnity/Assets/Scripts/Camera/`。
- 地图/航图脚本放在 `AeroSimUnity/Assets/Scripts/Map/`。
- 编辑器工具和编辑器测试放在 `AeroSimUnity/Assets/Scripts/Editor/B737/`。
- Unity 资源移动、复制、重命名时必须连同 `.meta` 文件一起处理。
- 修改场景、Prefab、材质后，需要回 Unity 检查 Missing Script / Missing Reference。
- 提交前请确认 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/`、`.vs/`、`*.csproj`、`*.sln` 没有被加入版本控制。

---

## 常见问题

### 图片或模型变成文本

这是 Git LFS 资源没有拉下来。执行：

```powershell
git lfs pull
```

### HUD 不显示

按 `Tab` 切换 HUD 显示。如果仍然看不到，检查当前相机和 HUD Canvas 是否输出到同一个 Display。

### 天气菜单打不开

按 `2`。如果仍无反应，检查 `UniStorm URP System` 是否启用，以及 Console 是否存在脚本错误。

### 下雨没有声音

先检查当前相机是否启用了 `AudioListener`。再查看 Play 后 Hierarchy 中 `UniStorm Sounds` 下的雨声 AudioSource 是否正在播放。

### JSBSim 没有数据

确认：

- `JSBSimBridge/start_jsbsim.bat` 已运行。
- `JSBSIM_DIR` 指向包含 `JSBSim.exe` 的目录。
- Unity Console 没有 UDP/TCP 端口错误。
- HUD 不再显示“等待 JSBSim 数据”。

---

## 后续计划

- 继续优化太阳方向、机舱阴影和天气光照。
- 完善仪表数据链路和异常状态显示。
- 改进起飞、接地、滑跑和姿态同步细节。
- 继续整理主场景资源，减少 Unity 自动脏改动对协作的影响。
- 扩展更多飞行流程和演示任务。

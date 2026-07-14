# 默认大部多云天气设计

## 目标

主场景启动后，UniStorm 默认天气应为 `Mostly Cloudy`，天气菜单默认选中并显示中文“大部多云”。

## 现状

- 主场景为 `AeroSimUnity/Assets/Scenes/MainScene.unity`。
- 当前 `CurrentWeatherType` 指向 `Partly Cloudy`，其 GUID 为 `865815ce607d94c4cb41162c44555e7c`。
- 目标天气资源为 `AeroSimUnity/Assets/UniStorm Weather System/Weather Types/Non-Precipitation/Mostly Cloudy.asset`，其 GUID 为 `b1b04f0270cfa784588fdc5818097ad3`。
- `AllWeatherTypes` 已包含目标天气资源。
- `B737UniStormWeatherMenuController` 已将 `Mostly Cloudy` 映射为“大部多云”，并通过 `CurrentWeatherType` 在下拉菜单中选择当前天气。

## 方案

只修改 `MainScene.unity` 中 UniStorm 系统的 `CurrentWeatherType` 引用，从 `Partly Cloudy` GUID 替换为 `Mostly Cloudy` GUID。

不修改 UniStorm Prefab、天气资源参数、运行时控制器或菜单布局。菜单会继续从 `CurrentWeatherType` 推导默认选项，因此实际天气和菜单显示保持一致。

## 测试

在 `AeroSimUnity/Assets/Scripts/Editor/B737/B737UniStormWeatherMenuControllerTests.cs` 增加编辑器测试：

- 验证 `Mostly Cloudy.asset.meta` 的 GUID 是 `b1b04f0270cfa784588fdc5818097ad3`。
- 验证 `MainScene.unity` 的 `CurrentWeatherType` 使用该 GUID。

该测试在场景仍指向 `Partly Cloudy` 时应失败；替换场景引用后应通过。

## 范围外

- 不调整云量、光照、声音、降水、雾或天气过渡参数。
- 不改变天气菜单按键、布局或中文名称映射。
- 不处理当前工作区内其他未提交 Unity 资源改动。

# PFD 纹理目录同步设计

## 目标

同步 Textures/Original 中的 PNG 时，在 Textures/Used 和
Textures/PreviewRGB 中保留图片相对于 Original 的完整目录结构。

## 路径规则

- Original/a.png 输出为 Used/a.png 和 PreviewRGB/a.png。
- Original/Buttons/Left/a.png 输出为 Used/Buttons/Left/a.png 和
  PreviewRGB/Buttons/Left/a.png。
- 只为实际输出的图片创建目录，不复制空目录。
- 相同文件名位于不同子目录时互不覆盖。

## 实现

现有 AssetDatabase.FindAssets 已经递归查找图片。修改输出路径计算逻辑：
从源资源路径移除 Original 根路径，得到相对路径，再分别拼接到两个输出根目录。
现有图片复制、去除 Alpha、Sprite 导入配置逻辑保持不变。

## 验证

增加编辑器测试验证根目录和多层子目录路径映射，并通过 Unity 菜单执行一次真实同步，
确认两个输出目录均生成对应子目录和图片。

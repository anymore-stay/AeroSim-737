# PFD 纹理目录同步实施计划

> **执行要求：** 使用测试驱动方式逐项实施。

**目标：** PFD 纹理同步工具在转换图片时保留 Original 下的相对文件夹结构。

**架构：** 保留现有递归资源搜索和图片转换流程，仅提取一个纯路径映射方法。
转换循环使用该方法分别构造 Used 与 PreviewRGB 的目标路径。

**技术栈：** Unity 2022.3、UnityEditor AssetDatabase、NUnit。

---

### 任务 1：增加路径映射回归测试

**文件：**
- 新建：AeroSimUnity/Assets/Scripts/Editor/B737/PFDTextureSyncToolTests.cs

- [ ] 编写多层子目录与根目录路径测试。
- [ ] 在 Unity EditMode 中运行测试，确认因为路径映射方法不存在而失败。

### 任务 2：实现相对目录保留

**文件：**
- 修改：AeroSimUnity/Assets/Aircraft/B737/Instruments/PFD/Scripts/PFDTextureSyncTool.cs

- [ ] 新增输出资源路径计算方法。
- [ ] 转换循环不再只取文件名，改为使用完整相对路径。
- [ ] 运行测试并确认通过。

### 任务 3：Unity 集成验证

- [ ] 在 Original 下建立多层测试目录并放入 PNG。
- [ ] 执行 PFD 纹理同步菜单。
- [ ] 确认 Used 与 PreviewRGB 中存在相同目录结构。
- [ ] 清理测试资源并检查 Unity Console。

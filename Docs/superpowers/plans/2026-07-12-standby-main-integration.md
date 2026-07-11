# Standby 主飞机接入实施计划

1. 核对现有 PFD 的 RenderTexture、屏幕材质、相机和 JSBSim 数据驱动结构。
2. 为 Standby 增加正式 JSBSim 数据驱动，复用 PFD 的空速、高度、姿态和磁航向字段，并自动禁用模拟数据。
3. 将空速带改为逐帧平滑追踪目标位置，保留 0～40 节刻度带静止规则和数字实时更新。
4. 增加 Standby 正式显示链路编辑器工具与测试，在 B737.prefab 中创建独立相机、RenderTexture、材质和中央屏幕平面。
5. 运行生成工具，把 Standby 实例写入 B737.prefab，同时保留已有未提交配置和用户调整过的 Standby.prefab。
6. 运行 EditMode 测试和 Unity 编译检查，确认正式实例没有启用 StandbyDemoDataSource，并说明位置微调节点。

# PFD 玻璃质感材质设计

## 目标

让左右 PFD 与现有 ND、EICAS 保持一致的仪表玻璃观感：画面仍清晰可读，同时具有轻微软化、微弱高光和环境反射。

## 根因

当前 `PFD_Left.mat`、`PFD_Right.mat` 使用 URP Unlit，只直接输出 RenderTexture，不参与高光和环境反射。相邻 ND、EICAS 使用 URP Lit，并设置了平滑度、镜面高光和环境反射，因此视觉上更像玻璃屏幕。

## 实现方案

- 左右 PFD 继续使用各自独立材质和 RenderTexture。
- 两份材质改用 `Universal Render Pipeline/Lit`。
- `_BaseMap`、`_MainTex` 和 `_EmissionMap` 都绑定对应侧的 PFD RenderTexture。
- 启用 `_EMISSION`，使用约 `0.55` 的发光颜色保持屏幕在暗座舱中的可读性。
- 使用 `Metallic = 0`、`Smoothness = 0.35`，开启镜面高光和环境反射。
- 不增加透明玻璃 Plane，不修改 PFD Plane 的位置、旋转和比例，不修改 PFD 内部 UI。
- 修改生成工具，保证以后重新生成时仍使用上述 Lit 参数。

## 验收标准

- PFD 材质使用 URP Lit，不再使用 URP Unlit。
- 左右材质分别引用各自的 RenderTexture，不能串画面。
- PFD 有与 ND 接近的微弱反光，文字和刻度仍清晰。
- 左右 PFD Plane 的 Transform 在应用材质前后完全不变。
- Unity 编译无新增错误，相关 EditMode 测试和全量 EditMode 测试通过。


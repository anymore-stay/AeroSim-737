# ZGYY Airport Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the ZGYY airport assets into `Models`, `SourcePack`, and `maps` while preserving Unity references and making the airport root explicitly compatible with floating-origin shifting.

**Architecture:** Preserve the existing FBX asset GUID by moving its `.meta` together with the model file, preserve folder GUIDs by renaming/moving each folder with its `.meta`, and keep the scene reference stable by leaving the airport prefab instance bound to the same model GUID. Add `FloatingOriginObject` directly to the airport root in `MainScene` so the airport no longer depends on implicit or legacy scene-root behavior.

**Tech Stack:** Unity YAML assets, Unity `.meta` GUID preservation, PowerShell filesystem moves, scene serialization edits.

---

### Task 1: Reorganize Airport Asset Paths

**Files:**
- Modify: `AeroSimUnity/Assets/Environment/Airport/`

- [ ] Rename top-level airport folder `5` to `ZGYY` and rename `5.meta` to `ZGYY.meta`.
- [ ] Create `AeroSimUnity/Assets/Environment/Airport/ZGYY/Models/` and move `ZGYY.fbx` plus `ZGYY.fbx.meta` into it as `ZGYY_Airport.fbx` and `ZGYY_Airport.fbx.meta`.
- [ ] Rename nested source folder `AeroSimUnity/Assets/Environment/Airport/ZGYY/5/5` to `SourcePack` together with its `.meta`.
- [ ] Move `AeroSimUnity/Assets/Environment/Airport/ZGYY/5/maps` to `AeroSimUnity/Assets/Environment/Airport/ZGYY/maps` together with `maps.meta`.
- [ ] Remove the now-empty intermediate folder `AeroSimUnity/Assets/Environment/Airport/ZGYY/5` and its folder meta so the final structure is exactly `ZGYY/{Models,SourcePack,maps}`.

### Task 2: Update Scene Attachment for Airport Root

**Files:**
- Modify: `AeroSimUnity/Assets/Scenes/MainScene.unity`

- [ ] Add a scene-level `FloatingOriginObject` component override to the airport prefab instance rooted at source object `fileID: 919132149155446097`.
- [ ] Keep the airport instance name override as `airport` so any name-based assumptions in the current scene remain unchanged.
- [ ] Preserve the existing airport FBX GUID reference so the prefab instance continues to point at the same imported model after the move.

### Task 3: Verify Serialized Stability

**Files:**
- Modify: `AeroSimUnity/Assets/Scenes/MainScene.unity`

- [ ] Confirm the airport prefab instance still references GUID `eb7fce3714c222b4f88bffd0b8ef3f0d`.
- [ ] Confirm the new `FloatingOriginObject` script GUID `2fd890d44e8443f4ea3ee6257c1c0bb1` is present once on the airport root override.
- [ ] Confirm the resulting directory tree is:

```text
AeroSimUnity/Assets/Environment/Airport/
└── ZGYY/
    ├── Models/
    │   └── ZGYY_Airport.fbx
    ├── SourcePack/
    └── maps/
```

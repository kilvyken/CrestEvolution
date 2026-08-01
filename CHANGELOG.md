# Changelog

## 0.1.0-architecture

- 根据原始文档建立五个核心开发文件。
- 建立完整 Feature ID 需求目录和原文机器可读归档。
- 建立核心状态、伤害、丝线护盾、标记血、输入、资源事务和动态绑定示例。
- 建立十个纹章模块的注册骨架。

## 0.2.0-crest-quick-switch-integration
- Added `CrestQuickSwitchIntegration.cs` as an internal overhaul feature rather than a second BepInEx plugin.
- Native `inputActions` now provide the modifier and four directions; fixed `KeyCode`/raw-axis fallbacks were removed.
- Added queued dash switching, dash settle frames, pre/post swap dash normalization, and horizontal velocity cleanup.
- Corrected and made the legacy all-crests bench replenish behavior opt-in.

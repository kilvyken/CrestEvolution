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

## 0.3.0-agent-skill - 2026-08-02

- 新增标准 Agent Skill 与根目录 `AGENTS.md`。
- 新增机器可读和人工可读游戏 Key 注册表，区分 E0/E1/E2/U 证据。
- 新增冲刺换纹章软锁安全协议、输入 Action 探针、FSM/资源探针流程。
- 新增 Agent 任务模板、绑定报告模板和静态契约检查器。
- 强化“禁止固定物理键、禁止猜绑定、单一换纹章所有者、资源事务与存档安全”约束。

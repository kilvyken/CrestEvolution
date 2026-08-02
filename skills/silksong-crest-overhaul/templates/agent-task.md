# Agent 任务模板

## 任务

- Feature IDs：
- 目标：
- 允许修改目录：
- 禁止修改目录：
- 本机游戏构建：
- 已有 E1 探针：

## 开始前

必须读取 `skills/silksong-crest-overhaul/SKILL.md` 和引用文件。运行：

```bash
python skills/silksong-crest-overhaul/scripts/validate_contract.py .
```

## 需求提取

逐项填写：

- 触发：
- 前置状态：
- 消耗：
- 持续/层数：
- 叠加：
- 退出/重置：
- UI：
- 与其他 Feature 的递归/冲突：
- 未给出的数值：

## Key 计划

| 逻辑 ID | 需要的游戏成员 | 当前证据 | 处理 |
|---|---|---|---|
| | | E0/E1/U | 实现/探针/降级 |

## 交付要求

- Core 代码与测试。
- GameInterop/Binding 变更。
- Feature 模块接线。
- 配置和日志。
- Feature 状态与 CHANGELOG。
- 自动测试结果。
- 游戏内手工测试步骤。
- 已知风险和回滚方式。

## 完成报告

```text
实现完成度：架构/纯逻辑/已编译/已运行/已验证
修改文件：
确认 Keys：
未知 Keys：
测试结果：
兼容风险：
存档风险：
需要用户执行的下一探针：
```

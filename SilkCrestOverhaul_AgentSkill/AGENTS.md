# AGENTS.md

本仓库的任何 Agent 在修改代码前，必须读取：

```text
skills/silksong-crest-overhaul/SKILL.md
```

并执行：

```bash
python skills/silksong-crest-overhaul/scripts/validate_contract.py .
```

核心约束：不得猜游戏内部 Key；不得用固定物理键实现玩家动作；反射集中在 GameInterop；换纹章、伤害、治疗、资源和无敌各自只有一个所有者；未经过本机编译/运行/探针验证的功能必须标记为未验证。

# Core tests

建议创建 xUnit/NUnit 项目，并优先覆盖：

- TimedStackCounter：阈值、刷新、到期、清零。
- DamagePipeline：顺序、暴击、追加伤害不递归。
- SilkShieldPolicy：0 丝、部分吸收、足额吸收、两格伤害。
- MarkedHealthPool：2/3 点累计转换、容量和余数。
- ResourceLedger：成功提交、失败、异常回滚。
- InputChordRouter：优先级、菜单禁用、同帧排他。
- InvulnerabilityLeaseService：重叠来源、超时和清空。

本环境未安装 .NET SDK，因此架构包未执行编译；本地 Agent 首个任务应是创建测试项目并在实际依赖版本下编译。

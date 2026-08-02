# 本地探针 Playbook

Agent 没有游戏时，用户负责采集证据。每个探针只解决一个逻辑绑定，避免一次导出整套 DLL。

## 1. 环境报告

保存到 `artifacts/environment-report.md`：

```text
游戏版本/构建号、商店、OS、位数、Mono/IL2CPP、Unity 版本
BepInEx/HarmonyX/Needleforge/FsmUtil/Prepatcher/AssetHelper/UnityHelper 版本
插件列表和配置列表
Assembly-CSharp.dll 文件版本、大小、SHA-256
测试存档与备份时间
```

## 2. 输入 Action 探针

目标：确定真实 ActionSet 类型、成员名和动作类型。

步骤：

1. 在 HeroController.Awake 后获取 `inputHandler` 和 `inputActions`。
2. 输出它们的完整类型名、程序集名。
3. 枚举 Public/NonPublic Instance 字段和可读属性：名称、类型、值类型。
4. 对看起来是 Action 的对象，记录是否有 `IsPressed/WasPressed/WasReleased`。
5. 用户依次修改键盘上下左右和手柄绑定，确认同一 Action 对象继续响应。
6. 记录菜单、过场、暂停时 Action 状态。
7. 输出 `artifacts/probes/input/input-actions-report.json`。

不要记录玩家隐私或系统级键盘输入，只记录游戏 Action 名和状态。

## 3. 冲刺探针

目标：找出冲刺开始/更新/结束、速度写入和状态清理链。

需要：

- HeroController 中包含 Dash/BackDash/Reset/Cancel 的方法签名。
- `cState` 类型和所有 bool 字段。
- 冲刺过程中每帧：dashing/backDashing/isDashStabBouncing/canControl、Rigidbody2D 速度。
- 正常冲刺结束时的调用顺序。
- 撞墙、空中、后冲、冲刺攻击的差异。
- 在 `ResetAllCrestState` 前后记录状态与速度。

输出：`artifacts/probes/crest-switch/dash-state-trace.csv` 和相关反编译片段。

## 4. 换纹章探针

目标：确认原版安全换纹章路径。

记录：

- 设置界面/椅子处换纹章时调用的完整方法链。
- `SetEquippedCrest` 前后 PlayerData 变化。
- `SendEquippedChangedEvent(bool)` 的参数语义和订阅者。
- `ResetAllCrestState` 实际清理的字段/FSM。
- 丝线、工具库存、血量、护符等级的原版保留规则。

优先复用原版完整换纹章入口；只有找不到时才组合低层方法。

## 5. 攻击与伤害探针

每种攻击生成一份：普通、上劈、下劈、冲刺、蓄力、工具、技能。

记录：

- 攻击动作开始入口。
- Hitbox/投射物创建。
- 敌人伤害接收入口和最终伤害字段。
- 攻击序列/多段命中标识。
- 暴击、元素、眩晕、击退的现有数据结构。
- 敌人死亡入口。

需要至少 30–80 行反编译上下文和一次运行日志。

## 6. 治疗与丝线探针

执行：正常治疗、丝线不足、治疗中受伤、空中尝试、二次吸血候选。

记录：

- 请求、预扣丝线、动画、完成、中断、退款。
- 治疗量和血量上限读取。
- 丝线获得/消费/钳制入口。
- FSM 状态序列和事件。

## 7. FSM 探针

每个技能包：

```text
scene
object path
FSM name
state sequence
input event
key variables
Action type list
animation clip
spawned object path
hitbox timing
sound/particle
```

状态名必须配 Action 指纹，防止本地化或版本变化。

## 8. 资源探针

优先 AssetHelper/AssetHelperMenu、FsmMaster、CUEP/UEP、UnityExplorer。记录 Bundle 路径与运行时路径的区别。

发布代码只保存资源 Key，不保存原版二进制资源。

## 9. 证据包结构

```text
artifacts/probes/<logical-id>/
├─ request.md
├─ environment.txt
├─ signatures.txt
├─ decompile-snippets.md
├─ call-chain.md
├─ runtime-log.txt
├─ state-trace.csv
├─ report.json
└─ screenshots/
```

## 10. Agent 收到证据后的动作

- 验证构建号与当前环境一致。
- 将 Key 升级为 E1，并写入 `game-key-registry.json`。
- 更新 `game-bindings.json`。
- 生成/更新自动测试和手工测试。
- 不删除旧构建绑定；保留为候选并标记版本范围。

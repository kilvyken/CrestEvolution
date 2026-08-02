---
name: silksong-crest-overhaul
description: 开发、整合、调试、测试和发布《空洞骑士：丝之歌》“纹章强化”大型 BepInEx Mod。凡任务涉及本仓库的纹章机制、快捷切换、原生输入、HeroController/ToolItemManager 绑定、Harmony/FSM/资源复用、伤害治疗丝线管线、存档安全或兼容性时，必须使用本 Skill。
---

# Silk Crest Overhaul Agent Skill

本 Skill 的目标不是让 Agent “尽可能多写代码”，而是让 Agent 在**没有游戏运行环境时也不胡猜，在拿到运行证据后能稳定完成绑定、实现和回归**。

## 1. 强制读取顺序

开始任何任务前，按顺序读取：

1. 本文件 `SKILL.md`。
2. `references/GAME_KEYS.md` 与 `references/game-key-registry.json`。
3. `references/CREST_SWITCH_SAFETY.md`。
4. 仓库根目录 `README.md`、`AGENTS.md`。
5. `docs/01_开发计划.md` 至 `docs/05_高难代码示例.md`。
6. `docs/03_功能整合.md` 中本次 Feature ID 对应段落。
7. `config/game-bindings.json`、最近一次 `binding-report.json`、`artifacts/environment-report.md`。
8. 本次允许修改的源文件与测试。

未完成读取，不得修改代码。

## 2. 事实优先级与证据等级

严格区分以下信息：

| 等级 | 含义 | 可以做什么 |
|---|---|---|
| E0 | 当前仓库源码中直接编译引用的类型、字段或方法 | 可作为当前代码基线，但仍需本地编译确认游戏版本兼容 |
| E1 | 用户本机反编译签名、运行时探针、FSM 导出或日志证据 | 可写入正式绑定并实现功能 |
| E2 | 官方 API 文档、官方包说明、官方仓库 | 可决定框架/API 的正确用法，不能替代游戏内部成员证据 |
| E3 | 其他 Mod 的公开源码、README、行为说明 | 只能参考设计和兼容策略；不能据此断言本游戏版本的内部 Key |
| U | 未验证候选或推断 | 只允许进入候选表、探针请求和兼容分支，不得写成唯一硬绑定 |

### 绝对规则

- 不得凭记忆发明游戏类型名、FSM 状态名、资源路径或输入 Action 名。
- “代码里出现过”不等于“当前游戏版本仍有效”；绑定报告必须记录程序集版本与证据。
- 无 E1 证据时，可以完成 Core、接口、测试、探针和降级逻辑，但不得声称游戏内功能已经完成。
- 外部参考项目没有明确许可证时，只总结行为，不复制代码。

## 3. 仓库边界

### 3.1 分层职责

```text
Core/          纯规则、数值、状态机、事务、计时器；禁止引用游戏类型
Features/      按 Feature ID 组合 Core 服务；禁止散落反射和对象搜索
GameInterop/   唯一允许接触游戏类、反射、FSM、Needleforge、Prepatcher 的层
Patches/       薄补丁，只采集事件、调整参数、提交结果；禁止承载完整玩法
UI/            只消费投影模型；禁止从 UI 反向控制规则状态
config/        版本相关绑定、功能开关、平衡参数
spec/docs/     唯一需求源、Feature ID 与人工验收规则
artifacts/     本机证据、探针、测试报告、绑定报告，不进入发布包
```

### 3.2 单一所有者

下列行为必须只有一个服务负责：

- 换纹章：`CrestSwitchService` 或当前兼容实现 `CrestQuickSwitchIntegration`。
- 丝线增减：`IGameApi` + `ResourceLedger`，Feature 不直接写 `playerData.silk`。
- 无敌：`InvulnerabilityLeaseService`，Feature 不直接开关布尔值。
- 伤害计算：`DamagePipeline`。
- 治疗与标记血转换：`HealthConversionService`。
- 虚拟工具消耗：`VirtualToolService` + 资源事务。
- 临时护符等级、生命/丝线上限：投影服务或 Prepatcher，禁止永久修改原始存档值。

若当前兼容代码尚未迁移到这些服务，本次修改必须减少直接访问，不能新增第二套所有者。

## 4. 硬性禁止项

除非任务明确是探针或兼容层，并在代码中写明原因，否则禁止：

- 在玩法输入中使用 `Input.GetKey*`、固定 `KeyCode`、`Input.GetAxis*`。
- 在 `Update`/`FixedUpdate` 中进行反射查找、LINQ 枚举、字符串扫描或 `FindObjectsOfType`。
- 在 Feature/UI/Core 中直接访问 `HeroController`、`PlayerData`、`ToolItemManager`、`Gameplay`。
- 在换纹章服务之外调用 `ResetAllCrestState`、`SetEquippedCrest`。
- 在 GameInterop 之外直接写 `playerData.silk`、生命上限、护符等级、工具库存。
- 使用无来源标签的追加伤害，或让燃爆/剑气默认递归重入完整伤害管线。
- 只用方法名选择第一个反射结果；多匹配必须拒绝并输出候选。
- 新增空 `catch { }`。兼容探针需要吞异常时，必须限频日志并记录失败成员。
- 在找不到绑定时阻止整个游戏启动。应只禁用依赖该绑定的 Feature ID。
- 发布原版游戏贴图、音效、动画或从第三方 Mod 复制的资源。

运行下面的检查器：

```bash
python skills/silksong-crest-overhaul/scripts/validate_contract.py .
```

## 5. 开发环境与依赖基线

以本机报告为最终真相。当前架构记录的编译目标是 `netstandard2.1`，并使用 BepInEx 5/HarmonyX。外部包版本只作为 2026-08-02 的核对基线：

- BepInExPack Silksong：运行时系列 `5.4.2304`。
- Needleforge：公开包 `0.9.0`，用于新增 Crest/Tool、BindFSM 注入、自定义动作集。
- Silksong.FsmUtil：`0.3.17`。
- Silksong.Prepatcher：`1.4.0`。
- Silksong.UnityHelper：`1.2.0`。
- Silksong.AssetHelper：`1.3.2`。

这些数字会变化。修改 `.csproj` 或 `thunderstore.toml` 前必须重新核对包页面、NuGet、实际插件目录和依赖 GUID。不得把网页“最新版本”直接写进项目而不进行本地编译与启动测试。

## 6. 游戏 Key 使用规范

完整表见 `references/GAME_KEYS.md`。任何 Key 都必须包含：

```text
逻辑 ID / 实际成员名 / 程序集 / 类型 / 签名 / 状态(E0/E1/U) / 游戏构建号 / 证据文件 / 最后验证日期
```

### 6.1 已在用户源码中出现的关键链

```text
HeroController.instance
HeroController.playerData
PlayerData.CurrentCrestID
PlayerData.silk
HeroController.ResetAllCrestState()
ToolItemManager.GetCrestByName(string)
ToolItemManager.SetEquippedCrest(string)
ToolItemManager.SendEquippedChangedEvent(bool)
ToolItemManager.TryReplenishTools(bool, ReplenishMethod)
```

它们属于 E0，而不是自动的 E1。正式接入时仍要记录本机反编译签名。

### 6.2 原生输入

玩法组合键必须读取游戏 Action 对象：

```text
HeroController.inputHandler -> inputActions
或 InputHandler.Instance -> inputActions
```

动作读取优先使用 `IsPressed`、`WasPressed`、`WasReleased`。这与 InControl `PlayerAction` 的公开 API 形状一致，但游戏实际类型必须由反编译/日志确认。

默认候选仅是 U：

```text
主功能键: taunt, ringTaunt, quickMap, dreamNail, inventory, cast
方向: up/moveUp/verticalUp/menuUp 等
向量: moveVector/movement/move/directionVector
```

找不到时：

1. 禁用该输入功能。
2. 输出 ActionSet 类型与可用成员。
3. 生成 `artifacts/probes/input-actions-report.json`。
4. 不回退到固定键位。

调试工具热键可以使用 BepInEx `KeyboardShortcut`，但必须可配置，并且不能承担玩家游戏动作。

## 7. 标准实现流程

### 7.1 新 Feature ID

1. 在 `docs/03_功能整合.md` 找到 Feature ID，摘录触发、消耗、持续、叠加、退出、UI、生命周期。
2. 建立验收表，所有缺失数值标记“待用户确认/待实测”，不擅自补值。
3. 判断是否已有共享系统；先扩共享系统，再写纹章模块。
4. 写纯逻辑测试，包括正常、边界、重复触发、重置、递归保护。
5. 若需要游戏 Key，先更新探针请求和绑定模板。
6. 完成 GameInterop 后，补充手工测试步骤与日志字段。
7. 更新 Feature 状态、CHANGELOG 和测试报告。

### 7.2 新游戏绑定

1. 定义稳定逻辑 ID，例如 `player.damage.before`，不要让 Core 使用真实方法名。
2. 收集 E1 证据：程序集、完整类型、完整签名、调用链、运行触发。
3. 在 `game-bindings.json` 写候选与签名约束。
4. Resolver 必须区分 `resolved/not-found/ambiguous/signature-mismatch`。
5. 缓存 FieldInfo/MethodInfo 或编译委托；不得每帧解析。
6. 绑定失败时只禁用相关 Feature。
7. 将结果写入 `binding-report.json`，包含游戏构建号和程序集哈希。

### 7.3 Harmony 补丁

优先顺序：原生事件/API > Postfix > Prefix > Finalizer > Transpiler。

- Postfix 用于发布最终事件或附加行为。
- Prefix 只在确实需要改参数、建立事务、取消原行为时使用。
- Prefix 返回 `false` 会跳过原方法，必须说明为什么不会破坏其他 Mod。
- Transpiler 必须有 IL 指纹、失败降级和至少一个兼容性测试。
- Patch 方法保持薄；完整规则交给服务。
- 所有 Patch 必须能由插件 GUID `UnpatchSelf()` 清理。

### 7.4 FSM 和原版动作

1. 先导出对象路径、FSM 名、状态序列、事件、变量和 Action 类型。
2. 使用对象路径 + FSM 名 + 状态动作指纹三重定位，不只匹配状态文字。
3. 优先插入旁路状态/事件，不整体替换 FSM。
4. 保存原始 Action 列表，功能禁用时可恢复。
5. 运行时从玩家本机资源克隆/池化，发布包不包含原版资源。
6. 资源缺失时降级为无特效玩法，不崩溃。

### 7.5 UI

- UI 订阅 `UiProjection`，不读取 Feature 私有字段。
- 每个投影有 `Revision`，只在变化时更新。
- 计时采用明确时钟：玩法通常 `Time.time`，暂停仍显示的 UI 可用 `unscaledTime`。
- 数字、图标、对象池不得每帧 Instantiate/Destroy。
- UI 丢失不能影响玩法状态机。

## 8. 换纹章与冲刺安全协议

任何换纹章实现必须遵循 `references/CREST_SWITCH_SAFETY.md`。摘要：

1. 输入只创建 `SwapRequest`，不得直接切换。
2. 记录请求来源：站立、攻击、蓄力、冲刺、后冲、空中、过场。
3. 等待控制权恢复、攻击/蓄力结束、冲刺结束并经过稳定帧窗口。
4. 冲刺来源请求在切换前清理冲刺状态和残余水平速度。
5. 通过唯一切换服务执行：快照 -> 暂停规则事件 -> 重置原纹章 -> 设置新纹章 -> 通知 -> 恢复允许保留资源 -> 发布统一事件。
6. 冲刺来源在切换后再进行若干帧恢复，防止游戏同帧重新写入速度/状态。
7. 异常时回滚或禁用快捷切换，不得让角色失去控制。

当前兼容代码直接保存/恢复 `playerData.silk`，只能视为迁移阶段。正式版本应由 `IGameApi`/资源账本完成快照，并发布丝线变化事件。

## 9. 生命周期与状态清理

每个 Feature 必须明确下面事件的行为：

| 事件 | 必须决定 |
|---|---|
| 换纹章 | 清理、暂停、持久保留或转换哪些状态 |
| 坐椅子 | 重置层数、修复工具、保存哪些解锁 |
| 受伤 | 是否中断、降层、刷新计时、触发护符 |
| 死亡 | 取消协程、租约、事务、对象池实例 |
| 复活 | 重新绑定 Hero/FSM/UI，不重复订阅 |
| 场景切换 | 清失效 Unity 引用，保留纯状态或按需求重置 |
| 退主菜单/读档 | 解除静态引用、重建存档上下文 |
| 配置热关闭 | 恢复原版行为，不残留 Patch/FSM 修改 |

不得用 `OnHardReset` 简单 `Deactivate/Activate` 代替所有生命周期语义；复杂模块应实现明确的 ResetReason。

## 10. 伤害、治疗和资源契约

### 伤害

- 每次原生攻击生成唯一 `AttackId` 和 `SequenceId`。
- 多段攻击使用 `HitIndex`，第一刀/保底暴击只按需求判定一次。
- 追加伤害默认 `ReenterPipeline=false`。
- 修饰器 ID 唯一、Order 固定、审计日志可重建计算过程。
- 最终伤害钳制和取整只做一次。

### 治疗

- 先建立 `HealTransaction`，再处理白血、溢出、红/绿/紫/蓝血和碎面甲次数。
- 中断、丝线不足、未命中必须回滚预留资源。
- 累计 2/3 点的转换使用整数余数，避免精度丢失。

### 资源

- 所有消耗先 Reserve，动作成功后 Commit，异常/失败自动 Refund。
- 虚拟工具不得改变原版库存。
- 建筑师弹夹切换必须保证念珠守恒。
- 临时上限投影不写入永久 PlayerData。

## 11. 测试矩阵

### 11.1 最低自动测试

- TimedStackCounter：刷新、过期、阈值、清零。
- DamagePipeline：顺序、暴击、追加伤害递归保护、多段攻击。
- ResourceLedger：成功、失败、异常回滚、重复 Dispose。
- HealthConversion：上限、余数、负数钳制、切换状态。
- InputChordRouter：同帧冲突、长短按、菜单禁用。
- CrestSwitch queue：冲刺/攻击请求、稳定帧、取消、覆盖策略。

### 11.2 快捷切换游戏内回归

至少覆盖：

- 键盘改键、手柄改键、D-pad、摇杆、手柄断开重连。
- 站立、奔跑、跳跃、下落、墙边、冲刺、后冲、冲刺攻击、蓄力、下劈。
- 冲刺撞墙、越过台阶、场景边缘、低帧率、时间减速。
- 主功能键本身的原版动作是否被误触发。
- 连续快速切换、目标锁定/未解锁/隐藏、切换同一纹章。
- 切换前后丝线、工具、血量、临时护符等级是否守恒。
- 与移动 Mod、UI Mod、Needleforge 自定义纹章同时启用。

每次测试保存日志时间段、视频、构建号、插件列表和配置。

## 12. 性能预算

- 常驻 Update 不得产生可观 GC 分配。
- 反射只在初始化、场景重绑或 ActionSet 改变时解析。
- 高频成员访问优先缓存委托/FieldRef；使用前验证结构体字段语义。
- 剑气、灵火、伤害数字、投射物使用对象池。
- 自动索敌使用 LayerMask 与空间查询，不全场扫描。
- 每 60 秒可选输出一次诊断摘要，禁止逐帧 Info 日志。

## 13. Agent 任务输出格式

每次完成任务必须输出：

```text
任务/Feature IDs：
修改文件：
证据等级：E0/E1/E2/E3/U
已确认的游戏 Keys：
仍未知的 Keys：
实现摘要：
测试：命令、结果、手工步骤
兼容与存档风险：
回滚方式：
下一步需要用户提供的探针资料：
```

代码未编译、未运行或绑定未验证时必须明确写出，禁止用“已完成”代替“架构已完成/待本机验证”。

## 14. 无法继续时

不要只回复“缺少 API”。生成一个完整探针请求，使用 `templates/agent-task.md` 和 `references/PROBE_PLAYBOOK.md`，至少说明：

- 需要验证的逻辑 ID。
- 候选类型/方法关键词。
- 需要在游戏内执行的动作。
- 需要导出的完整签名、调用链、FSM/对象路径或 ActionSet 成员。
- 证据拿到后会修改哪些文件。
- 不依赖该证据仍可完成的 Core/测试部分。

## 15. 参考来源

外部资料用于框架/API 约束，不替代本机游戏证据：

- BepInEx 官方文档：https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html
- Harmony 官方文档：https://harmony.pardeike.net/articles/patching.html
- Harmony Prefix：https://harmony.pardeike.net/articles/patching-prefix.html
- Harmony AccessTools：https://harmony.pardeike.net/articles/utilities.html
- Unity Rigidbody2D.linearVelocity：https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D-linearVelocity.html
- InControl PlayerAction API：https://www.gallantgames.com/incontrol-api/html/class_in_control_1_1_player_action.html
- Needleforge：https://thunderstore.io/c/hollow-knight-silksong/p/Voidlings/Needleforge/
- FsmUtil：https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/FsmUtil/
- Prepatcher：https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/SilksongPrepatcher/
- AssetHelper：https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/AssetHelper/
- UnityHelper：https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/UnityHelper/

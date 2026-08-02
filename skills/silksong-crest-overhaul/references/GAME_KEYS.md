# 游戏 Key 与绑定注册表

核对日期：2026-08-02。本文把“仓库中实际出现的标识”“外部 API”“尚未验证候选”分开记录。

## 1. 状态标记

- **E0**：上传源码/当前架构直接使用。
- **E1**：必须由本机反编译或运行探针确认后填写。
- **E2**：官方框架/API 文档。
- **U**：候选，不能作为唯一硬绑定。
- **CUSTOM**：本 Mod 自己定义的稳定 Key。

## 2. 插件与配置 Key

| Key | 当前值 | 状态 | 用途 |
|---|---:|---|---|
| Plugin GUID | `local.silkcrestoverhaul` | CUSTOM | Harmony owner、BepInEx 插件身份 |
| Plugin Name | `Silk Crest Overhaul` | CUSTOM | 显示名 |
| General.Enabled | `true` | CUSTOM | 总开关 |
| Debug.TraceEvents | `false` | CUSTOM | 事件追踪 |
| CrestQuickSwitch.Enabled | `true` | CUSTOM | 快捷切换开关 |
| CrestQuickSwitch.LongPressThresholdSeconds | `0.20` | CUSTOM | 长按判定 |
| CrestQuickSwitch.SpecialPressThresholdSeconds | `3.0` | CUSTOM | 无方向特殊切换 |
| CrestQuickSwitch.CooldownSeconds | `0.30` | CUSTOM | 切换冷却 |
| CrestQuickSwitch.SwapInvulnerabilitySeconds | `0.10` | CUSTOM | 切换保护 |
| CrestQuickSwitch.DashSettleFrames | `3` | CUSTOM | 冲刺结束稳定帧 |
| CrestQuickSwitch.PostSwapRecoveryFrames | `2` | CUSTOM | 冲刺切换后恢复帧 |
| CrestQuickSwitch.QueenCrestId | `Yen` | CUSTOM/U | 自定义丝母/女王纹章 ID；注册端必须一致 |
| CrestQuickSwitch.MainActionCandidates | `taunt,ringTaunt,quickMap,dreamNail,inventory,cast` | U | 原生 Action 候选 |
| CrestQuickSwitch.ReplenishAllCrestsAtBench | `false` | CUSTOM | 旧兼容行为，正式版应由虚拟库存替代 |
| CrestQuickSwitch.VerboseLogging | `false` | CUSTOM | 限频诊断 |

## 3. 游戏类型与成员（来自上传源码）

下表只证明旧/当前源码引用过这些名称。发布前应升级为 E1，记录本机完整签名。

| 逻辑用途 | 标识 | 状态 | 约束/风险 |
|---|---|---|---|
| 玩家单例 | `HeroController.instance` | E0 | 场景/复活后可能更换实例，不能永久缓存 |
| 玩家数据 | `HeroController.playerData` | E0 | 只允许 GameInterop 访问 |
| 当前纹章 | `PlayerData.CurrentCrestID` | E0 | 应由统一 CrestChanged 事件同步 |
| 丝线 | `PlayerData.silk` | E0 | 直接写会绕过事件/钳制；迁移到 IGameApi |
| 重置纹章状态 | `HeroController.ResetAllCrestState()` | E0 | 高风险，只能由切换事务调用 |
| 查找纹章 | `ToolItemManager.GetCrestByName(string)` | E0 | 目标可能未解锁/隐藏/不存在 |
| 装备纹章 | `ToolItemManager.SetEquippedCrest(string)` | E0 | 必须配合通知和状态事务 |
| 装备变化通知 | `ToolItemManager.SendEquippedChangedEvent(bool)` | E0 | 参数语义需 E1 确认 |
| 工具补给 | `ToolItemManager.TryReplenishTools(bool, ReplenishMethod)` | E0 | 坐椅子遍历临时切纹章风险高 |
| 纹章类型 | `ToolCrest` | E0 | `name/IsUnlocked/IsHidden` 在源码中使用 |

## 4. 原版/自定义纹章 Key

| 概念 | 当前访问方式 | 状态 |
|---|---|---|
| 建筑师/工具大师 | `Gameplay.ToolmasterCrest.name` | E0 |
| 丝母/女王 | 配置 `QueenCrestId = Yen` | CUSTOM/U |
| 萨满/法术 | `Gameplay.SpellCrest.name` | E0 |
| 猎手 | `Gameplay.HunterCrest`, `HunterCrest2`, `HunterCrest3` | E0 |
| 收割者 | `Gameplay.ReaperCrest.name` | E0 |
| 野兽/战士 | `Gameplay.WarriorCrest.name` | E0；中文概念映射需项目统一 |
| 漫游者 | `Gameplay.WandererCrest.name` | E0 |
| 女巫 | `Gameplay.WitchCrest.name` | E0 |
| 诅咒 | `Gameplay.CursedCrest.name` | E0 |
| 无披风/特殊 | `Gameplay.CloaklessCrest.name` | E0 |

### 快捷切换默认映射

```text
上短按: Toolmaster     上长按: Queen/Silk Mother
下短按: Spell          下长按: Hunter best unlocked
左短按: Reaper         左长按: Warrior/Beast
右短按: Wanderer       右长按: Witch
无方向长按 3 秒: Cursed <-> Cloakless
```

这只是当前产品设计，不是游戏固定规则。应通过配置或数据表表达，避免写死在输入类。

## 5. 原生输入 Key

### 5.1 访问链候选

| 层级 | 候选 | 状态 |
|---|---|---|
| 玩家输入处理器 | `HeroController.inputHandler` / `InputHandler` | E0/U |
| 全局输入处理器 | `InputHandler.Instance` / `instance` | U |
| 动作集合 | `inputActions` / `InputActions` / `actions` / `Actions` | E0/U |

### 5.2 Action 状态成员

| 语义 | 候选 | 证据 |
|---|---|---|
| 当前按住 | `IsPressed` / `isPressed` | E0，且符合 InControl E2 API |
| 本帧按下 | `WasPressed` / `wasPressed` | E0，且符合 InControl E2 API |
| 本帧释放 | `WasReleased` / `wasReleased` | E0，且符合 InControl E2 API |

### 5.3 动作名称候选

| 用途 | 候选 | 状态 |
|---|---|---|
| 主修饰键 | `taunt`, `ringTaunt`, `quickMap`, `dreamNail`, `inventory`, `cast` | U |
| 上 | `up`, `moveUp`, `verticalUp`, `menuUp` | U |
| 下 | `down`, `moveDown`, `verticalDown`, `menuDown` | U |
| 左 | `left`, `moveLeft`, `horizontalLeft`, `menuLeft` | U |
| 右 | `right`, `moveRight`, `horizontalRight`, `menuRight` | U |
| 移动向量 | `moveVector`, `movement`, `move`, `directionVector` | U |
| 向量值 | `Vector`, `Value`, `RawValue`, `Direction`; 或 `ReadValue/GetValue/GetVector()` | U |

必须输出实际 ActionSet 成员并升级到 E1。不得使用 `KeyboardBindingCache`、`PlayerPrefs KeyUp`、`Input.GetAxisRaw("Horizontal")` 代替游戏 Action。

## 6. Hero 状态和方法候选

| 用途 | 候选 | 状态 | 说明 |
|---|---|---|---|
| 状态容器 | `cState`, `CState`, `heroState` | E0/U | 类型本身需记录 |
| 攻击 | `attacking` | E0 |
| 上攻击 | `upAttacking` | E0 |
| 下攻击 | `downAttacking` | E0 |
| 蓄力 | `nailCharging`, `chargingAttack` | E0/U |
| 冲刺 | `dashing`, `isDashing` | E0/U |
| 后冲 | `backDashing`, `isBackDashing` | E0/U |
| 可控制 | `canControl` | E0 |
| 接受输入 | `acceptingInput` | E0 |
| 控制权被收回 | `controlRelinquished` | E0 |
| 冲刺刺反弹 | `isDashStabBouncing` | E0 |
| 加无敌 | `AddInvincibleTime(float)` | E0 |
| 无敌计时器 | `invincibleTimer` | E0 |
| 重置攻击冲刺 | `ResetAttacksDash()` | E0 |
| 重置攻击 | `ResetAttacks(bool)` | E0 |
| 结束冲刺 | `CancelDash/StopDash/EndDash/DashEnd/ResetDash` | U |

禁止仅因为候选方法存在就同时调用全部方法。E1 探针需确认每个方法副作用，正式实现只调用最小充分集合。

## 7. 物理 Key

| 语义 | API | 状态 |
|---|---|---|
| 玩家 2D 刚体 | `hero.GetComponent<Rigidbody2D>()` | E0 |
| Unity 6 速度 | `Rigidbody2D.linearVelocity` | E2/E0 候选 |
| 旧版速度 | `Rigidbody2D.velocity` | E2/E0 兼容候选 |

冲刺软锁修复只归零水平分量并保留 Y：`new Vector2(0, old.y)`。但在移动平台、击退、风场等场景可能不应归零；必须只对“冲刺来源的换纹章事务”执行，并纳入回归测试。

## 8. 架构逻辑绑定 ID

当前 `game-bindings.json` 已定义：

```text
player.attack.resolved
player.damage.before
player.heal.request
```

建议补齐：

```text
player.damage.after
player.heal.resolved
player.silk.get
player.silk.spend
player.crest.get
player.crest.set
player.crest.changed
player.control.can_accept
player.dash.active
player.dash.cancel
player.bench.rested
player.died
enemy.damage.before
enemy.damage.after
enemy.killed
tool.use.request
tool.use.resolved
tool.replenish
scene.changed
ui.health.project
ui.silk.project
```

逻辑 ID 稳定，真实方法候选可随游戏版本变化。

## 9. 事件与来源标签

### 事件

```text
AttackResolved
PlayerDamageBefore / PlayerDamageAfter
HealRequested / HealResolved
SilkChanged
CrestChanged
EnemyKilled
BenchRested
PlayerDied
SceneChanged
```

### 推荐 SourceTag 命名

```text
crest.hunter.normal
crest.hunter.super-rage
crest.reaper.fire-passive
crest.witch.dash
skill.guardian-sword-wave
tool.sting-shard
additional.explosion
system.crest-switch
system.silk-shield
```

规则：小写、点分层级、稳定、不包含本地化文本。追加伤害必须有 `additional.*` 或明确来源，并携带 `ReenterPipeline`。

## 10. Feature ID 前缀

| 前缀 | 模块 |
|---|---|
| HUN | 猎手 |
| REA | 收割者 |
| WAN | 漫游者 |
| BEA | 野兽 |
| WIT | 女巫 |
| ARC | 建筑师 |
| SHA | 萨满 |
| MOT | 丝母纹章 |
| CUR | 诅咒纹章 |
| USE | 无用之人 |
| EXP | 后续修复/扩展 |
| ULT | 大招/强化槽 |

Feature ID 永不复用。需求修改保留旧 ID 状态并新增 ID。

## 11. 当前必须保持未知的 Key

以下信息在上传文档中没有得到实际签名支持，Agent 不得自行填写：

- 攻击开始、Hitbox 生成、最终敌人伤害入口。
- 玩家受伤前后、治疗请求/完成/中断的真实方法。
- 丝线获得、消费、上限查询的真实入口。
- Bench、死亡、复活、读档、场景切换的精确回调。
- 护符/工具内部 ID、等级、槽位颜色和库存字段。
- 每个技能的 FSM、状态、事件、变量和原版资源路径。
- 原版 UI 层级和绑定字段。

拿不到 E1 证据时，生成探针，不得用名字相近的第一个方法代替。

## 12. 外部包核对表（2026-08-02）

| 包 | 核对版本 | 作用 | 注意 |
|---|---:|---|---|
| BepInExPack Silksong | 5.4.2304 | Loader/runtime | 项目 PackageReference 与运行时包版本不是同一命名方式 |
| Needleforge | 0.9.0 | Crest/Tool/动作集 | 公开说明称红色投射工具仍有限制；先做适配层 |
| FsmUtil | 0.3.17 | FSM 编辑 | BepInDependency GUID 需按包文档 |
| SilksongPrepatcher | 1.4.0 | PlayerData Get/Set 投影 | 适合临时值，不把临时状态写进存档 |
| UnityHelper | 1.2.0 | Unity 对象辅助 | 是否硬依赖由实际用量决定 |
| AssetHelper | 1.3.2 | 加载原版资源 | 当前依赖包含 MonoDetour BepInEx 5 与 I18N |

每次发布前重新核对，不自动升级。

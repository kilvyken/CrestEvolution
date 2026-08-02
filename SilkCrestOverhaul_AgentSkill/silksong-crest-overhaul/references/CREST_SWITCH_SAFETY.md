# 换纹章与冲刺软锁安全协议

本协议解决“冲刺时切换纹章后角色持续向前移动、失去正常控制或动作状态卡死”。它也适用于攻击、蓄力和 FSM 动作中切换。

## 1. 根因模型

当前源码显示：纹章切换会调用 `ResetAllCrestState()`、`SetEquippedCrest()` 和装备变化通知。若在冲刺 FSM/控制器完成自己的退出逻辑前重置纹章，原动作可能失去清除速度、碰撞状态或输入锁的机会。即使 `dashing` 已变为 false，`Rigidbody2D` 的水平速度或下一帧动作写入仍可能残留。

因此“检查 dashing=false 后立刻换”不够。

## 2. 事务状态机

```text
Idle
  -> Requested
  -> WaitingUnsafeStateExit
  -> WaitingStableFrames
  -> PreNormalize (only dash-origin)
  -> Snapshot
  -> ApplyCrest
  -> Notify
  -> RestoreAllowedState
  -> PostNormalizeFrames (only dash-origin)
  -> VerifyControl
  -> Completed
  -> Rollback/Disabled on failure
```

## 3. SwapRequest 必需字段

```csharp
record SwapRequest(
    long RequestId,
    string FromCrest,
    string ToCrest,
    SwapOrigin Origin,
    int RequestedFrame,
    double RequestedUnscaledTime,
    int EarliestFrame,
    bool PreserveSilk,
    bool PreserveMarkedHealth,
    string Reason);
```

`SwapOrigin` 至少包括：Standing、Moving、Airborne、Attack、Charge、Dash、BackDash、DashAttack、Dive、Cutscene、Unknown。

同一时间只允许一个请求。新请求覆盖还是排队必须是明确配置；默认最后一次有效方向覆盖旧目标，但不得丢失 `Origin=Dash` 标记。

## 4. 安全条件

提交切换前必须同时满足：

- Hero 实例和 playerData 有效。
- 非菜单、过场、坐椅子、读档或控制权被收回。
- 不在攻击、上/下攻击、蓄力。
- 不在冲刺、后冲、冲刺刺反弹。
- 距最后一个冲刺活动帧至少 `DashSettleFrames`。
- 距最后一个不安全帧至少 1 帧；复杂动作可提高。
- 目标纹章存在、已解锁、未隐藏，除非目标是本 Mod 明确允许的自定义纹章。

若请求超时（建议 1–2 秒）仍不安全，应取消并警告，不在过场结束后突然切换。

## 5. 冲刺前置归一化

只对 Dash-origin 请求执行：

1. 调用经 E1 验证的最小冲刺结束方法。
2. 清除已确认的冲刺标志。
3. 清除 `isDashStabBouncing`（若存在且语义确认）。
4. 获取玩家 Rigidbody2D。
5. 仅将水平速度归零，保留 Y。
6. 不修改重力、碰撞层、朝向或平台附着，除非探针证明需要。

不要无条件依次调用 `CancelDash/StopDash/EndDash/ResetDash` 全部候选。先用反编译确认调用链，选择一个主方法；字段清理只作为版本兼容后备。

## 6. 切换事务顺序

推荐：

```text
A. 进入 reentrancy guard
B. 创建允许保留状态的快照
C. 暂停本 Mod 的 CrestChanged/资源事件重入
D. 结束旧纹章模块的临时状态
E. 调用 ResetAllCrestState（若 E1 证明必要）
F. SetEquippedCrest
G. SendEquippedChangedEvent
H. 恢复允许保留的资源，必须走 IGameApi/事务
I. 发布本 Mod 唯一 CrestChangedEvent
J. 激活新纹章模块
K. 释放事件暂停与 guard
```

发生异常：

- 尝试恢复原纹章和快照。
- 若恢复失败，禁用快捷切换并记录完整日志，不继续重复切换。
- 不吞掉异常信息。

## 7. 可保留/不可保留状态

默认可保留：

- 当前白血和普通游戏资源（按游戏原生切换语义确认）。
- 丝线，仅当设计明确且通过资源服务恢复。
- 文档明确写“切换纹章不退出”的超级状态。

默认不可保留：

- 旧纹章普通强化层数、计时器和专属输入模式。
- 旧纹章的无敌 lease。
- 未提交资源事务。
- 旧纹章生成、且应随纹章退出的投射物/UI。

标记血、蓝血、碎面甲次数必须按 Feature 逐项定义，不能统一猜测。

## 8. 切换后恢复

Dash-origin 切换后连续 `PostSwapRecoveryFrames`：

- 检查冲刺标志是否被同帧/次帧重新写回。
- 必要时再次执行最小归一化。
- 检查 `HasControl`。
- 若玩家主动重新发起冲刺，不得错误归零新冲刺；需要请求帧/动作序列 ID 或输入边沿判断。

当前简单实现连续归零 X 速度存在一个风险：玩家在恢复窗口内主动输入移动也会被压制。因此正式版应优先检测“旧冲刺序列仍在写状态”，而不是盲目固定 N 帧归零。拿不到序列 ID 时，把恢复窗口保持在 1–2 帧并进行大量实测。

## 9. 输入和原动作冲突

主修饰 Action 可能同时触发原版 quickMap/inventory/cast。必须选择一种策略：

- 使用游戏中已有但无冲突的 Action；或
- 在组合键成功识别后，通过经验证的输入消费/动作取消机制阻止原动作；或
- 提供独立的可重绑定 Mod Action，并在设置菜单中注册。

不能只读取原生 Action 然后忽略原版动作副作用。

## 10. 回归矩阵

| 场景 | 预期 |
|---|---|
| 地面冲刺中请求 | 冲刺正常结束后切换，不持续滑行 |
| 空中冲刺中请求 | 保留合理 Y 速度，不悬空/坠落异常 |
| 后冲 | 不反向持续移动 |
| 冲刺攻击 | 等攻击状态与冲刺状态都结束 |
| 撞墙/台阶 | 不穿墙、不丢碰撞、不持续顶墙 |
| 移动平台 | 不破坏平台附着或继承速度 |
| 风场/水流 | 不把环境水平速度误判为冲刺残留 |
| 低 FPS | 稳定帧逻辑按帧和超时双重约束 |
| 时间减速 | 输入长按用 unscaledTime；玩法动作按游戏时间 |
| 场景切换 | 队列取消，不在新场景突然切换 |
| 死亡/复活 | 队列和恢复窗口清空 |
| 玩家在恢复窗口再次冲刺 | 新动作不得被旧事务取消 |

## 11. 日志字段

```text
requestId, from, to, reason, origin,
requestFrame, commitFrame, lastDashFrame, settleFrames,
unsafeFlags, velocityBefore, velocityAfter,
stateFlagsBefore, stateFlagsAfter,
controlBefore, controlAfter,
rollback, exception
```

Verbose 关闭时只记录提交、取消、超时和错误；禁止逐帧 Info。

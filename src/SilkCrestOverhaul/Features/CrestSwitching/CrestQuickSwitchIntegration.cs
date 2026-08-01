using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using GlobalSettings;

namespace SilkCrestOverhaul.Features.CrestSwitching;

/// <summary>
/// 将原独立 CrestKeyDirectionsV5 合并为 SilkCrestOverhaul 的内部功能。
///
/// 设计目标：
/// 1. 本文件不是独立 BepInEx 插件，不声明 BepInPlugin，生命周期由 ModBootstrap 管理。
/// 2. 主功能键、上下左右全部读取游戏 inputActions，尊重游戏内重新绑定和手柄映射；
///    反射失败时禁用输入，不再退回固定 KeyCode 或 Unity Horizontal/Vertical 轴。
/// 3. 冲刺期间只记录切换请求；等冲刺结束并经过安全帧窗口后再切换。
///    对来自冲刺的请求，在切换前后清除冲刺标志并归零残留水平速度，避免角色持续向前滑行。
///
/// ModBootstrap 中的接入方式：
///     private CrestQuickSwitchIntegration? _crestQuickSwitch;
///     _crestQuickSwitch = CrestQuickSwitchIntegration.Install(_log, _config, _harmony);
///     ...
///     _crestQuickSwitch?.Dispose();
///
/// 相关公开项目（仅作设计参考，不复制代码）：
/// - Bergbok/Silksong-Mods：GunZ 模块处理动作取消；
/// - Voidlings/Needleforge：新增纹章和动作注入 API；
/// - MCXGK3/KnightInSilkSong：角色能力同步及软锁修复经验。
/// </summary>
public sealed class CrestQuickSwitchIntegration : IDisposable
{
    private const int UpMask = 1;
    private const int DownMask = 2;
    private const int LeftMask = 4;
    private const int RightMask = 8;

    private static CrestQuickSwitchIntegration? _instance;

    private readonly ManualLogSource _log;
    private readonly Harmony _harmony;
    private readonly NativeInputAdapter _input;
    private readonly HeroReflectionCache _heroReflection;

    private readonly ConfigEntry<bool> _enabled;
    private readonly ConfigEntry<float> _longPressThreshold;
    private readonly ConfigEntry<float> _specialPressThreshold;
    private readonly ConfigEntry<float> _cooldownSeconds;
    private readonly ConfigEntry<float> _invulnerabilitySeconds;
    private readonly ConfigEntry<int> _dashSettleFrames;
    private readonly ConfigEntry<int> _postSwapRecoveryFrames;
    private readonly ConfigEntry<string> _queenCrestId;
    private readonly ConfigEntry<string> _mainActionCandidates;
    private readonly ConfigEntry<bool> _replenishAllCrestsAtBench;
    private readonly ConfigEntry<bool> _verboseLogging;

    private HeroController? _hero;
    private bool _disposed;
    private bool _isApplyingSwap;
    private bool _suppressInput;

    private float _nextSwapTime;
    private float _mainKeyDownTime = -1f;
    private int _lastChosenDirMask;
    private bool _swappedThisPress;
    private bool _specialTriggeredThisPress;
    private int _lastTriggeredDirMask;
    private bool _lastTriggeredLong;
    private bool _previousMainHeld;
    private int _previousDirectionMask;

    private bool _hasQueuedSwap;
    private string? _queuedTargetName;
    private string? _queuedReason;
    private int _queuedEarliestFrame = -1;
    private bool _queuedFromDash;

    private int _lastDashFrame = int.MinValue / 2;
    private int _lastUnsafeFrame = int.MinValue / 2;
    private int _postSwapRecoveryUntilFrame = -1;
    private bool _postSwapWasFromDash;

    private CrestQuickSwitchIntegration(
        ManualLogSource log,
        ConfigFile config,
        Harmony harmony)
    {
        _log = log;
        _harmony = harmony;
        _heroReflection = new HeroReflectionCache(log);
        _input = new NativeInputAdapter(log);

        _enabled = config.Bind(
            "CrestQuickSwitch",
            "Enabled",
            true,
            "Enable directional crest quick switching.");

        _longPressThreshold = config.Bind(
            "CrestQuickSwitch",
            "LongPressThresholdSeconds",
            0.20f,
            "Holding the main action for at least this duration selects the secondary crest.");

        _specialPressThreshold = config.Bind(
            "CrestQuickSwitch",
            "SpecialPressThresholdSeconds",
            3.0f,
            "Holding the main action without a direction toggles Cursed/Cloakless crest.");

        _cooldownSeconds = config.Bind(
            "CrestQuickSwitch",
            "CooldownSeconds",
            0.30f,
            "Minimum time between two crest switches.");

        _invulnerabilitySeconds = config.Bind(
            "CrestQuickSwitch",
            "SwapInvulnerabilitySeconds",
            0.10f,
            "Short protection applied when a queued switch is committed.");

        _dashSettleFrames = config.Bind(
            "CrestQuickSwitch",
            "DashSettleFrames",
            3,
            "Frames to wait after the final dash frame before changing crest. Increase to 4-5 if another movement mod still leaves dash velocity behind.");

        _postSwapRecoveryFrames = config.Bind(
            "CrestQuickSwitch",
            "PostSwapRecoveryFrames",
            2,
            "Frames after a dash-origin switch during which stale dash flags and horizontal velocity are cleared.");

        _queenCrestId = config.Bind(
            "CrestQuickSwitch",
            "QueenCrestId",
            "Yen",
            "Internal ID/name used by the custom Silk Mother/Queen crest.");

        _mainActionCandidates = config.Bind(
            "CrestQuickSwitch",
            "MainActionCandidates",
            "taunt,ringTaunt,quickMap,dreamNail,inventory,cast",
            "Comma-separated game inputActions member names. The first existing action is used as the quick-switch modifier.");

        _replenishAllCrestsAtBench = config.Bind(
            "CrestQuickSwitch",
            "ReplenishAllCrestsAtBench",
            false,
            "Legacy behavior from the standalone mod. Disabled by default because the overhaul should eventually replenish virtual tool inventories without temporarily changing crests.");

        _verboseLogging = config.Bind(
            "CrestQuickSwitch",
            "VerboseLogging",
            false,
            "Write input binding, queue, dash recovery, and switch diagnostics.");
    }

    public static CrestQuickSwitchIntegration Install(
        ManualLogSource log,
        ConfigFile config,
        Harmony harmony)
    {
        if (_instance != null) return _instance;

        var feature = new CrestQuickSwitchIntegration(log, config, harmony);
        _instance = feature;

        harmony.CreateClassProcessor(typeof(HeroAwakePatch)).Patch();
        harmony.CreateClassProcessor(typeof(HeroUpdatePatch)).Patch();
        harmony.CreateClassProcessor(typeof(BenchReplenishPatch)).Patch();

        feature.AttachHero(HeroController.instance);
        log.LogInfo("Crest quick switch integrated into Silk Crest Overhaul.");
        return feature;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _input.Reset();
        _hero = null;
        _instance = null;
    }

    private void AttachHero(HeroController? hero)
    {
        if (_disposed || hero == null) return;
        if (ReferenceEquals(_hero, hero)) return;

        _hero = hero;
        _heroReflection.Initialize(hero);
        _input.Bind(hero, ParseCandidates(_mainActionCandidates.Value));
        ResetPressState();
        _previousMainHeld = false;
        _previousDirectionMask = 0;

        Trace("Attached HeroController and refreshed native input bindings.");
    }

    private void OnHeroUpdate(HeroController hero)
    {
        if (_disposed || !_enabled.Value) return;
        AttachHero(hero);

        if (_hero == null || _hero.playerData == null) return;

        _input.RefreshIfActionSetChanged(_hero, ParseCandidates(_mainActionCandidates.Value));
        TrackUnsafeFrames();
        RunPostSwapRecovery();
        TryConsumeQueuedSwap();

        if (_suppressInput || _isApplyingSwap) return;
        if (!_input.IsReady) return;

        InputFrame frame = _input.ReadFrame();
        ProcessInputFrame(frame);
    }

    private void ProcessInputFrame(InputFrame frame)
    {
        bool mainDown = frame.MainPressed || (frame.MainHeld && !_previousMainHeld);
        bool mainUp = frame.MainReleased || (!frame.MainHeld && _previousMainHeld);
        int downMask = frame.DirectionMask & ~_previousDirectionMask;

        _previousMainHeld = frame.MainHeld;
        _previousDirectionMask = frame.DirectionMask;

        if (mainDown)
        {
            _mainKeyDownTime = Time.unscaledTime;
            ResetPressState();
        }

        if (mainUp || (_mainKeyDownTime >= 0f && !frame.MainHeld))
        {
            _mainKeyDownTime = -1f;
            ResetPressState();
            return;
        }

        if (!frame.MainHeld) return;
        if (_mainKeyDownTime < 0f) _mainKeyDownTime = Time.unscaledTime;

        int chosenDir = ChooseDirection(frame.DirectionMask, downMask);
        float holdDuration = Time.unscaledTime - _mainKeyDownTime;

        if (chosenDir == 0 && !_specialTriggeredThisPress &&
            holdDuration >= Math.Max(0.1f, _specialPressThreshold.Value))
        {
            HandleSpecialSwap();
            _specialTriggeredThisPress = true;
            _nextSwapTime = Time.unscaledTime + Math.Max(0f, _cooldownSeconds.Value);
            return;
        }

        if (chosenDir == 0 || Time.unscaledTime < _nextSwapTime) return;

        bool isLong = holdDuration >= Math.Max(0.01f, _longPressThreshold.Value);
        bool directionChanged = chosenDir != _lastTriggeredDirMask;
        bool modeChangedSameDirection = !directionChanged && isLong != _lastTriggeredLong;
        bool shouldTrigger = directionChanged || (!_swappedThisPress && modeChangedSameDirection);
        if (!shouldTrigger) return;

        PerformDirectionalSwap(chosenDir, isLong);
    }

    private void ResetPressState()
    {
        _lastChosenDirMask = 0;
        _lastTriggeredDirMask = 0;
        _lastTriggeredLong = false;
        _swappedThisPress = false;
        _specialTriggeredThisPress = false;
    }

    private void HandleSpecialSwap()
    {
        if (_hero?.playerData == null) return;

        string current = _hero.playerData.CurrentCrestID;
        string? cursed = SafeCrestName(() => Gameplay.CursedCrest);
        string cloakless = SafeCrestName(() => Gameplay.CloaklessCrest) ?? "Cloakless Crest";
        if (string.IsNullOrWhiteSpace(cursed)) return;

        RequestSwap(current == cursed ? cloakless : cursed, "SPECIAL_3S");
    }

    private void PerformDirectionalSwap(int directionMask, bool isLong)
    {
        if (_hero?.playerData == null) return;

        string current = _hero.playerData.CurrentCrestID;
        string? target = ResolveDirectionTarget(current, directionMask, isLong);
        if (string.IsNullOrWhiteSpace(target) || target == current) return;

        RequestSwap(target, isLong ? "LONG" : "SHORT");
        _lastTriggeredDirMask = directionMask;
        _lastTriggeredLong = isLong;
        _swappedThisPress = true;
        _nextSwapTime = Time.unscaledTime + Math.Max(0f, _cooldownSeconds.Value);
    }

    private void RequestSwap(string targetName, string reason)
    {
        if (_hero?.playerData == null || string.IsNullOrWhiteSpace(targetName)) return;

        bool dashActive = _heroReflection.IsDashLikeActive(_hero);
        bool safe = IsSafeToSwapNow();
        bool dashSettled = HasDashSettled();

        if (safe && dashSettled)
        {
            ApplySwapNow(targetName, reason, fromDash: false);
            return;
        }

        _hasQueuedSwap = true;
        _queuedTargetName = targetName;
        _queuedReason = reason;
        _queuedEarliestFrame = Math.Max(Time.frameCount + 1, _lastUnsafeFrame + 1);
        _queuedFromDash |= dashActive || Time.frameCount - _lastDashFrame <= Math.Max(1, _dashSettleFrames.Value);

        Trace($"Queued crest switch -> {targetName}; reason={reason}; dash={_queuedFromDash}; earliestFrame={_queuedEarliestFrame}");
    }

    private void TryConsumeQueuedSwap()
    {
        if (!_hasQueuedSwap || _hero?.playerData == null) return;
        if (Time.frameCount < _queuedEarliestFrame) return;
        if (!IsSafeToSwapNow() || !HasDashSettled()) return;

        string? target = _queuedTargetName;
        if (string.IsNullOrWhiteSpace(target) || target == _hero.playerData.CurrentCrestID)
        {
            ClearQueuedSwap();
            return;
        }

        bool fromDash = _queuedFromDash;
        string reason = (_queuedReason ?? "QUEUED") + (fromDash ? "_DASH_SAFE" : "_SAFE");
        ApplySwapNow(target, reason, fromDash);
        ClearQueuedSwap();
    }

    private void ClearQueuedSwap()
    {
        _hasQueuedSwap = false;
        _queuedTargetName = null;
        _queuedReason = null;
        _queuedEarliestFrame = -1;
        _queuedFromDash = false;
    }

    private void TrackUnsafeFrames()
    {
        if (_hero == null) return;

        bool dash = _heroReflection.IsDashLikeActive(_hero);
        if (dash) _lastDashFrame = Time.frameCount;

        if (dash || _heroReflection.IsAttackOrChargeActive(_hero) || !_heroReflection.HasControl(_hero))
            _lastUnsafeFrame = Time.frameCount;
    }

    private bool HasDashSettled()
    {
        int requiredFrames = Math.Max(1, _dashSettleFrames.Value);
        return Time.frameCount - _lastDashFrame >= requiredFrames;
    }

    private bool IsSafeToSwapNow()
    {
        if (_hero == null || _hero.playerData == null) return false;
        if (_heroReflection.IsDashLikeActive(_hero)) return false;
        if (_heroReflection.IsAttackOrChargeActive(_hero)) return false;
        if (!_heroReflection.HasControl(_hero)) return false;
        return true;
    }

    private void ApplySwapNow(string targetName, string reason, bool fromDash)
    {
        if (_isApplyingSwap || _hero?.playerData == null) return;
        _isApplyingSwap = true;

        try
        {
            ToolCrest target = ToolItemManager.GetCrestByName(targetName);
            if (target == null)
            {
                _log.LogWarning($"Crest quick switch target not found: {targetName}");
                return;
            }

            int silkBefore = _hero.playerData.silk;

            if (fromDash)
            {
                // 关键修复：不要只清 cState.dashing。冲刺状态结束后 Rigidbody2D 的 X 速度
                // 仍可能残留，ResetAllCrestState/换纹章会让负责收尾的原动作逻辑失去机会。
                _heroReflection.NormalizeDashExit(_hero, zeroHorizontalVelocity: true);
            }

            _heroReflection.ApplyInvincibility(_hero, Math.Max(0f, _invulnerabilitySeconds.Value));

            _hero.ResetAllCrestState();
            ToolItemManager.SetEquippedCrest(target.name);
            ToolItemManager.SendEquippedChangedEvent(true);
            _hero.playerData.silk = silkBefore;

            if (fromDash)
            {
                _postSwapWasFromDash = true;
                _postSwapRecoveryUntilFrame = Time.frameCount + Math.Max(1, _postSwapRecoveryFrames.Value);
                _heroReflection.NormalizeDashExit(_hero, zeroHorizontalVelocity: true);
            }

            _log.LogInfo($"Crest switched -> {target.name} [{reason}]");
        }
        catch (Exception ex)
        {
            _log.LogError($"Crest switch failed: target={targetName}, reason={reason}, error={ex}");
        }
        finally
        {
            _isApplyingSwap = false;
        }
    }

    private void RunPostSwapRecovery()
    {
        if (!_postSwapWasFromDash || _hero == null) return;
        if (Time.frameCount > _postSwapRecoveryUntilFrame)
        {
            _postSwapWasFromDash = false;
            return;
        }

        _heroReflection.NormalizeDashExit(_hero, zeroHorizontalVelocity: true);
        Trace($"Dash post-swap recovery frame {Time.frameCount}/{_postSwapRecoveryUntilFrame}");
    }

    private string? ResolveDirectionTarget(string current, int directionMask, bool isLong)
    {
        string? toolmaster = SafeCrestName(() => Gameplay.ToolmasterCrest);
        string queen = _queenCrestId.Value;
        string? spell = SafeCrestName(() => Gameplay.SpellCrest);
        string? hunter = GetBestHunterName();
        string? reaper = SafeCrestName(() => Gameplay.ReaperCrest);
        string? warrior = SafeCrestName(() => Gameplay.WarriorCrest);
        string? wanderer = SafeCrestName(() => Gameplay.WandererCrest);
        string? witch = SafeCrestName(() => Gameplay.WitchCrest);

        return directionMask switch
        {
            UpMask => ToggleLogic(current, isLong, toolmaster, queen),
            DownMask => ToggleLogic(current, isLong, spell, hunter),
            LeftMask => ToggleLogic(current, isLong, reaper, warrior),
            RightMask => ToggleLogic(current, isLong, wanderer, witch),
            _ => null
        };
    }

    private static string? ToggleLogic(
        string current,
        bool isLong,
        string? primary,
        string? secondary)
    {
        if (string.IsNullOrWhiteSpace(primary) || string.IsNullOrWhiteSpace(secondary)) return null;
        return isLong
            ? current == secondary ? primary : secondary
            : current == primary ? secondary : primary;
    }

    private string? GetBestHunterName()
    {
        try
        {
            if (Gameplay.HunterCrest3 != null && Gameplay.HunterCrest3.IsUnlocked)
                return Gameplay.HunterCrest3.name;
        }
        catch (Exception ex) { Trace($"Hunter crest 3 lookup failed: {ex.Message}"); }

        try
        {
            if (Gameplay.HunterCrest2 != null && Gameplay.HunterCrest2.IsUnlocked)
                return Gameplay.HunterCrest2.name;
        }
        catch (Exception ex) { Trace($"Hunter crest 2 lookup failed: {ex.Message}"); }

        return SafeCrestName(() => Gameplay.HunterCrest);
    }

    private static string? SafeCrestName(Func<ToolCrest> getter)
    {
        try { return getter()?.name; }
        catch { return null; }
    }

    private int ChooseDirection(int heldMask, int downMask)
    {
        if (BitCount(downMask) == 1)
        {
            _lastChosenDirMask = downMask;
            return downMask;
        }

        if (BitCount(heldMask) == 1)
        {
            _lastChosenDirMask = heldMask;
            return heldMask;
        }

        if (heldMask != 0 && _lastChosenDirMask != 0 && (heldMask & _lastChosenDirMask) != 0)
            return _lastChosenDirMask;

        return 0;
    }

    private static int BitCount(int value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }

    private static string[] ParseCandidates(string value) =>
        (value ?? string.Empty)
        .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void Trace(string message)
    {
        if (_verboseLogging.Value) _log.LogInfo($"[CrestQuickSwitch] {message}");
    }

    private bool TryReplenishAllCrestsAtBench(
        ref bool doReplenish,
        ToolItemManager.ReplenishMethod method)
    {
        if (!_replenishAllCrestsAtBench.Value) return true;
        if (_hero?.playerData == null) return true;
        if (method.ToString().IndexOf("Bench", StringComparison.OrdinalIgnoreCase) < 0) return true;
        if (BenchReplenishPatch.IsLooping) return true;

        string originalCrest = _hero.playerData.CurrentCrestID;
        var crestNames = new[]
        {
            SafeCrestName(() => Gameplay.ToolmasterCrest),
            _queenCrestId.Value,
            SafeCrestName(() => Gameplay.SpellCrest),
            GetBestHunterName(),
            SafeCrestName(() => Gameplay.ReaperCrest),
            SafeCrestName(() => Gameplay.WarriorCrest),
            SafeCrestName(() => Gameplay.WandererCrest),
            SafeCrestName(() => Gameplay.WitchCrest)
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

        try
        {
            BenchReplenishPatch.IsLooping = true;
            _suppressInput = true;

            foreach (string crestName in crestNames!)
            {
                ToolCrest crest = ToolItemManager.GetCrestByName(crestName);
                if (crest == null || !crest.IsUnlocked || crest.IsHidden) continue;

                _hero.ResetAllCrestState();
                ToolItemManager.SetEquippedCrest(crest.name);
                ToolItemManager.TryReplenishTools(true, method);
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"Replenish-all-crests-at-bench failed: {ex}");
        }
        finally
        {
            try
            {
                _hero.ResetAllCrestState();
                ToolItemManager.SetEquippedCrest(originalCrest);
                ToolItemManager.SendEquippedChangedEvent(true);
            }
            catch (Exception restoreEx)
            {
                _log.LogError($"Failed to restore crest after bench replenishment: {restoreEx}");
            }

            _suppressInput = false;
            BenchReplenishPatch.IsLooping = false;
        }

        return true;
    }

    [HarmonyPatch(typeof(HeroController), "Awake")]
    private static class HeroAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroController __instance) => _instance?.AttachHero(__instance);
    }

    [HarmonyPatch(typeof(HeroController), "Update")]
    private static class HeroUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroController __instance) => _instance?.OnHeroUpdate(__instance);
    }

    [HarmonyPatch(typeof(ToolItemManager), "TryReplenishTools")]
    private static class BenchReplenishPatch
    {
        internal static bool IsLooping;

        [HarmonyPrefix]
        private static bool Prefix(
            ref bool doReplenish,
            ToolItemManager.ReplenishMethod method)
        {
            return _instance?.TryReplenishAllCrestsAtBench(ref doReplenish, method) ?? true;
        }
    }

    private readonly struct InputFrame
    {
        public InputFrame(
            bool mainHeld,
            bool mainPressed,
            bool mainReleased,
            int directionMask)
        {
            MainHeld = mainHeld;
            MainPressed = mainPressed;
            MainReleased = mainReleased;
            DirectionMask = directionMask;
        }

        public bool MainHeld { get; }
        public bool MainPressed { get; }
        public bool MainReleased { get; }
        public int DirectionMask { get; }
    }

    /// <summary>
    /// 只读取游戏自己的 inputActions。玩家在设置菜单中改键后，Action 对象本身仍是同一个
    /// 逻辑动作，因此键盘、手柄和辅助控制方案会自动跟随游戏设置。
    /// </summary>
    private sealed class NativeInputAdapter
    {
        private static readonly string[] UpCandidates = { "up", "moveUp", "verticalUp", "menuUp" };
        private static readonly string[] DownCandidates = { "down", "moveDown", "verticalDown", "menuDown" };
        private static readonly string[] LeftCandidates = { "left", "moveLeft", "horizontalLeft", "menuLeft" };
        private static readonly string[] RightCandidates = { "right", "moveRight", "horizontalRight", "menuRight" };
        private static readonly string[] MoveVectorCandidates = { "moveVector", "movement", "move", "directionVector" };

        private readonly ManualLogSource _log;
        private object? _actionSet;
        private ActionReader? _main;
        private ActionReader? _up;
        private ActionReader? _down;
        private ActionReader? _left;
        private ActionReader? _right;
        private VectorReader? _moveVector;
        private bool _warnedNotReady;

        public NativeInputAdapter(ManualLogSource log) => _log = log;

        public bool IsReady =>
            _actionSet != null &&
            _main != null &&
            ((_up != null && _down != null && _left != null && _right != null) || _moveVector != null);

        public void Reset()
        {
            _actionSet = null;
            _main = _up = _down = _left = _right = null;
            _moveVector = null;
            _warnedNotReady = false;
        }

        public void Bind(HeroController hero, IReadOnlyList<string> mainCandidates)
        {
            Reset();
            _actionSet = ResolveActionSet(hero);
            if (_actionSet == null)
            {
                WarnOnce("Unable to resolve the game's inputActions object. Quick switching is disabled; no fixed-key fallback will be used.");
                return;
            }

            _main = FindAction(_actionSet, mainCandidates);
            _up = FindAction(_actionSet, UpCandidates);
            _down = FindAction(_actionSet, DownCandidates);
            _left = FindAction(_actionSet, LeftCandidates);
            _right = FindAction(_actionSet, RightCandidates);
            _moveVector = FindVector(_actionSet, MoveVectorCandidates);

            if (!IsReady)
            {
                string members = string.Join(", ", GetMemberNames(_actionSet.GetType()).Take(80));
                WarnOnce(
                    "Native input actions were found, but one or more required actions could not be mapped. " +
                    $"Available members: {members}");
                return;
            }

            _log.LogInfo(
                "[CrestQuickSwitch] Native input bound: " +
                $"main={_main?.Name}; up={_up?.Name}; down={_down?.Name}; " +
                $"left={_left?.Name}; right={_right?.Name}; vector={_moveVector?.Name}");
        }

        public void RefreshIfActionSetChanged(HeroController hero, IReadOnlyList<string> mainCandidates)
        {
            object? current = ResolveActionSet(hero);
            if (current == null)
            {
                if (_actionSet != null) Reset();
                return;
            }

            if (!ReferenceEquals(current, _actionSet)) Bind(hero, mainCandidates);
        }

        public InputFrame ReadFrame()
        {
            if (!IsReady || _main == null) return default;

            int mask = 0;
            if (_up?.IsPressed() == true) mask |= UpMask;
            if (_down?.IsPressed() == true) mask |= DownMask;
            if (_left?.IsPressed() == true) mask |= LeftMask;
            if (_right?.IsPressed() == true) mask |= RightMask;

            if (mask == 0 && _moveVector != null)
            {
                Vector2 vector = _moveVector.Read();
                const float threshold = 0.45f;
                if (vector.y > threshold) mask |= UpMask;
                if (vector.y < -threshold) mask |= DownMask;
                if (vector.x < -threshold) mask |= LeftMask;
                if (vector.x > threshold) mask |= RightMask;
            }

            return new InputFrame(
                _main.IsPressed(),
                _main.WasPressed(),
                _main.WasReleased(),
                mask);
        }

        private object? ResolveActionSet(HeroController hero)
        {
            object? inputHandler = ReadMember(hero, "inputHandler", "InputHandler");

            if (inputHandler == null)
            {
                Type? inputHandlerType = AccessTools.TypeByName("InputHandler");
                inputHandler = ReadStaticMember(inputHandlerType, "Instance", "instance");
            }

            return inputHandler == null
                ? null
                : ReadMember(inputHandler, "inputActions", "InputActions", "actions", "Actions");
        }

        private static ActionReader? FindAction(object actionSet, IEnumerable<string> candidates)
        {
            foreach (string candidate in candidates)
            {
                object? action = ReadMember(actionSet, candidate);
                if (action != null)
                {
                    var reader = new ActionReader(candidate, action);
                    if (reader.CanRead) return reader;
                }
            }
            return null;
        }

        private static VectorReader? FindVector(object actionSet, IEnumerable<string> candidates)
        {
            foreach (string candidate in candidates)
            {
                object? vectorAction = ReadMember(actionSet, candidate);
                if (vectorAction != null)
                {
                    var reader = new VectorReader(candidate, vectorAction);
                    if (reader.CanRead) return reader;
                }
            }
            return null;
        }

        private void WarnOnce(string message)
        {
            if (_warnedNotReady) return;
            _warnedNotReady = true;
            _log.LogWarning($"[CrestQuickSwitch] {message}");
        }

        private sealed class ActionReader
        {
            private readonly object _action;
            private readonly PropertyInfo? _isPressedProperty;
            private readonly PropertyInfo? _wasPressedProperty;
            private readonly PropertyInfo? _wasReleasedProperty;
            private readonly MethodInfo? _isPressedMethod;
            private readonly MethodInfo? _wasPressedMethod;
            private readonly MethodInfo? _wasReleasedMethod;

            public ActionReader(string name, object action)
            {
                Name = name;
                _action = action;
                Type type = action.GetType();
                _isPressedProperty = FindBoolProperty(type, "IsPressed", "isPressed");
                _wasPressedProperty = FindBoolProperty(type, "WasPressed", "wasPressed");
                _wasReleasedProperty = FindBoolProperty(type, "WasReleased", "wasReleased");
                _isPressedMethod = FindBoolMethod(type, "IsPressed", "get_IsPressed");
                _wasPressedMethod = FindBoolMethod(type, "WasPressed", "get_WasPressed");
                _wasReleasedMethod = FindBoolMethod(type, "WasReleased", "get_WasReleased");
            }

            public string Name { get; }
            public bool CanRead => _isPressedProperty != null || _isPressedMethod != null;
            public bool IsPressed() => Read(_isPressedProperty, _isPressedMethod);
            public bool WasPressed() => Read(_wasPressedProperty, _wasPressedMethod);
            public bool WasReleased() => Read(_wasReleasedProperty, _wasReleasedMethod);

            private bool Read(PropertyInfo? property, MethodInfo? method)
            {
                try
                {
                    if (property != null) return (bool)property.GetValue(_action, null);
                    if (method != null) return (bool)method.Invoke(_action, null);
                }
                catch { }
                return false;
            }
        }

        private sealed class VectorReader
        {
            private readonly object _action;
            private readonly PropertyInfo? _property;
            private readonly FieldInfo? _field;
            private readonly MethodInfo? _method;

            public VectorReader(string name, object action)
            {
                Name = name;
                _action = action;
                Type type = action.GetType();

                _property = FindVectorProperty(type, "Vector", "Value", "RawValue", "Direction");
                _field = FindVectorField(type, "Vector", "Value", "RawValue", "Direction");
                _method = FindVectorMethod(type, "ReadValue", "GetValue", "GetVector");
            }

            public string Name { get; }
            public bool CanRead => _property != null || _field != null || _method != null;

            public Vector2 Read()
            {
                try
                {
                    object? value = _property?.GetValue(_action, null)
                                    ?? _field?.GetValue(_action)
                                    ?? _method?.Invoke(_action, null);
                    if (value is Vector2 vector) return vector;
                }
                catch { }
                return Vector2.zero;
            }
        }
    }

    private sealed class HeroReflectionCache
    {
        private readonly ManualLogSource _log;

        private FieldInfo? _cState;
        private FieldInfo? _attacking;
        private FieldInfo? _upAttacking;
        private FieldInfo? _downAttacking;
        private FieldInfo? _nailCharging;
        private FieldInfo? _dashing;
        private FieldInfo? _backDashing;
        private FieldInfo? _canControl;
        private FieldInfo? _isDashStabBouncing;
        private FieldInfo? _acceptingInput;
        private FieldInfo? _controlRelinquished;
        private MethodInfo? _addInvincibleTime;
        private FieldInfo? _invincibleTimer;
        private MethodInfo? _resetAttacksDash;
        private MethodInfo? _resetAttacks;
        private readonly List<MethodInfo> _dashCancelMethods = new();

        public HeroReflectionCache(ManualLogSource log) => _log = log;

        public void Initialize(HeroController hero)
        {
            Type heroType = hero.GetType();
            _cState = FindField(heroType, "cState", "CState", "heroState");
            Type? stateType = _cState?.FieldType;

            if (stateType != null)
            {
                _attacking = FindField(stateType, "attacking");
                _upAttacking = FindField(stateType, "upAttacking");
                _downAttacking = FindField(stateType, "downAttacking");
                _nailCharging = FindField(stateType, "nailCharging", "chargingAttack");
                _dashing = FindField(stateType, "dashing", "isDashing");
                _backDashing = FindField(stateType, "backDashing", "isBackDashing");
                _canControl = FindField(stateType, "canControl");
            }

            _isDashStabBouncing = FindField(heroType, "isDashStabBouncing");
            _acceptingInput = FindField(heroType, "acceptingInput");
            _controlRelinquished = FindField(heroType, "controlRelinquished");
            _addInvincibleTime = FindMethod(heroType, "AddInvincibleTime", typeof(float));
            _invincibleTimer = FindField(heroType, "invincibleTimer");
            _resetAttacksDash = FindMethod(heroType, "ResetAttacksDash");
            _resetAttacks = FindMethod(heroType, "ResetAttacks", typeof(bool));

            _dashCancelMethods.Clear();
            foreach (string name in new[] { "CancelDash", "StopDash", "EndDash", "DashEnd", "ResetDash" })
            {
                MethodInfo? method = FindMethod(heroType, name);
                if (method != null && !_dashCancelMethods.Contains(method)) _dashCancelMethods.Add(method);
            }
        }

        public bool IsAttackOrChargeActive(HeroController hero)
        {
            object? state = _cState?.GetValue(hero);
            return state != null &&
                   (ReadBool(state, _attacking) ||
                    ReadBool(state, _upAttacking) ||
                    ReadBool(state, _downAttacking) ||
                    ReadBool(state, _nailCharging));
        }

        public bool IsDashLikeActive(HeroController hero)
        {
            object? state = _cState?.GetValue(hero);
            return (state != null && (ReadBool(state, _dashing) || ReadBool(state, _backDashing))) ||
                   ReadBool(hero, _isDashStabBouncing);
        }

        public bool HasControl(HeroController hero)
        {
            try
            {
                object? state = _cState?.GetValue(hero);
                bool canControl = true;
                if (state != null && _canControl != null) canControl &= ReadBool(state, _canControl);
                if (_acceptingInput != null) canControl &= ReadBool(hero, _acceptingInput);
                if (_controlRelinquished != null) canControl &= !ReadBool(hero, _controlRelinquished);
                return canControl;
            }
            catch { return false; }
        }

        public void NormalizeDashExit(HeroController hero, bool zeroHorizontalVelocity)
        {
            try { _resetAttacksDash?.Invoke(hero, null); } catch { }
            try { _resetAttacks?.Invoke(hero, new object[] { true }); } catch { }

            foreach (MethodInfo method in _dashCancelMethods)
            {
                try { method.Invoke(hero, null); } catch { }
            }

            object? state = null;
            try { state = _cState?.GetValue(hero); } catch { }
            if (state != null)
            {
                WriteBool(state, _dashing, false);
                WriteBool(state, _backDashing, false);
            }
            WriteBool(hero, _isDashStabBouncing, false);

            if (zeroHorizontalVelocity) ZeroHorizontalVelocity(hero);
        }

        public void ApplyInvincibility(HeroController hero, float seconds)
        {
            if (seconds <= 0f) return;
            try
            {
                if (_addInvincibleTime != null)
                {
                    _addInvincibleTime.Invoke(hero, new object[] { seconds });
                    return;
                }

                if (_invincibleTimer != null)
                {
                    float current = Convert.ToSingle(_invincibleTimer.GetValue(hero));
                    _invincibleTimer.SetValue(hero, Mathf.Max(current, seconds));
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[CrestQuickSwitch] Failed to apply invulnerability: {ex.Message}");
            }
        }

        private static void ZeroHorizontalVelocity(HeroController hero)
        {
            try
            {
                Rigidbody2D? body = hero.GetComponent<Rigidbody2D>();
                if (body == null) return;

                Type bodyType = body.GetType();
                PropertyInfo? velocityProperty =
                    AccessTools.Property(bodyType, "linearVelocity") ??
                    AccessTools.Property(bodyType, "velocity");

                if (velocityProperty != null && velocityProperty.CanRead && velocityProperty.CanWrite)
                {
                    object? value = velocityProperty.GetValue(body, null);
                    if (value is Vector2 velocity)
                        velocityProperty.SetValue(body, new Vector2(0f, velocity.y), null);
                }
            }
            catch { }
        }

        private static bool ReadBool(object target, FieldInfo? field)
        {
            try { return field != null && field.GetValue(target) is bool value && value; }
            catch { return false; }
        }

        private static void WriteBool(object target, FieldInfo? field, bool value)
        {
            try
            {
                if (field != null && field.FieldType == typeof(bool)) field.SetValue(target, value);
            }
            catch { }
        }
    }

    private static object? ReadMember(object target, params string[] names)
    {
        Type type = target.GetType();
        foreach (string name in names)
        {
            FieldInfo? field = FindField(type, name);
            if (field != null)
            {
                try { return field.GetValue(target); } catch { }
            }

            PropertyInfo? property = FindProperty(type, name);
            if (property != null && property.CanRead)
            {
                try { return property.GetValue(target, null); } catch { }
            }
        }
        return null;
    }

    private static object? ReadStaticMember(Type? type, params string[] names)
    {
        if (type == null) return null;
        foreach (string name in names)
        {
            FieldInfo? field = FindField(type, name);
            if (field != null && field.IsStatic)
            {
                try { return field.GetValue(null); } catch { }
            }

            PropertyInfo? property = FindProperty(type, name);
            MethodInfo? getter = property?.GetGetMethod(true);
            if (property != null && getter?.IsStatic == true)
            {
                try { return property.GetValue(null, null); } catch { }
            }
        }
        return null;
    }

    private static FieldInfo? FindField(Type type, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (string name in names)
        {
            FieldInfo? exact = type.GetField(name, flags);
            if (exact != null) return exact;

            FieldInfo? insensitive = type.GetFields(flags)
                .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (insensitive != null) return insensitive;
        }
        return null;
    }

    private static PropertyInfo? FindProperty(Type type, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (string name in names)
        {
            PropertyInfo? exact = type.GetProperty(name, flags);
            if (exact != null) return exact;

            PropertyInfo? insensitive = type.GetProperties(flags)
                .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (insensitive != null) return insensitive;
        }
        return null;
    }

    private static MethodInfo? FindMethod(Type type, string name, params Type[] parameters)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        return parameters.Length == 0
            ? type.GetMethods(flags).FirstOrDefault(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
                x.GetParameters().Length == 0)
            : type.GetMethods(flags).FirstOrDefault(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
                x.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
    }

    private static PropertyInfo? FindBoolProperty(Type type, params string[] names) =>
        names.Select(name => FindProperty(type, name))
            .FirstOrDefault(property => property?.PropertyType == typeof(bool));

    private static MethodInfo? FindBoolMethod(Type type, params string[] names) =>
        names.Select(name => FindMethod(type, name))
            .FirstOrDefault(method => method?.ReturnType == typeof(bool));

    private static PropertyInfo? FindVectorProperty(Type type, params string[] names) =>
        names.Select(name => FindProperty(type, name))
            .FirstOrDefault(property => property?.PropertyType == typeof(Vector2));

    private static FieldInfo? FindVectorField(Type type, params string[] names) =>
        names.Select(name => FindField(type, name))
            .FirstOrDefault(field => field?.FieldType == typeof(Vector2));

    private static MethodInfo? FindVectorMethod(Type type, params string[] names) =>
        names.Select(name => FindMethod(type, name))
            .FirstOrDefault(method => method?.ReturnType == typeof(Vector2));

    private static IEnumerable<string> GetMemberNames(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return type.GetFields(flags).Select(x => x.Name)
            .Concat(type.GetProperties(flags).Select(x => x.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }
}

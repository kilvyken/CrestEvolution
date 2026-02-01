using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using GlobalSettings;

[BepInPlugin("com.yourname.crestkeydirs5", "Crest Key + Directions V5", "1.7")] 
public class CrestKeyDirectionsV5 : BaseUnityPlugin
{
    public static ManualLogSource Log;

    private const float LONG_PRESS_THRESHOLD = 0.2f;
    private const float SPECIAL_PRESS_THRESHOLD = 3.0f;
    private const float COOLDOWN_SECONDS = 0.3f;
    private const float IFRAME_SECONDS = 0.1f;

    public const string QUEEN_CREST_ID = "Yen";

    private static float nextSwapTime = 0f;
    private static float mainKeyDownTime = -1f;
    private static int lastChosenDirMask = 0;

    private static bool swappedThisPress = false;
    private static bool specialTriggeredThisPress = false;
    private static int lastTriggeredDirMask = 0;
    private static bool lastTriggeredLong = false;

    private static HeroController hero;

    // 排队逻辑
    private static bool hasQueuedSwap = false;
    private static string queuedTargetName = null;
    private static string queuedReason = null;
    private static int queuedEarliestFrame = -1;
    private static bool queuedFromCombat = false;

    private static bool isApplyingSwap = false;

    // ==================== 性能优化：反射缓存 ====================
    private static class ReflectionCache
    {
        public static FieldInfo fi_cState;
        public static FieldInfo fi_attacking;
        public static FieldInfo fi_upAttacking;
        public static FieldInfo fi_downAttacking;
        public static FieldInfo fi_nailCharging;
        public static FieldInfo fi_dashing;
        public static FieldInfo fi_backDashing;
        public static FieldInfo fi_isDashStabBouncing;

        // Control checks
        public static FieldInfo fi_cState_canControl;
        public static FieldInfo fi_hero_acceptingInput;
        public static FieldInfo fi_hero_controlRelinquished;

        // Methods
        public static MethodInfo mi_AddInvincibleTime;
        public static FieldInfo fi_invincibleTimer;
        public static MethodInfo mi_ResetAttacksDash;
        public static MethodInfo mi_ResetAttacks;

        public static void Initialize(HeroController heroInstance)
        {
            if (heroInstance == null) return;
            Type heroType = typeof(HeroController);

            fi_cState = AccessTools.Field(heroType, "cState");
            Type cStateType = fi_cState?.FieldType;

            if (cStateType != null)
            {
                fi_attacking = AccessTools.Field(cStateType, "attacking");
                fi_upAttacking = AccessTools.Field(cStateType, "upAttacking");
                fi_downAttacking = AccessTools.Field(cStateType, "downAttacking");
                fi_nailCharging = AccessTools.Field(cStateType, "nailCharging");
                fi_dashing = AccessTools.Field(cStateType, "dashing");
                fi_backDashing = AccessTools.Field(cStateType, "backDashing");
                fi_cState_canControl = AccessTools.Field(cStateType, "canControl");
            }

            fi_isDashStabBouncing = AccessTools.Field(heroType, "isDashStabBouncing");
            fi_hero_acceptingInput = AccessTools.Field(heroType, "acceptingInput");
            fi_hero_controlRelinquished = AccessTools.Field(heroType, "controlRelinquished");

            mi_AddInvincibleTime = heroType.GetMethod("AddInvincibleTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fi_invincibleTimer = heroType.GetField("invincibleTimer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            mi_ResetAttacksDash = AccessTools.Method(heroType, "ResetAttacksDash", new Type[] { });
            mi_ResetAttacks = AccessTools.Method(heroType, "ResetAttacks", new Type[] { typeof(bool) });
        }
    }

    // ==================== 新增：输入适配器 (处理手柄 + 键盘) ====================
    private static class InputAdapter
    {
        // 缓存游戏原本的 InputHandler 反射信息
        private static FieldInfo fi_inputHandler;
        private static FieldInfo fi_inputActions;
        private static object cachedInputActions;

        // 我们尝试探测的动作名称 (Silksong/HK mod 通常绑定在这些动作上)
        // 你可以根据实际情况修改优先级，比如 "quickMap" 也就是 Tab/LB
        private static string[] possibleActionNames = new string[] { "quickMap", "dreamNail", "inventory", "cast" };
        private static PropertyInfo pi_ActionIsPressed;
        private static PropertyInfo pi_ActionWasPressed;
        private static object targetActionObject;

        public static void Initialize(HeroController hero)
        {
            try
            {
                // 获取 inputHandler
                fi_inputHandler = AccessTools.Field(typeof(HeroController), "inputHandler");
                object inputHandler = fi_inputHandler?.GetValue(hero);
                if (inputHandler == null) return;

                // 获取 inputActions
                fi_inputActions = AccessTools.Field(inputHandler.GetType(), "inputActions");
                cachedInputActions = fi_inputActions?.GetValue(inputHandler);
                if (cachedInputActions == null) return;

                // 尝试在 inputActions 中找到我们关心的 Action (Taunt)
                Type actionsType = cachedInputActions.GetType();
                foreach (var name in possibleActionNames)
                {
                    FieldInfo actionField = AccessTools.Field(actionsType, name);
                    if (actionField != null)
                    {
                        targetActionObject = actionField.GetValue(cachedInputActions);
                        if (targetActionObject != null)
                        {
                            Type oneAxisType = targetActionObject.GetType();
                            // 获取 IsPressed / WasPressed 属性
                            pi_ActionIsPressed = AccessTools.Property(oneAxisType, "IsPressed");
                            pi_ActionWasPressed = AccessTools.Property(oneAxisType, "WasPressed");
                            Log?.LogInfo($"[InputAdapter] Hooked into game action: {name}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[InputAdapter] Init Failed: {ex.Message}");
            }
        }

        public static bool IsTauntDown()
        {
            // 1. 键盘检查
            if (Input.GetKeyDown(KeyboardBindingCache.Taunt)) return true;

            // 2. 游戏原生 Action 检查 (最准确，适配任何手柄配置)
            if (pi_ActionWasPressed != null && targetActionObject != null)
            {
                try { if ((bool)pi_ActionWasPressed.GetValue(targetActionObject, null)) return true; } catch { }
            }

            // 3. 手柄按键兜底 (如果反射失败，检查常用手柄键)
            // JoystickButton4 = LB/L1, Button5 = RB/R1, Button3 = Y/Triangle
            if (Input.GetKeyDown(KeyCode.JoystickButton4) ||
                Input.GetKeyDown(KeyCode.JoystickButton5) ||
                Input.GetKeyDown(KeyCode.JoystickButton3)) return true;

            return false;
        }

        public static bool IsTauntUp()
        {
            if (Input.GetKeyUp(KeyboardBindingCache.Taunt)) return true;

            // 注意：Action 只有 IsPressed 和 WasPressed，通常没有 WasReleased，所以用 !IsPressed 近似或自行逻辑处理
            // 为简单起见，这里主要依赖键盘和手柄物理按键的 Up
            if (Input.GetKeyUp(KeyCode.JoystickButton4) ||
                Input.GetKeyUp(KeyCode.JoystickButton5) ||
                Input.GetKeyUp(KeyCode.JoystickButton3)) return true;

            // 如果使用 Action 状态，这里可以不返回 true，完全依赖 IsTauntHeld 返回 false 来触发
            return false;
        }

        public static bool IsTauntHeld()
        {
            if (Input.GetKey(KeyboardBindingCache.Taunt)) return true;

            if (pi_ActionIsPressed != null && targetActionObject != null)
            {
                try { if ((bool)pi_ActionIsPressed.GetValue(targetActionObject, null)) return true; } catch { }
            }

            if (Input.GetKey(KeyCode.JoystickButton4) ||
                Input.GetKey(KeyCode.JoystickButton5) ||
                Input.GetKey(KeyCode.JoystickButton3)) return true;

            return false;
        }

        // 获取混合方向掩码 (键盘 + 手柄摇杆)
        public static int GetCombinedDirMask(bool justDown)
        {
            int mask = 0;

            // A. 键盘部分
            if (justDown)
            {
                if (Input.GetKeyDown(KeyboardBindingCache.Up)) mask |= 1;
                if (Input.GetKeyDown(KeyboardBindingCache.Down)) mask |= 2;
                if (Input.GetKeyDown(KeyboardBindingCache.Left)) mask |= 4;
                if (Input.GetKeyDown(KeyboardBindingCache.Right)) mask |= 8;
            }
            else
            {
                if (Input.GetKey(KeyboardBindingCache.Up)) mask |= 1;
                if (Input.GetKey(KeyboardBindingCache.Down)) mask |= 2;
                if (Input.GetKey(KeyboardBindingCache.Left)) mask |= 4;
                if (Input.GetKey(KeyboardBindingCache.Right)) mask |= 8;
            }

            // B. 手柄摇杆部分 (Unity 通用轴)
            // 阈值设为 0.5 防止漂移
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (v > 0.5f) mask |= 1;  // Up
            if (v < -0.5f) mask |= 2; // Down
            if (h < -0.5f) mask |= 4; // Left
            if (h > 0.5f) mask |= 8;  // Right

            return mask;
        }
    }

    private void Awake()
    {
        Log = base.Logger;

        Harmony.CreateAndPatchAll(typeof(CrestKeyDirectionsV5));
        Harmony.CreateAndPatchAll(typeof(BenchReplenishPatch));

        KeyboardBindingCache.Initialize();

        Log.LogInfo($"CrestKeyDirectionsV5 loaded (Gamepad Ready). Target Queen ID: {QUEEN_CREST_ID}");
    }

    private void Update()
    {
        if (hero == null) hero = HeroController.instance;
        if (hero == null || hero.playerData == null) return;

        TryConsumeQueuedSwap();

        // 1. 获取主功能键状态 (键盘 或 手柄)
        bool isMainDown = InputAdapter.IsTauntDown();
        bool isMainHeld = InputAdapter.IsTauntHeld();
        bool isMainUp = InputAdapter.IsTauntUp();

        // 处理按下
        if (isMainDown)
        {
            mainKeyDownTime = Time.time;
            ResetPressState();
        }

        // 处理抬起
        // 注意：如果是手柄 Action 模式，IsTauntUp 可能检测不到，所以如果 Held 为 false 也视为抬起
        if (isMainUp || (mainKeyDownTime > 0f && !isMainHeld))
        {
            // 只有当之前是按下状态时，才执行重置
            if (mainKeyDownTime > 0f)
            {
                mainKeyDownTime = -1f;
                ResetPressState();
            }
            return;
        }

        if (!isMainHeld) return;
        if (mainKeyDownTime < 0f) mainKeyDownTime = Time.time;

        // 2. 获取方向 (键盘 + 手柄摇杆)
        // 逻辑：HeldMask 用于判定当前指向，DownMask 用于判定瞬间输入(虽然这里主要逻辑依赖 Held)
        int heldMask = InputAdapter.GetCombinedDirMask(false);
        int downMask = InputAdapter.GetCombinedDirMask(true); // 注意：GetAxisRaw 很难检测 Down 的一瞬间，这里主要还是靠 Keyboard

        int chosenDir = ChooseDirection(heldMask, downMask);

        float holdDuration = Time.time - mainKeyDownTime;

        // 3秒特殊长按
        if (chosenDir == 0 && !specialTriggeredThisPress)
        {
            if (holdDuration >= SPECIAL_PRESS_THRESHOLD)
            {
                HandleSpecial3SecSwap();
                specialTriggeredThisPress = true;
                nextSwapTime = Time.time + COOLDOWN_SECONDS;
                return;
            }
        }

        if (chosenDir == 0) return;
        if (Time.time < nextSwapTime) return;

        bool isLong = holdDuration >= LONG_PRESS_THRESHOLD;

        bool directionChanged = chosenDir != lastTriggeredDirMask;
        bool modeChangedSameDir = !directionChanged && (isLong != lastTriggeredLong);
        bool shouldTrigger = directionChanged || (!swappedThisPress && modeChangedSameDir);
        if (!shouldTrigger) return;

        PerformDirectionalSwap(chosenDir, isLong);
    }

    private void ResetPressState()
    {
        lastChosenDirMask = 0;
        lastTriggeredDirMask = 0;
        lastTriggeredLong = false;
        swappedThisPress = false;
        specialTriggeredThisPress = false;
    }

    private void HandleSpecial3SecSwap()
    {
        string current = hero.playerData.CurrentCrestID;
        string cursedName = Gameplay.CursedCrest.name;

        string cloaklessName = "Cloakless Crest";
        if (Gameplay.CloaklessCrest != null) cloaklessName = Gameplay.CloaklessCrest.name;

        string targetName = (current == cursedName) ? cloaklessName : cursedName;
        RequestSwap(targetName, "SPECIAL_3S");
    }

    private void PerformDirectionalSwap(int dirMask, bool isLong)
    {
        string current = hero.playerData.CurrentCrestID;
        string targetName = ResolveDirectionTarget(current, dirMask, isLong);

        if (string.IsNullOrEmpty(targetName)) return;
        if (targetName == current) return;

        RequestSwap(targetName, isLong ? "LONG" : "SHORT");

        lastTriggeredDirMask = dirMask;
        lastTriggeredLong = isLong;
        swappedThisPress = true;
        nextSwapTime = Time.time + COOLDOWN_SECONDS;
    }

    private static void RequestSwap(string targetName, string reason)
    {
        if (string.IsNullOrEmpty(targetName)) return;
        if (hero == null || hero.playerData == null) return;

        if (IsSafeToSwapNow())
        {
            ApplySwapNow(targetName, reason);
            return;
        }

        hasQueuedSwap = true;
        queuedTargetName = targetName;
        queuedReason = reason;
        queuedEarliestFrame = Time.frameCount + 1;

        queuedFromCombat = queuedFromCombat || IsCombatLikeActive();
    }

    private static void TryConsumeQueuedSwap()
    {
        if (!hasQueuedSwap) return;
        if (hero == null || hero.playerData == null) return;
        if (Time.frameCount < queuedEarliestFrame) return;

        if (!IsSafeToSwapNow()) return;

        string current = hero.playerData.CurrentCrestID;
        if (string.IsNullOrEmpty(queuedTargetName) || queuedTargetName == current)
        {
            hasQueuedSwap = false;
            queuedFromCombat = false;
            return;
        }

        if (queuedFromCombat) ForceClearCombatStateLight();

        ApplySwapNow(queuedTargetName, (queuedReason ?? "QUEUED") + (queuedFromCombat ? "_LIGHTCLR" : "_FAST"));

        hasQueuedSwap = false;
        queuedFromCombat = false;
    }

    // ==================== 优化核心：使用缓存字段 ====================
    private static bool IsSafeToSwapNow()
    {
        try
        {
            // 初始化检查
            if (ReflectionCache.fi_cState == null && hero != null)
            {
                ReflectionCache.Initialize(hero);
                InputAdapter.Initialize(hero); // 同时初始化输入适配器
            }

            object cStateObj = ReflectionCache.fi_cState?.GetValue(hero);

            if (cStateObj != null)
            {
                bool attacking =
                    GetCachedBool(cStateObj, ReflectionCache.fi_attacking) ||
                    GetCachedBool(cStateObj, ReflectionCache.fi_upAttacking) ||
                    GetCachedBool(cStateObj, ReflectionCache.fi_downAttacking);

                bool nailCharging = GetCachedBool(cStateObj, ReflectionCache.fi_nailCharging);
                bool dashing =
                    GetCachedBool(cStateObj, ReflectionCache.fi_dashing) ||
                    GetCachedBool(cStateObj, ReflectionCache.fi_backDashing);

                if (attacking || nailCharging || dashing) return false;
            }

            if (GetCachedBool(hero, ReflectionCache.fi_isDashStabBouncing)) return false;

            bool canControl = true;

            if (cStateObj != null && ReflectionCache.fi_cState_canControl != null)
                canControl &= GetCachedBool(cStateObj, ReflectionCache.fi_cState_canControl);

            if (ReflectionCache.fi_hero_acceptingInput != null)
                canControl &= (bool)ReflectionCache.fi_hero_acceptingInput.GetValue(hero);

            if (ReflectionCache.fi_hero_controlRelinquished != null)
                canControl &= !(bool)ReflectionCache.fi_hero_controlRelinquished.GetValue(hero);

            return canControl;
        }
        catch { return false; }
    }

    private static bool IsCombatLikeActive()
    {
尝试
        {
            if (ReflectionCache.fi_cState == null && hero != null) ReflectionCache.Initialize(hero);
            object cStateObj = ReflectionCache.fi_cState?.GetValue(hero);
            if (cStateObj != null)
            {
                bool attacking =
                    GetCachedBool(cStateObj, ReflectionCache.fi_attacking) ||
                    GetCachedBool(cStateObj, ReflectionCache.fi_upAttacking) ||
                    GetCachedBool(cStateObj, ReflectionCache.fi_downAttacking);

                bool nailCharging = GetCachedBool(cStateObj, ReflectionCache.fi_nailCharging);
                bool dashing =
                    GetCachedBool(cStateObj, ReflectionCache.fi_dashing) ||
                    GetCachedBool(cStateObj, ReflectionCache.fi_backDashing);

                if (attacking || nailCharging || dashing) return true;
            }

            if (GetCachedBool(hero, ReflectionCache.fi_isDashStabBouncing)) return true;
        }
        catch { }
        return false;
    }

    private static bool GetCachedBool(object obj, FieldInfo fi)
    {
        if (obj == null || fi == null) return false;
        return (bool)fi.GetValue(obj);
    }

    private static void ForceClearCombatStateLight()
    {
        try { ReflectionCache.mi_ResetAttacksDash?.Invoke(hero, null); } catch { }
        try
        {
            if (ReflectionCache.mi_ResetAttacks != null)
                ReflectionCache.mi_ResetAttacks.Invoke(hero, new object[] { true });
            else
                ReflectionCache.mi_ResetAttacksDash?.Invoke(hero, null);
        }
        catch { }
        queuedEarliestFrame = Mathf.Max(queuedEarliestFrame, Time.frameCount + 1);
    }

    private static void ApplySwapNow(string targetName, string reason)
    {
        if (isApplyingSwap) return;
        isApplyingSwap = true;

        try
        {
            ToolCrest target = ToolItemManager.GetCrestByName(targetName);
            if (target == null) return;

            int silkBefore = hero.playerData.silk;
            ApplyInvincibilitySafe(IFRAME_SECONDS);

            hero.ResetAllCrestState();
            ToolItemManager.SetEquippedCrest(target.name);
            ToolItemManager.SendEquippedChangedEvent(true);

            hero.playerData.silk = silkBefore;
            Log?.LogInfo($"Swapped -> {target.name} [{reason}]");
        }
        finally
        {
            isApplyingSwap = false;
        }
    }

    private static string ResolveDirectionTarget(string current, int dir, bool isLong)
    {
        string nameToolmaster = Gameplay.ToolmasterCrest.name;
        string nameQueen = QUEEN_CREST_ID;
        string nameSpell = Gameplay.SpellCrest.name;
        string nameHunter = GetBestHunterName();
        string nameReaper = Gameplay.ReaperCrest.name;
        string nameWarrior = Gameplay.WarriorCrest.name;
        string nameWanderer = Gameplay.WandererCrest.name;
        string nameWitch = Gameplay.WitchCrest.name;

        switch (dir)
        {
            case 1: return ToggleLogic(current, isLong, nameToolmaster, nameQueen);
            case 2: return ToggleLogic(current, isLong, nameSpell, nameHunter);
            case 4: return ToggleLogic(current, isLong, nameReaper, nameWarrior);
            case 8: return ToggleLogic(current, isLong, nameWanderer, nameWitch);
            default: return null;
        }
    }

    private static string ToggleLogic(string current, bool isLong, string primary, string secondary)
    {
        if (isLong) return (current == secondary) ? primary : secondary;
        else return (current == primary) ? secondary : primary;
    }

    private static int ChooseDirection(int heldMask, int downMask)
    {
        if (BitCount(downMask) == 1) { lastChosenDirMask = downMask; return downMask; }
        if (BitCount(heldMask) == 1) { lastChosenDirMask = heldMask; return heldMask; }
        if (heldMask != 0 && lastChosenDirMask != 0 && (heldMask & lastChosenDirMask) != 0) return lastChosenDirMask;
        return 0;
    }

    private static int BitCount(int x)
    {
        int c = 0;
        while (x != 0) { x &= (x - 1); c++; }
        return c;
    }

    private static string GetBestHunterName()
    {
        try { if (Gameplay.HunterCrest3 != null && Gameplay.HunterCrest3.IsUnlocked) return Gameplay.HunterCrest3.name; } catch { }
        try { if (Gameplay.HunterCrest2 != null && Gameplay.HunterCrest2.IsUnlocked) return Gameplay.HunterCrest2.name; } catch { }
        return Gameplay.HunterCrest.name;
    }

    private static void ApplyInvincibilitySafe(float seconds)
    {
        if (seconds <= 0f || hero == null) return;
        try
        {
            if (ReflectionCache.mi_AddInvincibleTime == null) ReflectionCache.Initialize(hero);

            if (ReflectionCache.mi_AddInvincibleTime != null)
            {
                ReflectionCache.mi_AddInvincibleTime.Invoke(hero, new object[] { seconds });
                return;
            }
            if (ReflectionCache.fi_invincibleTimer != null)
            {
                float current = (float)ReflectionCache.fi_invincibleTimer.GetValue(hero);
                ReflectionCache.fi_invincibleTimer.SetValue(hero, Mathf.Max(current, seconds));
            }
        }
        catch { }
    }

    [HarmonyPatch(typeof(HeroController), "Awake")]
    [HarmonyPostfix]
    private static void HeroAwakePostfix(HeroController __instance)
    {
        hero = __instance;
        ReflectionCache.Initialize(hero);
        InputAdapter.Initialize(hero); // 初始化输入适配器
    }

    // ====== 键盘键位缓存 ======
    private static class KeyboardBindingCache
    {
        public static KeyCode Taunt { get; private set; } = KeyCode.V;
        public static KeyCode Up { get; private set; } = KeyCode.UpArrow;
        public static KeyCode Down { get; private set; } = KeyCode.DownArrow;
        public static KeyCode Left { get; private set; } = KeyCode.LeftArrow;
        public static KeyCode Right { get; private set; } = KeyCode.RightArrow;

        public static void Initialize()
        {
            Taunt = ParseKeyCode(ReadKeyString("KeyTaunt", "V"), KeyCode.V);
            Up = ParseKeyCode(ReadKeyString("KeyUp", "UpArrow"), KeyCode.UpArrow);
            Down = ParseKeyCode(ReadKeyString("KeyDown", "DownArrow"), KeyCode.DownArrow);
            Left = ParseKeyCode(ReadKeyString("KeyLeft", "LeftArrow"), KeyCode.LeftArrow);
            Right = ParseKeyCode(ReadKeyString("KeyRight", "RightArrow"), KeyCode.RightArrow);
        }

        private static string ReadKeyString(string prefKey, string fallback)
        {
            try
            {
                var platformType = AccessTools.TypeByName("Platform");
                if (platformType != null)
                {
                    object current = AccessTools.Property(platformType, "Current")?.GetValue(null)
                                  ?? AccessTools.Field(platformType, "Current")?.GetValue(null)
                                  ?? AccessTools.Field(platformType, "current")?.GetValue(null);

                    if (current != null)
                    {
                        var lsdObj = AccessTools.Property(current.GetType(), "LocalSharedData")?.GetValue(current, null)
                                  ?? AccessTools.Field(current.GetType(), "LocalSharedData")?.GetValue(current)
                                  ?? AccessTools.Field(current.GetType(), "localSharedData")?.GetValue(current);

                        if (lsdObj != null)
                        {
                            var t = lsdObj.GetType();
                            var m2 = t.GetMethod("GetString", new[] { typeof(string), typeof(string) });
                            if (m2 != null) return (string)m2.Invoke(lsdObj, new object[] { prefKey, fallback });
                        }
                    }
                }
            }
捕获 { }

            return PlayerPrefs.GetString(prefKey, fallback);
        }

        private static KeyCode ParseKeyCode(string s, KeyCode fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            s = s.Trim();
            switch (s.ToLowerInvariant())
            {
                case "up": return KeyCode.UpArrow;
                case "down": return KeyCode.DownArrow;
                case "left": return KeyCode.LeftArrow;
                case "right": return KeyCode.RightArrow;
                case "esc": return KeyCode.Escape;
                case "enter": return KeyCode.Return;
            }
            if (s.Length == 1 && char.IsLetter(s[0]))
            {
                if (Enum.TryParse(s.ToUpperInvariant(), true, out KeyCode kc1)) return kc1;
            }
            if (s.Length == 1 && char.IsDigit(s[0]))
            {
                return (KeyCode)((int)KeyCode.Alpha0 + (s[0] - '0'));
            }
            if (s.Length == 2 && (s[0] | 32) == 'd' && char.IsDigit(s[1]))
            {
                return (KeyCode)((int)KeyCode.Alpha0 + (s[1] - '0'));
            }
            if (Enum.TryParse(s, true, out KeyCode kc)) return kc;
返回回退值;

    }
}


// ==================== 坐椅子 Patch (逻辑未变，这部分通常不卡) ====================
[HarmonyPatch(typeof(ToolItemManager), "TryReplenishTools")]
公共静态类 BenchReplenishPatch
{
    private static bool isLoopingRefills = false;

    public static bool Prefix(ref bool doReplenish, ToolItemManager.ReplenishMethod method)
    {
        if (isLoopingRefills) return true;

        bool isBench = method.ToString().IndexOf("Bench", StringComparison.OrdinalIgnoreCase) >= 0;
如果(!isBench) 返回 true;



尝试
        {
            isLoopingRefills = true;
            var hero = HeroController.instance;
如果 (英雄 == 空或英雄.玩家数据 == 空) 返回 true;


列表<string>补给列表 = 新建列表<string>()
            {
                Gameplay.ToolmasterCrest.name,
                CrestKeyDirectionsV5.QUEEN_CREST_ID,
游戏玩法.法术徽章名称,

游戏玩法.死神徽章名称,
游戏玩法.战士徽章名称,
游戏玩法.流浪者徽章名称,
游戏玩法.女巫徽章名称
            };

foreach (var 徽章名称 in 补充列表)
            {
工具徽章 徽章 = 工具物品管理器.按名称获取徽章(徽章名称);
如果 (徽章 == 空或 !徽章.已解锁 或 徽章.已隐藏) 继续;


英雄.重置所有徽章状态();
工具物品管理器.设置已装备的徽章(徽章名称);
ToolItemManager.尝试补充工具(true, 方法);
            }

            hero.ResetAllCrestState();
ToolItemManager.设置已装备的徽章(原徽章);
            ToolItemManager.SendEquippedChangedEvent(true);

返回 true;
        }
        finally
        {
            isLoopingRefills = false;
        }
    }
}

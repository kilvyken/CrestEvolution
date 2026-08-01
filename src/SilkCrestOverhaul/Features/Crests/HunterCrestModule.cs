using BepInEx.Logging;
using SilkCrestOverhaul.Core.Combat;
using SilkCrestOverhaul.Core.Events;
using SilkCrestOverhaul.Core.State;
using SilkCrestOverhaul.Core.Time;
using SilkCrestOverhaul.GameInterop;

namespace SilkCrestOverhaul.Features.Crests;

public sealed class HunterCrestModule : ICrestModule
{
    private readonly ManualLogSource _log;
    private readonly GameEventHub _events;
    private readonly IGameApi _game;
    private readonly TimedStackCounter _combo;
    private bool _active;

    public HunterCrestModule(ManualLogSource log, GameEventHub events, IClock clock, IGameApi game)
    {
        _log = log; _events = events; _game = game;
        _combo = new TimedStackCounter(12, 10, clock);
        _combo.ThresholdReached += (_, e) =>
        {
            if (!_active) return;
            if (e.Threshold == 6) _game.SetTemporaryCharmLevelOffset(1, "HUN-002");
            if (e.Threshold == 12) _game.SetTemporaryCharmLevelOffset(2, "HUN-003");
        };
        _combo.Expired += (_, _) => ResetCombo();
    }

    public string CrestId => "hunter";

    public void Activate()
    {
        if (_active) return;
        _active = true;
        _events.AttackResolved += OnAttack;
        _events.BenchRested += ResetCombo;
        _events.PlayerDied += ResetCombo;
        _log.LogDebug("Hunter module activated.");
    }

    public void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _events.AttackResolved -= OnAttack;
        _events.BenchRested -= ResetCombo;
        _events.PlayerDied -= ResetCombo;
        ResetCombo();
        _game.SetTemporaryCharmLevelOffset(0, "hunter-deactivate");
    }

    public void Tick() => _combo.Tick();
    public void Dispose() => Deactivate();

    private void OnAttack(AttackEvent e)
    {
        if (e.IsAdditionalDamage) return;
        _combo.Add(1, refreshDuration: true, thresholds: new[] { 6, 12 });
        // HUN-005 should be applied through an attack-shape modifier and an invulnerability lease,
        // not by permanently changing the vanilla dash attack prefab.
    }

    private void ResetCombo()
    {
        _combo.Clear();
        _game.SetTemporaryCharmLevelOffset(0, "hunter-reset");
    }
}

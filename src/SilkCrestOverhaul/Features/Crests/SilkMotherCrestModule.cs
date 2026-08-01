using BepInEx.Logging;
using SilkCrestOverhaul.Core.Combat;
using SilkCrestOverhaul.Core.Events;
using SilkCrestOverhaul.GameInterop;

namespace SilkCrestOverhaul.Features.Crests;

public sealed class SilkMotherCrestModule : ICrestModule
{
    private readonly ManualLogSource _log;
    private readonly GameEventHub _events;
    private readonly IGameApi _game;
    private readonly SilkShieldPolicy _shield = new();
    private bool _active;
    private int _stage;

    public SilkMotherCrestModule(ManualLogSource log, GameEventHub events, IGameApi game)
    {
        _log = log; _events = events; _game = game;
    }

    public string CrestId => "silk-mother";

    public void Activate()
    {
        if (_active) return;
        _active = true;
        _events.PlayerDamageBefore += OnDamageBefore;
        _events.BenchRested += Reset;
        _events.PlayerDied += Reset;
    }

    public void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _events.PlayerDamageBefore -= OnDamageBefore;
        _events.BenchRested -= Reset;
        _events.PlayerDied -= Reset;
        Reset();
    }

    public void Tick() { }
    public void Dispose() => Deactivate();

    public bool TryIncreaseStage()
    {
        if (_stage >= 12) return false;
        if (!_game.TrySpendSilk(9, "MOT-stage-up")) return false;
        _stage++;
        _game.SetTemporaryCharmLevelOffset(1, "MOT-004");
        return true;
    }

    private void OnDamageBefore(PlayerDamageEvent e)
    {
        if (_stage <= 0) return;
        var result = _shield.Resolve(e.MaskDamage, _game.CurrentSilk, 3);
        if (result.SilkSpent > 0) _game.TrySpendSilk(result.SilkSpent, "MOT-002-silk-shield");
        if (result.ShouldExitEnhancement) Reset();
        // The actual patch adapter must replace/cancel vanilla damage using the result,
        // because records published after the fact cannot mutate the game call by themselves.
    }

    private void Reset()
    {
        _stage = 0;
        _game.SetTemporaryCharmLevelOffset(0, "silk-mother-reset");
    }
}

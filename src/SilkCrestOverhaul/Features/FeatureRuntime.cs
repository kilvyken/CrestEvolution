using System;
using BepInEx.Logging;
using SilkCrestOverhaul.Core.Events;
using SilkCrestOverhaul.GameInterop;

namespace SilkCrestOverhaul.Features;

public sealed class FeatureRuntime : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly GameEventHub _events;
    private readonly CrestModuleRegistry _registry;
    private readonly IGameApi _game;

    public FeatureRuntime(ManualLogSource log, GameEventHub events, CrestModuleRegistry registry, IGameApi game)
    {
        _log = log; _events = events; _registry = registry; _game = game;
    }

    public void Start()
    {
        _events.CrestChanged += OnCrestChanged;
        _events.PlayerDied += OnHardReset;
        _registry.Activate(_game.CurrentCrestId);
    }

    private void OnCrestChanged(CrestChangedEvent e) => _registry.Activate(e.Current);
    private void OnHardReset() { _registry.Active?.Deactivate(); _registry.Active?.Activate(); }

    public void Dispose()
    {
        _events.CrestChanged -= OnCrestChanged;
        _events.PlayerDied -= OnHardReset;
        _registry.Dispose();
    }
}

using System;
using BepInEx.Logging;
using SilkCrestOverhaul.Core.Combat;

namespace SilkCrestOverhaul.Core.Events;

public sealed class GameEventHub
{
    private readonly ManualLogSource _log;
    private readonly bool _trace;

    public GameEventHub(ManualLogSource log, bool trace)
    {
        _log = log;
        _trace = trace;
    }

    public event Action<AttackEvent>? AttackResolved;
    public event Action<PlayerDamageEvent>? PlayerDamageBefore;
    public event Action<PlayerDamageEvent>? PlayerDamageAfter;
    public event Action<HealEvent>? HealRequested;
    public event Action<HealEvent>? HealResolved;
    public event Action<ResourceEvent>? SilkChanged;
    public event Action<CrestChangedEvent>? CrestChanged;
    public event Action<EnemyKilledEvent>? EnemyKilled;
    public event Action? BenchRested;
    public event Action? PlayerDied;
    public event Action<string>? SceneChanged;

    public void Publish(AttackEvent e) { Trace(nameof(AttackResolved), e); AttackResolved?.Invoke(e); }
    public void PublishBefore(PlayerDamageEvent e) { Trace(nameof(PlayerDamageBefore), e); PlayerDamageBefore?.Invoke(e); }
    public void PublishAfter(PlayerDamageEvent e) { Trace(nameof(PlayerDamageAfter), e); PlayerDamageAfter?.Invoke(e); }
    public void PublishRequested(HealEvent e) { Trace(nameof(HealRequested), e); HealRequested?.Invoke(e); }
    public void PublishResolved(HealEvent e) { Trace(nameof(HealResolved), e); HealResolved?.Invoke(e); }
    public void Publish(ResourceEvent e) { Trace(nameof(SilkChanged), e); SilkChanged?.Invoke(e); }
    public void Publish(CrestChangedEvent e) { Trace(nameof(CrestChanged), e); CrestChanged?.Invoke(e); }
    public void Publish(EnemyKilledEvent e) { Trace(nameof(EnemyKilled), e); EnemyKilled?.Invoke(e); }
    public void PublishBenchRested() { Trace(nameof(BenchRested), ""); BenchRested?.Invoke(); }
    public void PublishPlayerDied() { Trace(nameof(PlayerDied), ""); PlayerDied?.Invoke(); }
    public void PublishSceneChanged(string scene) { Trace(nameof(SceneChanged), scene); SceneChanged?.Invoke(scene); }

    private void Trace(string name, object value)
    {
        if (_trace) _log.LogDebug($"[Event] {name}: {value}");
    }
}

public sealed record PlayerDamageEvent(int MaskDamage, string SourceTag, bool Cancelled = false);
public sealed record HealEvent(int Requested, int Applied, string SourceTag);
public sealed record ResourceEvent(int Previous, int Current, int Delta, string Reason);
public sealed record CrestChangedEvent(string Previous, string Current);
public sealed record EnemyKilledEvent(int EnemyInstanceId, string EnemyName);

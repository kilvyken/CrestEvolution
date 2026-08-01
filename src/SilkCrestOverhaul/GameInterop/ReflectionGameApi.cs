using System;
using BepInEx.Logging;
using SilkCrestOverhaul.Core.Combat;
using SilkCrestOverhaul.Core.Events;
using SilkCrestOverhaul.GameInterop.Binding;

namespace SilkCrestOverhaul.GameInterop;

public sealed class ReflectionGameApi : IGameApi
{
    private readonly ManualLogSource _log;
    private readonly GameEventHub _events;
    private readonly DynamicPatchResolver _resolver;

    public ReflectionGameApi(ManualLogSource log, GameEventHub events, DynamicPatchResolver resolver)
    {
        _log = log;
        _events = events;
        _resolver = resolver;
    }

    public string CurrentCrestId => "unbound";
    public int CurrentSilk => 0;

    public bool TrySpendSilk(int amount, string reason)
    {
        _log.LogWarning($"TrySpendSilk is not bound yet. amount={amount}, reason={reason}");
        return false;
    }

    public void AddSilk(int amount, string reason) =>
        _log.LogWarning($"AddSilk is not bound yet. amount={amount}, reason={reason}");

    public void SetTemporaryCharmLevelOffset(int offset, string source) =>
        _log.LogWarning($"Charm level projection is not bound yet. offset={offset}, source={source}");

    public IDisposable AcquireInvulnerability(string source, double maxDurationSeconds) =>
        new NoopDisposable();

    public bool TryInvokeVanillaAction(string bindingId)
    {
        _log.LogWarning($"Vanilla action is not bound: {bindingId}");
        return false;
    }

    public void SpawnAdditionalDamage(AdditionalDamageCommand command) =>
        _log.LogWarning($"Additional damage spawn is not bound: {command}");

    public void Dispose() { }
    private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}

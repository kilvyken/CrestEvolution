using System;
using SilkCrestOverhaul.Core.Combat;

namespace SilkCrestOverhaul.GameInterop;

public interface IGameApi : IDisposable
{
    string CurrentCrestId { get; }
    int CurrentSilk { get; }
    bool TrySpendSilk(int amount, string reason);
    void AddSilk(int amount, string reason);
    void SetTemporaryCharmLevelOffset(int offset, string source);
    IDisposable AcquireInvulnerability(string source, double maxDurationSeconds);
    bool TryInvokeVanillaAction(string bindingId);
    void SpawnAdditionalDamage(AdditionalDamageCommand command);
}

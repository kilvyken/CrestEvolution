using System;

namespace SilkCrestOverhaul.Core.Resources;

public sealed class VirtualToolService
{
    private readonly ResourceLedger _ledger;
    public VirtualToolService(ResourceLedger ledger) => _ledger = ledger;

    public bool TryUse(string toolId, string resource, int cost, Func<bool> invokeVanillaAction)
    {
        using var tx = _ledger.Reserve(resource, cost, $"virtual-tool:{toolId}");
        if (!tx.Success) return false;
        if (!invokeVanillaAction()) return false;
        tx.Commit();
        return true;
    }
}

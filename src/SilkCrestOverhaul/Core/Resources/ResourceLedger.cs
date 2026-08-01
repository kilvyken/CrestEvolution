using System;
using System.Collections.Generic;

namespace SilkCrestOverhaul.Core.Resources;

public sealed class ResourceLedger
{
    private readonly Dictionary<string, int> _balances = new(StringComparer.Ordinal);

    public int Get(string resource) => _balances.TryGetValue(resource, out var value) ? value : 0;
    public void Set(string resource, int value) => _balances[resource] = Math.Max(0, value);

    public ResourceTransaction Reserve(string resource, int amount, string reason)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var current = Get(resource);
        if (current < amount) return ResourceTransaction.Failed(resource, amount, reason);
        _balances[resource] = current - amount;
        return new ResourceTransaction(this, resource, amount, reason, true);
    }

    internal void Refund(string resource, int amount) => Set(resource, Get(resource) + amount);
}

public sealed class ResourceTransaction : IDisposable
{
    private readonly ResourceLedger? _ledger;
    private bool _committed;
    private bool _disposed;

    internal ResourceTransaction(ResourceLedger? ledger, string resource, int amount, string reason, bool success)
    {
        _ledger = ledger;
        Resource = resource;
        Amount = amount;
        Reason = reason;
        Success = success;
    }

    public string Resource { get; }
    public int Amount { get; }
    public string Reason { get; }
    public bool Success { get; }

    public static ResourceTransaction Failed(string resource, int amount, string reason) =>
        new(null, resource, amount, reason, false);

    public void Commit()
    {
        if (!Success) throw new InvalidOperationException("Cannot commit a failed transaction.");
        _committed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Success && !_committed) _ledger!.Refund(Resource, Amount);
    }
}

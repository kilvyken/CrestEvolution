using System;
using System.Collections.Generic;

namespace SilkCrestOverhaul.Features;

public sealed class CrestModuleRegistry : IDisposable
{
    private readonly Dictionary<string, ICrestModule> _modules = new(StringComparer.OrdinalIgnoreCase);
    public ICrestModule? Active { get; private set; }

    public void Register(ICrestModule module) => _modules.Add(module.CrestId, module);

    public void Activate(string crestId)
    {
        if (Active?.CrestId.Equals(crestId, StringComparison.OrdinalIgnoreCase) == true) return;
        Active?.Deactivate();
        Active = _modules.TryGetValue(crestId, out var module) ? module : null;
        Active?.Activate();
    }

    public void Tick() => Active?.Tick();
    public void Dispose()
    {
        Active?.Deactivate();
        foreach (var module in _modules.Values) module.Dispose();
        _modules.Clear();
    }
}

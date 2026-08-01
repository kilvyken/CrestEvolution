using System;
using System.Collections.Generic;

namespace SilkCrestOverhaul.Core.State;

public enum RageMode { None, Rage, SuperRage }

public sealed class CrestRuntimeState
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public string CrestId { get; private set; } = "none";
    public RageMode Rage { get; set; }
    public bool SpecialMode { get; set; }
    public long Revision { get; private set; }

    public void Activate(string crestId)
    {
        ResetTransient();
        CrestId = crestId;
        Revision++;
    }

    public T Get<T>(string key, T fallback = default!) =>
        _values.TryGetValue(key, out var value) && value is T typed ? typed : fallback;

    public void Set<T>(string key, T value)
    {
        _values[key] = value!;
        Revision++;
    }

    public void Remove(string key)
    {
        if (_values.Remove(key)) Revision++;
    }

    public void ResetTransient()
    {
        _values.Clear();
        Rage = RageMode.None;
        SpecialMode = false;
        Revision++;
    }
}

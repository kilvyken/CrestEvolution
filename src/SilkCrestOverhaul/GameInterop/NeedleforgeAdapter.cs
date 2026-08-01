using BepInEx.Logging;

namespace SilkCrestOverhaul.GameInterop;

/// <summary>
/// Keep every Needleforge reference in this adapter. Replace the placeholders only after
/// validating the exact local Needleforge API and license obligations.
/// </summary>
public sealed class NeedleforgeAdapter
{
    private readonly ManualLogSource _log;
    public NeedleforgeAdapter(ManualLogSource log) => _log = log;

    public bool IsAvailable => false; // Resolve plugin/package at runtime or compile with an optional symbol.
    public void RegisterUpgradeSlots() => _log.LogWarning("Needleforge adapter is not bound yet.");
    public void RegisterCustomCrest(string crestId) => _log.LogWarning($"Needleforge crest registration pending: {crestId}");
}

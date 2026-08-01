using BepInEx.Logging;

namespace SilkCrestOverhaul.Features.Crests;

public sealed class PlaceholderCrestModule : ICrestModule
{
    private readonly ManualLogSource _log;
    public PlaceholderCrestModule(string crestId, ManualLogSource log) { CrestId = crestId; _log = log; }
    public string CrestId { get; }
    public void Activate() => _log.LogDebug($"Activated placeholder crest module: {CrestId}");
    public void Deactivate() { }
    public void Tick() { }
    public void Dispose() { }
}

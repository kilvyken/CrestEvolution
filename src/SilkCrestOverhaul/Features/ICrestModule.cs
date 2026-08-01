using System;

namespace SilkCrestOverhaul.Features;

public interface ICrestModule : IDisposable
{
    string CrestId { get; }
    void Activate();
    void Deactivate();
    void Tick();
}

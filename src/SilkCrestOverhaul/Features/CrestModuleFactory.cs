using BepInEx.Configuration;
using BepInEx.Logging;
using SilkCrestOverhaul.Core.Events;
using SilkCrestOverhaul.Core.Time;
using SilkCrestOverhaul.Features.Crests;
using SilkCrestOverhaul.GameInterop;

namespace SilkCrestOverhaul.Features;

public static class CrestModuleFactory
{
    public static CrestModuleRegistry CreateDefaultRegistry(
        ManualLogSource log, GameEventHub events, IClock clock, IGameApi game, ConfigFile config)
    {
        var registry = new CrestModuleRegistry();
        registry.Register(new HunterCrestModule(log, events, clock, game));
        registry.Register(new PlaceholderCrestModule("reaper", log));
        registry.Register(new PlaceholderCrestModule("wanderer", log));
        registry.Register(new PlaceholderCrestModule("beast", log));
        registry.Register(new PlaceholderCrestModule("witch", log));
        registry.Register(new PlaceholderCrestModule("architect", log));
        registry.Register(new PlaceholderCrestModule("shaman", log));
        registry.Register(new SilkMotherCrestModule(log, events, game));
        registry.Register(new PlaceholderCrestModule("curse", log));
        registry.Register(new PlaceholderCrestModule("useless", log));
        return registry;
    }
}

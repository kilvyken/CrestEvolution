using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SilkCrestOverhaul.Core.Events;
using SilkCrestOverhaul.Core.Time;
using SilkCrestOverhaul.Features;
using SilkCrestOverhaul.Features.CrestSwitching;
using SilkCrestOverhaul.GameInterop;
using SilkCrestOverhaul.GameInterop.Binding;

namespace SilkCrestOverhaul.Bootstrap;

public sealed class ModBootstrap : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly ConfigFile _config;
    private readonly Harmony _harmony;
    private FeatureRuntime? _runtime;
    private IGameApi? _game;
    private CrestQuickSwitchIntegration? _crestQuickSwitch;

    public ModBootstrap(ManualLogSource log, ConfigFile config, Harmony harmony)
    {
        _log = log;
        _config = config;
        _harmony = harmony;
    }

    public void Start()
    {
        var enabled = _config.Bind("General", "Enabled", true, "Master switch.");
        var traceEvents = _config.Bind("Debug", "TraceEvents", false, "Verbose combat event logging.");
        if (!enabled.Value)
        {
            _log.LogInfo("Silk Crest Overhaul is disabled by configuration.");
            return;
        }

        var events = new GameEventHub(_log, traceEvents.Value);
        var clock = new UnityClock();
        var bindings = GameBindingConfig.LoadOrCreate(_log);
        var resolver = new DynamicPatchResolver(_log, bindings);
        _game = new ReflectionGameApi(_log, events, resolver);

        var registry = CrestModuleFactory.CreateDefaultRegistry(_log, events, clock, _game, _config);
        _runtime = new FeatureRuntime(_log, events, registry, _game);
        _runtime.Start();

        _crestQuickSwitch = CrestQuickSwitchIntegration.Install(_log, _config, _harmony);

        // Actual patch classes should be applied only after resolver diagnostics pass.
        resolver.WriteReport();
        _log.LogInfo("Silk Crest Overhaul architecture runtime started.");
    }

    public void Dispose()
    {
        _crestQuickSwitch?.Dispose();
        _runtime?.Dispose();
        _game?.Dispose();
    }
}

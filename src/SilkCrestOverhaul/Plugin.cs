using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SilkCrestOverhaul.Bootstrap;

namespace SilkCrestOverhaul;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "local.silkcrestoverhaul";
    public const string PluginName = "Silk Crest Overhaul";
    public const string PluginVersion = "0.1.0-architecture";

    private Harmony? _harmony;
    private ModBootstrap? _bootstrap;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);
        _bootstrap = new ModBootstrap(Logger, Config, _harmony);
        _bootstrap.Start();
    }

    private void OnDestroy()
    {
        try
        {
            _bootstrap?.Dispose();
        }
        finally
        {
            _harmony?.UnpatchSelf();
        }
    }
}

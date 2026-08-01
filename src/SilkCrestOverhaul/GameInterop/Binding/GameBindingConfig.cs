using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;

namespace SilkCrestOverhaul.GameInterop.Binding;

public sealed class GameBindingConfig
{
    public Dictionary<string, MethodBindingSpec> Methods { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string[]> Assets { get; set; } = new(StringComparer.Ordinal);

    public static GameBindingConfig LoadOrCreate(ManualLogSource log)
    {
        var path = Path.Combine(Paths.ConfigPath, "SilkCrestOverhaul", "game-bindings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            var seed = new GameBindingConfig();
            File.WriteAllText(path, JsonSerializer.Serialize(seed, new JsonSerializerOptions { WriteIndented = true }));
            log.LogWarning($"Created empty binding file: {path}");
            return seed;
        }

        try
        {
            return JsonSerializer.Deserialize<GameBindingConfig>(File.ReadAllText(path)) ?? new GameBindingConfig();
        }
        catch (Exception ex)
        {
            log.LogError($"Failed to parse game bindings: {ex}");
            return new GameBindingConfig();
        }
    }
}

public sealed class MethodBindingSpec
{
    public string[] AssemblyCandidates { get; set; } = Array.Empty<string>();
    public string[] TypeCandidates { get; set; } = Array.Empty<string>();
    public string[] MethodCandidates { get; set; } = Array.Empty<string>();
    public string[] ParameterTypeNames { get; set; } = Array.Empty<string>();
    public string? ReturnTypeName { get; set; }
    public bool Required { get; set; }
}

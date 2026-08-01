using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SilkCrestOverhaul.GameInterop.Binding;

public sealed class DynamicPatchResolver
{
    private readonly ManualLogSource _log;
    private readonly GameBindingConfig _config;
    private readonly List<BindingReportEntry> _report = new();

    public DynamicPatchResolver(ManualLogSource log, GameBindingConfig config)
    {
        _log = log;
        _config = config;
    }

    public MethodBase? Resolve(string bindingId)
    {
        if (!_config.Methods.TryGetValue(bindingId, out var spec))
        {
            Record(bindingId, "missing-spec", Array.Empty<string>());
            return null;
        }

        var matches = new List<MethodBase>();
        foreach (var typeName in spec.TypeCandidates)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type is null) continue;
            foreach (var methodName in spec.MethodCandidates)
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == methodName && SignatureMatches(m, spec));
                matches.AddRange(methods);
            }
        }

        matches = matches.Distinct().ToList();
        if (matches.Count == 1)
        {
            Record(bindingId, "resolved", new[] { Describe(matches[0]) });
            return matches[0];
        }

        var status = matches.Count == 0 ? "not-found" : "ambiguous";
        Record(bindingId, status, matches.Select(Describe).ToArray());
        if (spec.Required) _log.LogError($"Required binding {bindingId} is {status}.");
        else _log.LogWarning($"Optional binding {bindingId} is {status}.");
        return null;
    }

    public void WriteReport()
    {
        var dir = Path.Combine(Paths.ConfigPath, "SilkCrestOverhaul");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "binding-report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool SignatureMatches(MethodInfo method, MethodBindingSpec spec)
    {
        var parameters = method.GetParameters();
        if (spec.ParameterTypeNames.Length > 0)
        {
            if (parameters.Length != spec.ParameterTypeNames.Length) return false;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.FullName != spec.ParameterTypeNames[i]) return false;
            }
        }
        if (spec.ReturnTypeName is not null && method.ReturnType.FullName != spec.ReturnTypeName) return false;
        return true;
    }

    private void Record(string id, string status, string[] candidates) =>
        _report.Add(new BindingReportEntry(id, status, candidates));

    private static string Describe(MethodBase m) => $"{m.DeclaringType?.FullName}::{m}";
}

public sealed record BindingReportEntry(string BindingId, string Status, string[] Candidates);

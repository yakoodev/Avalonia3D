using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Avalonia3D.Sandbox.Scenes;

public sealed record PbrQaAssetEntry(
    string RelativePath,
    string DisplayName,
    bool IncludeInRegressionScene,
    bool IncludeInSnapshotChecks,
    string[] KnownArtifacts,
    float MinMeanBrightness,
    float MaxMeanBrightness,
    float MaxNearWhiteRatio);

public static class PbrQaAssetRegistry
{
    private const string RegistryFileName = "PBR_QA_ASSETS.json";

    public static IReadOnlyList<PbrQaAssetEntry> Load(string assetsRoot)
    {
        if (string.IsNullOrWhiteSpace(assetsRoot))
        {
            return [];
        }

        var path = Path.Combine(assetsRoot, RegistryFileName);
        if (!File.Exists(path))
        {
            Log.Warning("PBR QA asset registry is missing: {Path}", path);
            return [];
        }

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payload = JsonSerializer.Deserialize<PbrQaAssetRegistryPayload>(json, options);
            if (payload?.Assets == null || payload.Assets.Count == 0)
            {
                Log.Warning("PBR QA asset registry is empty: {Path}", path);
                return [];
            }

            return payload.Assets;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse PBR QA asset registry: {Path}", path);
            return [];
        }
    }

    private sealed class PbrQaAssetRegistryPayload
    {
        public List<PbrQaAssetEntry> Assets { get; set; } = [];
    }
}

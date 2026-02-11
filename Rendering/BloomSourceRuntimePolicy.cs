using System;

namespace Avalonia3D.Rendering;

/// <summary>
/// Centralized runtime adjustment policy for bloom settings based on active bloom source.
/// Keeps source-specific attenuation in one place.
/// </summary>
public static class BloomSourceRuntimePolicy
{
    public static (float Threshold, float Intensity, float MinContribution) Resolve(
        (float Threshold, float Intensity, float MinContribution) runtime,
        BloomProfile bloom,
        string sourceTag)
    {
        if (string.IsNullOrWhiteSpace(sourceTag))
        {
            return runtime;
        }

        if (sourceTag.StartsWith("emissive", StringComparison.OrdinalIgnoreCase))
        {
            var threshold = runtime.Threshold * Math.Clamp(bloom.EmissivePrimaryThresholdScale, 0.5f, 4f);
            var intensity = runtime.Intensity * Math.Clamp(bloom.EmissivePrimaryIntensityScale, 0f, 1f);

            if (sourceTag.Contains("+color", StringComparison.OrdinalIgnoreCase))
            {
                intensity *= Math.Clamp(bloom.EmissivePrimaryWithColorIntensityScale, 0f, 1f);
            }

            return (
                Math.Clamp(threshold, 0f, 16f),
                Math.Clamp(intensity, 0f, 8f),
                Math.Clamp(bloom.EmissivePrimaryMinContribution, 0f, 1f));
        }

        return runtime;
    }
}

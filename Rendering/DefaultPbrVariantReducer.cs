using Avalonia3D.Shaders;
using System.Collections.Generic;

namespace Avalonia3D.Rendering;

/// <summary>
/// Default capability-aware reducer for runtime PBR variants.
/// Removes extension-heavy flags first and then map-specific extension flags.
/// </summary>
public sealed class DefaultPbrVariantReducer : IPbrVariantReducer
{
    private static readonly PbrFeatures[] ReductionOrder =
    [
        PbrFeatures.VolumeThicknessMap,
        PbrFeatures.TransmissionMap,
        PbrFeatures.SpecularColorMap,
        PbrFeatures.SpecularMap,
        PbrFeatures.SheenRoughnessMap,
        PbrFeatures.SheenColorMap,
        PbrFeatures.ClearcoatNormalMap,
        PbrFeatures.ClearcoatRoughnessMap,
        PbrFeatures.ClearcoatMap,
        PbrFeatures.EmissiveStrength,
        PbrFeatures.Ior,
        PbrFeatures.Specular,
        PbrFeatures.Sheen,
        PbrFeatures.Clearcoat,
        PbrFeatures.Transmission,
        PbrFeatures.ReflectionsIbl,
        PbrFeatures.EmissiveMap,
        PbrFeatures.OcclusionMap,
        PbrFeatures.MetallicRoughnessMap,
        PbrFeatures.NormalMap,
        PbrFeatures.BaseColorMap
    ];

    public IEnumerable<PbrFeatures> GetReductionChain(PbrFeatures requestedFeatures)
    {
        var current = requestedFeatures;

        foreach (var feature in ReductionOrder)
        {
            if (!current.HasFlag(feature))
            {
                continue;
            }

            current &= ~feature;
            yield return current;
        }
    }
}

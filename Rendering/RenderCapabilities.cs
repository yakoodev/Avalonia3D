using Avalonia3D.Shaders;

namespace Avalonia3D.Rendering;

public sealed record RenderCapabilities(MaterialFeatureSet SupportedMaterialFeatures) : IRenderCapabilities
{
    public static RenderCapabilities Default { get; } = new(MaterialFeatureSetExtensions.PbrFeatureMask);

    public bool Supports(MaterialFeatureSet features)
    {
        return (features & ~SupportedMaterialFeatures) == MaterialFeatureSet.None;
    }
}

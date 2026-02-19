using Avalonia3D.Shaders;

namespace Avalonia3D.Rendering;

public interface IRenderCapabilities
{
    MaterialFeatureSet SupportedMaterialFeatures { get; }

    bool Supports(MaterialFeatureSet features);
}

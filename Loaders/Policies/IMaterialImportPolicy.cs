using Avalonia3D.Model;
using Avalonia3D.Rendering;

namespace Avalonia3D.Loaders.Policies;

public enum MaterialColorSpaceHandling
{
    Default,
    ForceSrgb,
    ForceLinear
}

public enum MaterialEmissiveBehavior
{
    IgnoreForTransparencyFallback,
    TreatAsTransparencySignal
}

public interface IMaterialImportPolicy
{
    MaterialAlphaMode ResolveAlphaMode(Material material, MaterialImportPolicyContext context);
    MaterialColorSpaceHandling ResolveColorSpaceHandling(Material material, TextureSemantic semantic, MaterialImportPolicyContext context);
    MaterialEmissiveBehavior ResolveEmissiveBehavior(Material material, MaterialImportPolicyContext context);
}

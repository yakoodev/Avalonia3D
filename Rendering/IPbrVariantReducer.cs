using Avalonia3D.Shaders;
using System.Collections.Generic;

namespace Avalonia3D.Rendering;

/// <summary>
/// Provides a chain of reduced PBR feature sets to retry runtime shader compilation
/// when a full-feature variant cannot compile on the current GPU capabilities.
/// </summary>
public interface IPbrVariantReducer
{
    IEnumerable<PbrFeatures> GetReductionChain(PbrFeatures requestedFeatures);
}


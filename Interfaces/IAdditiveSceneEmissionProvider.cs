using System.Numerics;

namespace Avalonia3D.Interfaces;

/// <summary>
/// Optional runtime contract for scene objects that want to inject additive scene emission
/// into shader uniform uEmissionColor.
/// </summary>
public interface IAdditiveSceneEmissionProvider
{
    bool HasAdditiveSceneEmission { get; }
    Vector3 AdditiveSceneEmissionColor { get; }
}

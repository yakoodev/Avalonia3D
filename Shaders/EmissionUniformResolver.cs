using System.Numerics;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Shaders;

/// <summary>
/// Centralized policy for resolving additive scene emission that is passed via uEmissionColor.
/// Keeping this logic in one place makes future emissive/fallback tuning easier.
/// </summary>
public static class EmissionUniformResolver
{
    public static Vector3 ResolveSceneEmissionColor(Material? material, SceneObject sceneObject)
    {
        if (sceneObject is IAdditiveSceneEmissionProvider provider && provider.HasAdditiveSceneEmission)
        {
            return provider.AdditiveSceneEmissionColor;
        }

        return material == null
            ? sceneObject.EmissionColor
            : Vector3.Zero;
    }
}

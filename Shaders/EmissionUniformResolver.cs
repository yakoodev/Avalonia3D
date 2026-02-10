using System.Numerics;
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
        // Scene emission is an additive runtime channel and should remain active
        // even when a material exists (e.g. morph-driven emissive fallback).
        _ = material;
        return sceneObject.EmissionColor;
    }
}

using System;
using System.Numerics;

namespace Avalonia3D.Model
{
    public readonly record struct MorphDrivenEmissionResult(
        Vector3 EmissiveFactor,
        float EmissiveIntensity,
        Vector3 SceneEmissionColor);

    /// <summary>
    /// Centralized mapping from normalized morph activation to emissive response.
    /// Keeps fallback behavior in one place for future tuning/extensions.
    /// </summary>
    public sealed class MorphDrivenEmissionComposer
    {
        public float MaterialIntensityBoost { get; init; } = 4f;
        public float SceneRedBoost { get; init; } = 2.5f;

        public MorphDrivenEmissionResult Compose(
            Vector3 baseEmissiveFactor,
            float baseEmissiveIntensity,
            Vector3 baseSceneEmissionColor,
            float normalizedActivation)
        {
            var a = Math.Clamp(normalizedActivation, 0f, 1f);
            var dim = 1f - a;

            var factor = new Vector3(
                MathF.Max(baseEmissiveFactor.X, a),
                baseEmissiveFactor.Y * dim,
                baseEmissiveFactor.Z * dim);

            var intensity = MathF.Max(baseEmissiveIntensity, baseEmissiveIntensity + (a * MaterialIntensityBoost));

            var sceneEmission = new Vector3(
                MathF.Max(baseSceneEmissionColor.X, a * SceneRedBoost),
                baseSceneEmissionColor.Y * dim,
                baseSceneEmissionColor.Z * dim);

            return new MorphDrivenEmissionResult(factor, intensity, sceneEmission);
        }
    }
}

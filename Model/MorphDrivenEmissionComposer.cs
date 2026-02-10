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
        public float SceneActivationThreshold { get; init; } = 0.05f;
        public float SceneRedAtThreshold { get; init; } = 3.5f;
        public float SceneRedAtFullActivation { get; init; } = 9f;

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

            var sceneActivation = a <= SceneActivationThreshold
                ? 0f
                : (a - SceneActivationThreshold) / MathF.Max(1f - SceneActivationThreshold, 0.0001f);

            var sceneRed = sceneActivation <= 0f
                ? baseSceneEmissionColor.X
                : MathF.Max(
                    baseSceneEmissionColor.X,
                    SceneRedAtThreshold + ((SceneRedAtFullActivation - SceneRedAtThreshold) * sceneActivation));

            var sceneEmission = new Vector3(
                sceneRed,
                baseSceneEmissionColor.Y * dim,
                baseSceneEmissionColor.Z * dim);

            return new MorphDrivenEmissionResult(factor, intensity, sceneEmission);
        }
    }
}

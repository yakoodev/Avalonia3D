using System;

namespace Avalonia3D.Rendering;

/// <summary>
/// Centralized policy for how much non-emissive color is mixed into bloom extraction.
/// Keeps bloom tuning isolated and easy to iterate without touching pass internals.
/// </summary>
public static class BloomSecondaryContributionPolicy
{
    public static float Resolve(ShaderRenderMode renderMode, BloomProfile bloomProfile, bool hasEmissivePrimary)
    {
        var configured = Math.Clamp(bloomProfile.ColorAdditiveContribution, 0f, 1f);

        if (!hasEmissivePrimary)
        {
            return configured;
        }

        // When emissive is already provided as a primary bloom source,
        // aggressive scene-color additive contribution tends to wash the frame to white.
        // Keep it strongly attenuated and configurable via profile scale.
        var scale = Math.Clamp(bloomProfile.ColorAdditiveWhenEmissivePresentScale, 0f, 1f);

        if (renderMode is ShaderRenderMode.Default or ShaderRenderMode.Pbr)
        {
            return configured * scale;
        }

        return configured;
    }
}

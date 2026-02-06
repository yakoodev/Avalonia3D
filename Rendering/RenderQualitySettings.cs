using System;

namespace Avalonia3D.Rendering
{
    [Flags]
    public enum PostEffectsFlags
    {
        None = 0,
        ToneMapping = 1 << 0,
        GammaCorrection = 1 << 1
    }

    public enum ToneMappingOperator
    {
        None = 0,
        Reinhard = 1
    }

    public enum MsaaPolicy
    {
        Disabled = 0,
        X2 = 2,
        X4 = 4,
        X8 = 8
    }

    public enum RenderQualityPreset
    {
        Low,
        Medium,
        High,
        Custom
    }

    public sealed record RenderQualitySettings
    {
        public const int MinShadowMapSize = 256;
        public const int MaxShadowMapSize = 8192;

        public bool ShadowsEnabled { get; init; } = true;
        public int ShadowMapSize { get; init; } = 2048;
        public PostEffectsFlags PostEffects { get; init; } = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection;
        public ToneMappingOperator ToneMapping { get; init; } = ToneMappingOperator.Reinhard;
        public float Gamma { get; init; } = 2.2f;
        public MsaaPolicy MsaaPolicy { get; init; } = MsaaPolicy.X4;

        public static RenderQualitySettings Low => new()
        {
            ShadowsEnabled = false,
            ShadowMapSize = 1024,
            PostEffects = PostEffectsFlags.GammaCorrection,
            ToneMapping = ToneMappingOperator.None,
            Gamma = 2.0f,
            MsaaPolicy = MsaaPolicy.Disabled
        };

        public static RenderQualitySettings Medium => new()
        {
            ShadowsEnabled = true,
            ShadowMapSize = 1536,
            PostEffects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection,
            ToneMapping = ToneMappingOperator.Reinhard,
            Gamma = 2.2f,
            MsaaPolicy = MsaaPolicy.X2
        };

        public static RenderQualitySettings High => new()
        {
            ShadowsEnabled = true,
            ShadowMapSize = 4096,
            PostEffects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection,
            ToneMapping = ToneMappingOperator.Reinhard,
            Gamma = 2.2f,
            MsaaPolicy = MsaaPolicy.X4
        };

        public RenderQualitySettings Validate()
        {
            var clampedShadowMapSize = Math.Clamp(ShadowMapSize, MinShadowMapSize, MaxShadowMapSize);
            var gamma = Math.Clamp(Gamma, 1.0f, 3.0f);

            var toneMapping = ToneMapping;
            if (!PostEffects.HasFlag(PostEffectsFlags.ToneMapping))
            {
                toneMapping = ToneMappingOperator.None;
            }

            return this with
            {
                ShadowMapSize = clampedShadowMapSize,
                Gamma = gamma,
                ToneMapping = toneMapping
            };
        }

        public static RenderQualitySettings FromPreset(RenderQualityPreset preset, RenderQualitySettings? current = null)
        {
            return preset switch
            {
                RenderQualityPreset.Low => Low,
                RenderQualityPreset.Medium => Medium,
                RenderQualityPreset.High => High,
                RenderQualityPreset.Custom => current ?? Medium,
                _ => Medium
            };
        }
    }
}

using System;

namespace Avalonia3D.Rendering
{
    [Flags]
    public enum PostEffectsFlags
    {
        None = 0,
        ToneMapping = 1 << 0,
        GammaCorrection = 1 << 1,
        Bloom = 1 << 2
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

    public enum ReflectionMode
    {
        IBL,
        ScreenSpace,
        Planar,
        Off
    }

    public enum RenderQualityPreset
    {
        Low,
        Medium,
        High,
        Ultra,
        PbrDebugNeutral,
        Custom
    }

    // Legacy/compatibility adapter. New orchestration should use GraphicsProfile directly.
    public sealed record RenderQualitySettings
    {
        public const int MinShadowMapSize = 256;
        public const int MaxShadowMapSize = 8192;
        public const int MinLights = 1;
        public const int MaxSupportedLights = 16;
        public const int DefaultMaxLights = 4;

        public bool ShadowsEnabled { get; init; } = true;
        public int ShadowMapSize { get; init; } = 2048;
        public PostEffectsFlags PostEffects { get; init; } = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection | PostEffectsFlags.Bloom;
        public ToneMappingOperator ToneMapping { get; init; } = ToneMappingOperator.Reinhard;
        public float Gamma { get; init; } = 2.2f;
        public bool BloomEnabled { get; init; } = true;
        public float BloomThreshold { get; init; } = 0.3f;
        public float BloomIntensity { get; init; } = 1.5f;
        public float BloomRadius { get; init; } = 1.0f;
        public int BloomIterations { get; init; } = 4;
        public MsaaPolicy MsaaPolicy { get; init; } = MsaaPolicy.X4;
        public bool ReflectionsEnabled { get; init; } = true;
        public ReflectionMode ReflectionMode { get; init; } = ReflectionMode.IBL;
        public float ReflectionIntensity { get; init; } = 0.35f;
        public string? EnvironmentMapPath { get; init; }
        public int MaxLights { get; init; } = DefaultMaxLights;

        public static RenderQualitySettings Low => FromProfile(GraphicsProfile.Low);
        public static RenderQualitySettings Medium => FromProfile(GraphicsProfile.Medium);
        public static RenderQualitySettings High => FromProfile(GraphicsProfile.High);
        public static RenderQualitySettings Ultra => FromProfile(GraphicsProfile.Ultra);

        public GraphicsProfile ToProfile(string profileName = "LegacySettings")
        {
            return new GraphicsProfile
            {
                Name = profileName,
                MsaaPolicy = MsaaPolicy,
                Shadows = new ShadowProfile { Enabled = ShadowsEnabled, MapSize = ShadowMapSize },
                PostFx = new PostFxProfile
                {
                    Effects = PostEffects,
                    ToneMapping = ToneMapping,
                    Gamma = Gamma,
                    Bloom = new BloomProfile
                    {
                        Enabled = BloomEnabled,
                        Threshold = BloomThreshold,
                        Intensity = BloomIntensity,
                        Radius = BloomRadius,
                        Iterations = BloomIterations
                    }
                },
                Reflections = new ReflectionProfile
                {
                    Enabled = ReflectionsEnabled,
                    Mode = ReflectionMode,
                    Intensity = ReflectionIntensity,
                    EnvironmentMapPath = EnvironmentMapPath
                },
                MaxLights = MaxLights
            }.Validate();
        }

        public static RenderQualitySettings FromProfile(GraphicsProfile profile)
        {
            var validated = (profile ?? GraphicsProfile.Medium).Validate();
            return new RenderQualitySettings
            {
                ShadowsEnabled = validated.Shadows.Enabled,
                ShadowMapSize = validated.Shadows.MapSize,
                PostEffects = validated.PostFx.Effects,
                ToneMapping = validated.PostFx.ToneMapping,
                Gamma = validated.PostFx.Gamma,
                BloomEnabled = validated.PostFx.Bloom.Enabled,
                BloomThreshold = validated.PostFx.Bloom.Threshold,
                BloomIntensity = validated.PostFx.Bloom.Intensity,
                BloomRadius = validated.PostFx.Bloom.Radius,
                BloomIterations = validated.PostFx.Bloom.Iterations,
                MsaaPolicy = validated.MsaaPolicy,
                ReflectionsEnabled = validated.Reflections.Enabled,
                ReflectionMode = validated.Reflections.Mode,
                ReflectionIntensity = validated.Reflections.Intensity,
                EnvironmentMapPath = validated.Reflections.EnvironmentMapPath,
                MaxLights = validated.MaxLights
            };
        }

        public RenderQualitySettings Validate() => FromProfile(ToProfile());

        public static RenderQualitySettings FromPreset(RenderQualityPreset preset, RenderQualitySettings? current = null)
        {
            var currentProfile = current?.ToProfile("Custom");
            return FromProfile(GraphicsProfile.FromPreset(preset, currentProfile));
        }
    }
}

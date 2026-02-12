using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia3D.Rendering
{
    public sealed record ShadowProfile
    {
        public bool Enabled { get; init; } = true;
        public int MapSize { get; init; } = 2048;
    }

    public sealed record BloomProfile
    {
        public bool Enabled { get; init; } = true;
        public float Threshold { get; init; } = 0.3f;
        public float Intensity { get; init; } = 1.5f;
        public float Radius { get; init; } = 1.0f;
        public int Iterations { get; init; } = 4;
        public float SoftKnee { get; init; } = 0.75f;
        public float NormalizationBoost { get; init; } = 1.8f;
        public float EmissiveMinContribution { get; init; } = 0.035f;
        public float UnlitIntensityBoost { get; init; } = 2.5f;
        public float ColorAdditiveContribution { get; init; } = 0.2f;
    }

    public sealed record PostFxProfile
    {
        public PostEffectsFlags Effects { get; init; } = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection;
        public ToneMappingOperator ToneMapping { get; init; } = ToneMappingOperator.Reinhard;
        public float Gamma { get; init; } = 2.2f;
        public BloomProfile Bloom { get; init; } = new();
    }

    public sealed record ReflectionProfile
    {
        public bool Enabled { get; init; } = true;
        public ReflectionMode Mode { get; init; } = ReflectionMode.IBL;
        public float Intensity { get; init; } = 0.35f;
        public string? EnvironmentMapPath { get; init; }
    }

    public sealed record PbrTuningProfile
    {
        public float Exposure { get; init; } = 1.0f;
        public float PbrWhitePoint { get; init; } = 1.0f;
        public float IblDiffuseIntensity { get; init; } = 0.2f;
        public float IblSpecularIntensity { get; init; } = 1.0f;
        public float ReflectionContributionClamp { get; init; } = 1.25f;
        public float AmbientStrengthClamp { get; init; } = 0.35f;
        public float AmbientOcclusionStrength { get; init; } = 1.0f;
        public float SeparateEmissiveSurfaceScale { get; init; } = 0.05f;
    }

    public sealed record BackgroundProfile
    {
        public float Red { get; init; } = 0.06f;
        public float Green { get; init; } = 0.06f;
        public float Blue { get; init; } = 0.08f;
    }

    public sealed record GraphicsProfile
    {
        public const string DefaultEnvironmentMapPath = "Assets/Environment/neutral_studio_fallback.ppm";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public string Name { get; init; } = "Medium";
        public RenderQualityPreset QualityPreset { get; init; } = RenderQualityPreset.Medium;
        public MsaaPolicy MsaaPolicy { get; init; } = MsaaPolicy.X2;
        public ShadowProfile Shadows { get; init; } = new();
        public PostFxProfile PostFx { get; init; } = new();
        public ReflectionProfile Reflections { get; init; } = new();
        public PbrTuningProfile PbrTuning { get; init; } = new();
        public BackgroundProfile Background { get; init; } = new();
        public int MaxLights { get; init; } = RenderQualitySettings.DefaultMaxLights;

        public static GraphicsProfile Low => new()
        {
            Name = "Low",
            QualityPreset = RenderQualityPreset.Low,
            MsaaPolicy = MsaaPolicy.Disabled,
            Shadows = new ShadowProfile { Enabled = false, MapSize = 1024 },
            PostFx = new PostFxProfile
            {
                Effects = PostEffectsFlags.GammaCorrection,
                ToneMapping = ToneMappingOperator.None,
                Gamma = 2.1f,
                Bloom = new BloomProfile
                {
                    Enabled = false,
                    Threshold = 0.25f,
                    Intensity = 0.8f,
                    Radius = 0.75f,
                    Iterations = 2,
                    SoftKnee = 0.75f,
                    NormalizationBoost = 1.6f,
                    EmissiveMinContribution = 0.02f,
                    UnlitIntensityBoost = 2.0f,
                    ColorAdditiveContribution = 0.2f
                }
            },
            Reflections = new ReflectionProfile
            {
                Enabled = false,
                Mode = ReflectionMode.Off,
                Intensity = 0f,
                EnvironmentMapPath = null
            },
            PbrTuning = new PbrTuningProfile
            {
                Exposure = 0.95f,
                PbrWhitePoint = 1.15f,
                IblDiffuseIntensity = 0f,
                IblSpecularIntensity = 0f,
                ReflectionContributionClamp = 0.7f,
                AmbientStrengthClamp = 0.25f,
                AmbientOcclusionStrength = 0.8f,
                SeparateEmissiveSurfaceScale = 0.2f
            },
            Background = new BackgroundProfile { Red = 0.09f, Green = 0.09f, Blue = 0.10f },
            MaxLights = 2
        };

        public static GraphicsProfile Medium => new()
        {
            Name = "Medium",
            QualityPreset = RenderQualityPreset.Medium,
            MsaaPolicy = MsaaPolicy.X2,
            Shadows = new ShadowProfile { Enabled = true, MapSize = 2048 },
            PostFx = new PostFxProfile
            {
                Effects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection | PostEffectsFlags.Bloom,
                ToneMapping = ToneMappingOperator.Reinhard,
                Gamma = 2.2f,
                Bloom = new BloomProfile
                {
                    Enabled = true,
                    Threshold = 0.42f,
                    Intensity = 1.25f,
                    Radius = 0.9f,
                    Iterations = 3,
                    SoftKnee = 0.75f,
                    NormalizationBoost = 1.8f,
                    EmissiveMinContribution = 0.03f,
                    UnlitIntensityBoost = 2.3f,
                    ColorAdditiveContribution = 0.35f
                }
            },
            Reflections = new ReflectionProfile
            {
                Enabled = true,
                Mode = ReflectionMode.IBL,
                Intensity = 0.35f,
                EnvironmentMapPath = DefaultEnvironmentMapPath
            },
            PbrTuning = new PbrTuningProfile
            {
                Exposure = 1.0f,
                PbrWhitePoint = 1.2f,
                IblDiffuseIntensity = 0.2f,
                IblSpecularIntensity = 0.75f,
                ReflectionContributionClamp = 1.05f,
                AmbientStrengthClamp = 0.30f,
                AmbientOcclusionStrength = 1.0f,
                SeparateEmissiveSurfaceScale = 0.2f
            },
            Background = new BackgroundProfile { Red = 0.06f, Green = 0.06f, Blue = 0.08f },
            MaxLights = 4
        };

        public static GraphicsProfile High => new()
        {
            Name = "High",
            QualityPreset = RenderQualityPreset.High,
            MsaaPolicy = MsaaPolicy.X4,
            Shadows = new ShadowProfile { Enabled = true, MapSize = 4096 },
            PostFx = new PostFxProfile
            {
                Effects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection | PostEffectsFlags.Bloom,
                ToneMapping = ToneMappingOperator.Reinhard,
                Gamma = 2.2f,
                Bloom = new BloomProfile
                {
                    Enabled = true,
                    Threshold = 0.28f,
                    Intensity = 1.7f,
                    Radius = 1.1f,
                    Iterations = 4,
                    SoftKnee = 0.75f,
                    NormalizationBoost = 1.9f,
                    EmissiveMinContribution = 0.035f,
                    UnlitIntensityBoost = 2.5f,
                    ColorAdditiveContribution = 0.45f
                }
            },
            Reflections = new ReflectionProfile
            {
                Enabled = true,
                Mode = ReflectionMode.IBL,
                Intensity = 0.5f,
                EnvironmentMapPath = DefaultEnvironmentMapPath
            },
            PbrTuning = new PbrTuningProfile
            {
                Exposure = 1.08f,
                PbrWhitePoint = 1.28f,
                IblDiffuseIntensity = 0.25f,
                IblSpecularIntensity = 1.0f,
                ReflectionContributionClamp = 1.2f,
                AmbientStrengthClamp = 0.35f,
                AmbientOcclusionStrength = 1.05f,
                SeparateEmissiveSurfaceScale = 0.2f
            },
            Background = new BackgroundProfile { Red = 0.04f, Green = 0.05f, Blue = 0.07f },
            MaxLights = 8
        };



        public static GraphicsProfile Ultra => new()
        {
            Name = "Ultra",
            QualityPreset = RenderQualityPreset.Ultra,
            MsaaPolicy = MsaaPolicy.X8,
            Shadows = new ShadowProfile { Enabled = true, MapSize = 8192 },
            PostFx = new PostFxProfile
            {
                Effects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection | PostEffectsFlags.Bloom,
                ToneMapping = ToneMappingOperator.Reinhard,
                Gamma = 2.2f,
                Bloom = new BloomProfile
                {
                    Enabled = true,
                    Threshold = 0.35f,
                    Intensity = 1.2f,
                    Radius = 1.4f,
                    Iterations = 6,
                    SoftKnee = 0.55f,
                    NormalizationBoost = 1.2f,
                    EmissiveMinContribution = 0.04f,
                    UnlitIntensityBoost = 2.5f,
                    ColorAdditiveContribution = 0.2f
                }
            },
            Reflections = new ReflectionProfile
            {
                Enabled = true,
                Mode = ReflectionMode.IBL,
                Intensity = 0.75f,
                EnvironmentMapPath = DefaultEnvironmentMapPath
            },
            PbrTuning = new PbrTuningProfile
            {
                Exposure = 1.0f,
                PbrWhitePoint = 1.35f,
                IblDiffuseIntensity = 0.15f,
                IblSpecularIntensity = 0.95f,
                ReflectionContributionClamp = 1.0f,
                AmbientStrengthClamp = 0.22f,
                AmbientOcclusionStrength = 1.0f
            },
            Background = new BackgroundProfile { Red = 0.02f, Green = 0.03f, Blue = 0.05f },
            MaxLights = RenderQualitySettings.MaxSupportedLights
        };

        public static GraphicsProfile PbrDebugNeutral => new()
        {
            Name = "PBR Debug Neutral",
            QualityPreset = RenderQualityPreset.PbrDebugNeutral,
            MsaaPolicy = MsaaPolicy.X2,
            Shadows = new ShadowProfile { Enabled = true, MapSize = 2048 },
            PostFx = new PostFxProfile
            {
                Effects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection,
                ToneMapping = ToneMappingOperator.Reinhard,
                Gamma = 2.2f,
                Bloom = new BloomProfile { Enabled = false }
            },
            Reflections = new ReflectionProfile
            {
                Enabled = true,
                Mode = ReflectionMode.IBL,
                Intensity = 0.4f,
                EnvironmentMapPath = DefaultEnvironmentMapPath
            },
            PbrTuning = new PbrTuningProfile
            {
                Exposure = 1.0f,
                PbrWhitePoint = 1.15f,
                IblDiffuseIntensity = 0.15f,
                IblSpecularIntensity = 0.6f,
                ReflectionContributionClamp = 0.9f,
                AmbientStrengthClamp = 0.25f,
                AmbientOcclusionStrength = 1.0f
            },
            Background = new BackgroundProfile { Red = 0.05f, Green = 0.05f, Blue = 0.06f },
            MaxLights = 4
        };

        public GraphicsProfile Validate()
        {
            var validatedShadows = (Shadows ?? new ShadowProfile()) with
            {
                MapSize = Math.Clamp(Shadows?.MapSize ?? 2048, RenderQualitySettings.MinShadowMapSize, RenderQualitySettings.MaxShadowMapSize)
            };

            var bloom = PostFx?.Bloom ?? new BloomProfile();
            var bloomEnabledBySettings = (PostFx?.Effects ?? PostEffectsFlags.None).HasFlag(PostEffectsFlags.Bloom) && bloom.Enabled;
            var validatedPostFx = (PostFx ?? new PostFxProfile()) with
            {
                Effects = bloomEnabledBySettings
                    ? (PostFx?.Effects ?? PostEffectsFlags.None) | PostEffectsFlags.Bloom
                    : (PostFx?.Effects ?? PostEffectsFlags.None) & ~PostEffectsFlags.Bloom,
                Gamma = Math.Clamp(PostFx?.Gamma ?? 2.2f, 1.0f, 3.0f),
                ToneMapping = (PostFx?.Effects ?? PostEffectsFlags.None).HasFlag(PostEffectsFlags.ToneMapping)
                    ? PostFx?.ToneMapping ?? ToneMappingOperator.Reinhard
                    : ToneMappingOperator.None,
                Bloom = bloom with
                {
                    Enabled = bloomEnabledBySettings,
                    Threshold = Math.Clamp(bloom.Threshold, 0f, 16f),
                    Intensity = Math.Clamp(bloom.Intensity, 0f, 8f),
                    Radius = Math.Clamp(bloom.Radius, 0.1f, 4f),
                    Iterations = Math.Clamp(bloom.Iterations, 1, 8),
                    SoftKnee = Math.Clamp(bloom.SoftKnee, 0f, 2f),
                    NormalizationBoost = Math.Clamp(bloom.NormalizationBoost, 0f, 4f),
                    EmissiveMinContribution = Math.Clamp(bloom.EmissiveMinContribution, 0f, 1f),
                    UnlitIntensityBoost = Math.Clamp(bloom.UnlitIntensityBoost, 0f, 8f),
                    ColorAdditiveContribution = Math.Clamp(bloom.ColorAdditiveContribution, 0f, 1f)
                }
            };

            var reflections = Reflections ?? new ReflectionProfile();
            var validatedReflections = reflections with
            {
                Intensity = Math.Clamp(reflections.Intensity, 0f, 2f),
                Mode = reflections.Enabled ? reflections.Mode : ReflectionMode.Off,
                EnvironmentMapPath = string.IsNullOrWhiteSpace(reflections.EnvironmentMapPath)
                    ? ResolveDefaultEnvironmentMapPath(reflections.Enabled, reflections.Mode)
                    : reflections.EnvironmentMapPath.Trim()
            };

            var validatedPbr = (PbrTuning ?? new PbrTuningProfile()) with
            {
                Exposure = Math.Clamp(PbrTuning?.Exposure ?? 1.0f, 0.1f, 8.0f),
                PbrWhitePoint = Math.Clamp(PbrTuning?.PbrWhitePoint ?? 1.0f, 0.5f, 16.0f),
                IblDiffuseIntensity = Math.Clamp(PbrTuning?.IblDiffuseIntensity ?? 0.2f, 0f, 4.0f),
                IblSpecularIntensity = Math.Clamp(PbrTuning?.IblSpecularIntensity ?? 1.0f, 0f, 8.0f),
                ReflectionContributionClamp = Math.Clamp(PbrTuning?.ReflectionContributionClamp ?? 1.25f, 0f, 8.0f),
                AmbientStrengthClamp = Math.Clamp(PbrTuning?.AmbientStrengthClamp ?? 0.35f, 0f, 2.0f),
                AmbientOcclusionStrength = Math.Clamp(PbrTuning?.AmbientOcclusionStrength ?? 1.0f, 0f, 4.0f)
            };

            var validatedBackground = (Background ?? new BackgroundProfile()) with
            {
                Red = Math.Clamp(Background?.Red ?? 0.06f, 0f, 1f),
                Green = Math.Clamp(Background?.Green ?? 0.06f, 0f, 1f),
                Blue = Math.Clamp(Background?.Blue ?? 0.08f, 0f, 1f)
            };

            return this with
            {
                Name = string.IsNullOrWhiteSpace(Name) ? "Custom" : Name.Trim(),
                Shadows = validatedShadows,
                PostFx = validatedPostFx,
                Reflections = validatedReflections,
                PbrTuning = validatedPbr,
                Background = validatedBackground,
                MaxLights = Math.Clamp(MaxLights, RenderQualitySettings.MinLights, RenderQualitySettings.MaxSupportedLights)
            };
        }

        public string ToJson() => JsonSerializer.Serialize(this.Validate(), JsonOptions);

        public static GraphicsProfile FromJson(string json)
        {
            var parsed = JsonSerializer.Deserialize<GraphicsProfile>(json, JsonOptions);
            return (parsed ?? Medium).Validate();
        }

        public static GraphicsProfile FromPreset(RenderQualityPreset preset, GraphicsProfile? current = null)
        {
            return preset switch
            {
                RenderQualityPreset.Low => Low,
                RenderQualityPreset.Medium => Medium,
                RenderQualityPreset.High => High,
                RenderQualityPreset.Ultra => Ultra,
                RenderQualityPreset.PbrDebugNeutral => PbrDebugNeutral,
                RenderQualityPreset.Custom => current ?? Medium,
                _ => Medium
            };
        }

        public string ToSummary() =>
            $"Profile={Name} ({QualityPreset}), shadows={Shadows.Enabled}:{Shadows.MapSize}, postfx={PostFx.Effects}, gamma={PostFx.Gamma:0.00}, bloom={PostFx.Bloom.Enabled}:{PostFx.Bloom.Threshold:0.00}/{PostFx.Bloom.Intensity:0.00}/{PostFx.Bloom.Radius:0.00}x{PostFx.Bloom.Iterations}, refl={Reflections.Mode}:{Reflections.Intensity:0.00}, exposure={PbrTuning.Exposure:0.00}, wp={PbrTuning.PbrWhitePoint:0.00}, ibl(diff/spec)={PbrTuning.IblDiffuseIntensity:0.00}/{PbrTuning.IblSpecularIntensity:0.00}, reflClamp={PbrTuning.ReflectionContributionClamp:0.00}, ambientClamp={PbrTuning.AmbientStrengthClamp:0.00}, lights={MaxLights}, msaa={MsaaPolicy}, bg=({Background.Red:0.00},{Background.Green:0.00},{Background.Blue:0.00})";

        private static string? ResolveDefaultEnvironmentMapPath(bool reflectionsEnabled, ReflectionMode mode)
        {
            if (!reflectionsEnabled || mode != ReflectionMode.IBL)
            {
                return null;
            }

            return DefaultEnvironmentMapPath;
        }
    }
}

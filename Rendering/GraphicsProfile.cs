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

    public sealed record PostFxProfile
    {
        public PostEffectsFlags Effects { get; init; } = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection;
        public ToneMappingOperator ToneMapping { get; init; } = ToneMappingOperator.Reinhard;
        public float Gamma { get; init; } = 2.2f;
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
        public float IblIntensity { get; init; } = 1.0f;
        public float AmbientOcclusionStrength { get; init; } = 1.0f;
    }

    public sealed record GraphicsProfile
    {
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
                Gamma = 2.0f
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
                IblIntensity = 0.6f,
                AmbientOcclusionStrength = 0.8f
            }
        };

        public static GraphicsProfile Medium => new()
        {
            Name = "Medium",
            QualityPreset = RenderQualityPreset.Medium,
            MsaaPolicy = MsaaPolicy.X2,
            Shadows = new ShadowProfile { Enabled = true, MapSize = 1536 },
            PostFx = new PostFxProfile
            {
                Effects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection,
                ToneMapping = ToneMappingOperator.Reinhard,
                Gamma = 2.2f
            },
            Reflections = new ReflectionProfile
            {
                Enabled = true,
                Mode = ReflectionMode.IBL,
                Intensity = 0.3f
            },
            PbrTuning = new PbrTuningProfile
            {
                Exposure = 1.0f,
                IblIntensity = 1.0f,
                AmbientOcclusionStrength = 1.0f
            }
        };

        public static GraphicsProfile High => new()
        {
            Name = "High",
            QualityPreset = RenderQualityPreset.High,
            MsaaPolicy = MsaaPolicy.X4,
            Shadows = new ShadowProfile { Enabled = true, MapSize = 4096 },
            PostFx = new PostFxProfile
            {
                Effects = PostEffectsFlags.ToneMapping | PostEffectsFlags.GammaCorrection,
                ToneMapping = ToneMappingOperator.Reinhard,
                Gamma = 2.2f
            },
            Reflections = new ReflectionProfile
            {
                Enabled = true,
                Mode = ReflectionMode.IBL,
                Intensity = 0.45f
            },
            PbrTuning = new PbrTuningProfile
            {
                Exposure = 1.1f,
                IblIntensity = 1.2f,
                AmbientOcclusionStrength = 1.1f
            }
        };

        public GraphicsProfile Validate()
        {
            var validatedShadows = (Shadows ?? new ShadowProfile()) with
            {
                MapSize = Math.Clamp(Shadows?.MapSize ?? 2048, RenderQualitySettings.MinShadowMapSize, RenderQualitySettings.MaxShadowMapSize)
            };

            var validatedPostFx = (PostFx ?? new PostFxProfile()) with
            {
                Gamma = Math.Clamp(PostFx?.Gamma ?? 2.2f, 1.0f, 3.0f),
                ToneMapping = (PostFx?.Effects ?? PostEffectsFlags.None).HasFlag(PostEffectsFlags.ToneMapping)
                    ? PostFx?.ToneMapping ?? ToneMappingOperator.Reinhard
                    : ToneMappingOperator.None
            };

            var reflections = Reflections ?? new ReflectionProfile();
            var validatedReflections = reflections with
            {
                Intensity = Math.Clamp(reflections.Intensity, 0f, 2f),
                Mode = reflections.Enabled ? reflections.Mode : ReflectionMode.Off,
                EnvironmentMapPath = string.IsNullOrWhiteSpace(reflections.EnvironmentMapPath)
                    ? null
                    : reflections.EnvironmentMapPath.Trim()
            };

            var validatedPbr = (PbrTuning ?? new PbrTuningProfile()) with
            {
                Exposure = Math.Clamp(PbrTuning?.Exposure ?? 1.0f, 0.1f, 8.0f),
                IblIntensity = Math.Clamp(PbrTuning?.IblIntensity ?? 1.0f, 0f, 8.0f),
                AmbientOcclusionStrength = Math.Clamp(PbrTuning?.AmbientOcclusionStrength ?? 1.0f, 0f, 4.0f)
            };

            return this with
            {
                Name = string.IsNullOrWhiteSpace(Name) ? "Custom" : Name.Trim(),
                Shadows = validatedShadows,
                PostFx = validatedPostFx,
                Reflections = validatedReflections,
                PbrTuning = validatedPbr
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
                RenderQualityPreset.Custom => current ?? Medium,
                _ => Medium
            };
        }

        public string ToSummary() =>
            $"Profile={Name} ({QualityPreset}), shadows={Shadows.Enabled}:{Shadows.MapSize}, postfx={PostFx.Effects}, gamma={PostFx.Gamma:0.00}, refl={Reflections.Mode}:{Reflections.Intensity:0.00}, exposure={PbrTuning.Exposure:0.00}, ibl={PbrTuning.IblIntensity:0.00}, msaa={MsaaPolicy}";
    }
}

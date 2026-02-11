using Avalonia3D.Rendering;

namespace Avalonia3D.Model
{
    public sealed record EnvironmentLightingSettings
    {
        public bool ReflectionsEnabled { get; init; } = true;
        public ReflectionMode ReflectionMode { get; init; } = ReflectionMode.IBL;
        public float ReflectionIntensity { get; init; } = 0.35f;
        public float IblDiffuseIntensity { get; init; } = 0.2f;
        public float IblSpecularIntensity { get; init; } = 1.0f;
        public float ReflectionContributionClamp { get; init; } = 1.25f;
        public float AmbientStrengthClamp { get; init; } = 0.35f;
        public string? EnvironmentMapPath { get; init; }

        public static EnvironmentLightingSettings FromRenderQuality(RenderQualitySettings settings)
        {
            return FromGraphicsProfile(settings.ToProfile());
        }

        public static EnvironmentLightingSettings FromGraphicsProfile(GraphicsProfile profile)
        {
            var validated = (profile ?? GraphicsProfile.Medium).Validate();
            return new EnvironmentLightingSettings
            {
                ReflectionsEnabled = validated.Reflections.Enabled,
                ReflectionMode = validated.Reflections.Mode,
                ReflectionIntensity = validated.Reflections.Intensity,
                IblDiffuseIntensity = validated.PbrTuning.IblDiffuseIntensity,
                IblSpecularIntensity = validated.PbrTuning.IblSpecularIntensity,
                ReflectionContributionClamp = validated.PbrTuning.ReflectionContributionClamp,
                AmbientStrengthClamp = validated.PbrTuning.AmbientStrengthClamp,
                EnvironmentMapPath = validated.Reflections.EnvironmentMapPath
            };
        }
    }
}

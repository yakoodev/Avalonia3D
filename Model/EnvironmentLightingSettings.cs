using Avalonia3D.Rendering;

namespace Avalonia3D.Model
{
    public sealed record EnvironmentLightingSettings
    {
        public bool ReflectionsEnabled { get; init; } = true;
        public ReflectionMode ReflectionMode { get; init; } = ReflectionMode.IBL;
        public float ReflectionIntensity { get; init; } = 0.35f;
        public string? EnvironmentMapPath { get; init; }

        public static EnvironmentLightingSettings FromRenderQuality(RenderQualitySettings settings)
        {
            var validated = settings.Validate();
            return new EnvironmentLightingSettings
            {
                ReflectionsEnabled = validated.ReflectionsEnabled,
                ReflectionMode = validated.ReflectionMode,
                ReflectionIntensity = validated.ReflectionIntensity,
                EnvironmentMapPath = validated.EnvironmentMapPath
            };
        }
    }
}

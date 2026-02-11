using System.Numerics;

namespace Avalonia3D.Rendering
{
    public sealed class RenderFrameState
    {
        public FrameMetrics Metrics { get; } = new();
        public uint OutputFramebufferId { get; set; }
        public uint ForwardFramebufferId { get; set; }
        public uint ForwardColorTextureId { get; set; }
        public uint EmissiveFramebufferId { get; set; }
        public uint EmissiveTextureId { get; set; }
        public uint? ShadowMapId { get; set; }
        public Matrix4x4 LightSpaceMatrix { get; set; } = Matrix4x4.Identity;
        public uint? EnvironmentReflectionTextureId { get; set; }
        public float ReflectionIntensity { get; set; }
        public float IblDiffuseIntensity { get; set; } = 0.2f;
        public float IblSpecularIntensity { get; set; } = 1.0f;
        public float ReflectionContributionClamp { get; set; } = 1.25f;
        public float AmbientStrengthClamp { get; set; } = 0.35f;
        public float DirectLightContributionClamp { get; set; } = 1.35f;
        public float SeparateEmissiveSurfaceScale { get; set; } = 0.28f;
        public bool ReflectionsEnabled { get; set; }
        public ReflectionMode ReflectionMode { get; set; } = ReflectionMode.Off;
        public PbrDebugViewMode PbrDebugViewMode { get; set; } = PbrDebugViewMode.None;

        public bool HasEmissiveTarget => EmissiveFramebufferId != 0 && EmissiveTextureId != 0;

        public void ResetForwardTargets()
        {
            ForwardFramebufferId = 0;
            ForwardColorTextureId = 0;
            EmissiveFramebufferId = 0;
            EmissiveTextureId = 0;
        }
    }

    public sealed class FrameMetrics
    {
        public int DrawCalls { get; set; }
        public int CulledObjects { get; set; }

        public void Reset()
        {
            DrawCalls = 0;
            CulledObjects = 0;
        }
    }
}

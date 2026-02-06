using System.Numerics;

namespace Avalonia3D.Rendering
{
    public sealed class RenderFrameState
    {
        public uint OutputFramebufferId { get; set; }
        public uint? ShadowMapId { get; set; }
        public Matrix4x4 LightSpaceMatrix { get; set; } = Matrix4x4.Identity;
        public uint? EnvironmentReflectionTextureId { get; set; }
        public float ReflectionIntensity { get; set; }
        public bool ReflectionsEnabled { get; set; }
        public ReflectionMode ReflectionMode { get; set; } = ReflectionMode.Off;
    }
}

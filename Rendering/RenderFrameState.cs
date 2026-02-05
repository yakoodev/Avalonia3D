using System.Numerics;

namespace Avalonia3D.Rendering
{
    public sealed class RenderFrameState
    {
        public uint? ShadowMapId { get; set; }
        public Matrix4x4 LightSpaceMatrix { get; set; } = Matrix4x4.Identity;
    }
}

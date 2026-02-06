using Avalonia3D.Interfaces;
using Avalonia3D.Shaders;
using System.Numerics;

namespace Avalonia3D.Model
{
    public class Material
    {
        public Vector4 BaseColorFactor { get; set; } = new(1f, 1f, 1f, 1f);
        public float MetallicFactor { get; set; } = 0.0f;
        public float RoughnessFactor { get; set; } = 1.0f;
        public float OcclusionStrength { get; set; } = 1.0f;
        public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;
        public float Opacity { get; set; } = 1.0f;
        public bool IsTransparent { get; set; }

        public TextureData? BaseColorTexture { get; set; }
        public TextureData? NormalTexture { get; set; }
        public TextureData? MetallicRoughnessTexture { get; set; }
        public TextureData? OcclusionTexture { get; set; }
        public TextureData? EmissiveTexture { get; set; }

        public IShader? Shader { get; set; }
        public string? ShaderId { get; set; }

        public PbrFeatures Features { get; set; } = PbrFeatures.None;
    }
}

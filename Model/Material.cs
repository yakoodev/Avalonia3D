using Avalonia3D.Interfaces;
using Avalonia3D.Shaders;
using System.Numerics;

namespace Avalonia3D.Model
{
    public enum MaterialAlphaMode
    {
        Opaque = 0,
        Mask = 1,
        Blend = 2
    }

    public sealed class MaterialSurfaceSettings
    {
        public MaterialAlphaMode AlphaMode { get; set; } = MaterialAlphaMode.Opaque;
        public float AlphaCutoff { get; set; } = 0.5f;
        public bool DoubleSided { get; set; }
        public float EmissiveIntensity { get; set; } = 1f;
        public bool HasTextureTransparency { get; set; }
        public MaterialTransmissionSettings Transmission { get; set; } = new();

        public bool HasTransmission
        {
            get => Transmission.Enabled;
            set => Transmission.Enabled = value;
        }

        public float TransmissionFactor
        {
            get => Transmission.Factor;
            set => Transmission.Factor = value;
        }
    }

    public sealed class MaterialTransmissionSettings
    {
        public bool Enabled { get; set; }
        public float Factor { get; set; }
        public float Thickness { get; set; }
        public float Ior { get; set; } = 1.5f;
        public float AttenuationDistance { get; set; } = float.PositiveInfinity;
        public Vector3 AttenuationColor { get; set; } = Vector3.One;
    }

    public class Material
    {
        public Vector4 BaseColorFactor { get; set; } = new(1f, 1f, 1f, 1f);
        public float MetallicFactor { get; set; } = 0.0f;
        public float RoughnessFactor { get; set; } = 1.0f;
        public float OcclusionStrength { get; set; } = 1.0f;
        public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;
        public float Opacity { get; set; } = 1.0f;
        public bool IsTransparent { get; set; }
        public MaterialSurfaceSettings Surface { get; set; } = new();

        public MaterialAlphaMode AlphaMode
        {
            get => Surface.AlphaMode;
            set => Surface.AlphaMode = value;
        }

        public float AlphaCutoff
        {
            get => Surface.AlphaCutoff;
            set => Surface.AlphaCutoff = value;
        }

        public bool DoubleSided
        {
            get => Surface.DoubleSided;
            set => Surface.DoubleSided = value;
        }

        public float EmissiveIntensity
        {
            get => Surface.EmissiveIntensity;
            set => Surface.EmissiveIntensity = value;
        }

        public bool HasTextureTransparency
        {
            get => Surface.HasTextureTransparency;
            set => Surface.HasTextureTransparency = value;
        }

        public bool HasTransmission
        {
            get => Surface.HasTransmission;
            set => Surface.HasTransmission = value;
        }

        public float TransmissionFactor
        {
            get => Surface.TransmissionFactor;
            set => Surface.TransmissionFactor = value;
        }

        public float TransmissionThickness
        {
            get => Surface.Transmission.Thickness;
            set => Surface.Transmission.Thickness = value;
        }

        public float TransmissionIor
        {
            get => Surface.Transmission.Ior;
            set => Surface.Transmission.Ior = value;
        }

        public float TransmissionAttenuationDistance
        {
            get => Surface.Transmission.AttenuationDistance;
            set => Surface.Transmission.AttenuationDistance = value;
        }

        public Vector3 TransmissionAttenuationColor
        {
            get => Surface.Transmission.AttenuationColor;
            set => Surface.Transmission.AttenuationColor = value;
        }

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

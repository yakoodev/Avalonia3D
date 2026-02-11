using Avalonia3D.Interfaces;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;
using System;
using System.Collections.Generic;
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
        public float EmissiveStrength { get; set; } = 1f;
        public bool HasTextureTransparency { get; set; }
    }

    public sealed class MaterialSurfaceAdvancedSettings
    {
        public MaterialTransmissionSettings Transmission { get; set; } = new();
        public MaterialClearcoatSettings Clearcoat { get; set; } = new();
        public MaterialSheenSettings Sheen { get; set; } = new();
        public MaterialSpecularSettings Specular { get; set; } = new();
        public MaterialIorSettings Ior { get; set; } = new();

        public bool HasTransmission
        {
            get => Transmission.Enabled;
            set => Transmission.Enabled = value;
        }

        public bool HasClearcoat => Clearcoat.Factor > 0.001f;
        public bool HasSheen => Sheen.ColorFactor.LengthSquared() > 0.0001f || Sheen.RoughnessFactor > 0.001f;
        public bool HasSpecular => Specular.Factor > 0.001f;
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

    public sealed class MaterialClearcoatSettings
    {
        public float Factor { get; set; }
        public float Roughness { get; set; }
    }

    public sealed class MaterialSheenSettings
    {
        public Vector3 ColorFactor { get; set; } = Vector3.Zero;
        public float RoughnessFactor { get; set; }
    }

    public sealed class MaterialSpecularSettings
    {
        public float Factor { get; set; } = 1f;
        public Vector3 ColorFactor { get; set; } = Vector3.One;
    }

    public sealed class MaterialIorSettings
    {
        public float Value { get; set; } = 1.5f;
    }

    public sealed class MaterialTextureTransformRuntime
    {
        public Vector2 UvOffset { get; set; } = Vector2.Zero;
        public Vector2 UvScale { get; set; } = Vector2.One;
        public float UvRotation { get; set; }

        public Vector2 Apply(Vector2 uv)
        {
            var scaled = new Vector2(uv.X * UvScale.X, uv.Y * UvScale.Y);
            if (MathF.Abs(UvRotation) < 0.000001f)
            {
                return scaled + UvOffset;
            }

            var sin = MathF.Sin(UvRotation);
            var cos = MathF.Cos(UvRotation);
            var rotated = new Vector2(
                scaled.X * cos - scaled.Y * sin,
                scaled.X * sin + scaled.Y * cos);

            return rotated + UvOffset;
        }
    }

    public sealed class MaterialTextureRuntimeParameters
    {
        private readonly Dictionary<TextureSemantic, MaterialTextureTransformRuntime> _transforms = new();

        public MaterialTextureTransformRuntime BaseColor => GetOrCreate(TextureSemantic.BaseColor);
        public MaterialTextureTransformRuntime Emissive => GetOrCreate(TextureSemantic.Emissive);
        public MaterialTextureTransformRuntime Normal => GetOrCreate(TextureSemantic.Normal);
        public MaterialTextureTransformRuntime MetallicRoughness => GetOrCreate(TextureSemantic.MetallicRoughness);
        public MaterialTextureTransformRuntime Occlusion => GetOrCreate(TextureSemantic.Occlusion);

        public MaterialTextureTransformRuntime GetOrCreate(TextureSemantic semantic)
        {
            if (_transforms.TryGetValue(semantic, out var value))
            {
                return value;
            }

            value = new MaterialTextureTransformRuntime();
            _transforms[semantic] = value;
            return value;
        }
    }

    public class Material
    {
        public sealed class MaterialExtensionTextures
        {
            public TextureData? ClearcoatTexture { get; set; }
            public TextureData? ClearcoatRoughnessTexture { get; set; }
            public TextureData? ClearcoatNormalTexture { get; set; }
            public TextureData? SheenColorTexture { get; set; }
            public TextureData? SheenRoughnessTexture { get; set; }
            public TextureData? SpecularTexture { get; set; }
            public TextureData? SpecularColorTexture { get; set; }
            public TextureData? TransmissionTexture { get; set; }
            public TextureData? VolumeThicknessTexture { get; set; }
        }

        public Vector4 BaseColorFactor { get; set; } = new(1f, 1f, 1f, 1f);
        public float MetallicFactor { get; set; } = 0.0f;
        public float RoughnessFactor { get; set; } = 1.0f;
        public float OcclusionStrength { get; set; } = 1.0f;
        public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;
        public float Opacity { get; set; } = 1.0f;
        public bool IsTransparent { get; set; }
        public MaterialSurfaceSettings Surface { get; set; } = new();
        public MaterialSurfaceAdvancedSettings SurfaceAdvanced { get; set; } = new();

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

        public float EmissiveStrength
        {
            get => Surface.EmissiveStrength;
            set => Surface.EmissiveStrength = value;
        }

        public bool HasTextureTransparency
        {
            get => Surface.HasTextureTransparency;
            set => Surface.HasTextureTransparency = value;
        }

        public bool HasTransmission
        {
            get => SurfaceAdvanced.HasTransmission;
            set => SurfaceAdvanced.HasTransmission = value;
        }

        public float TransmissionFactor
        {
            get => SurfaceAdvanced.Transmission.Factor;
            set => SurfaceAdvanced.Transmission.Factor = value;
        }

        public float TransmissionThickness
        {
            get => SurfaceAdvanced.Transmission.Thickness;
            set => SurfaceAdvanced.Transmission.Thickness = value;
        }

        public float TransmissionIor
        {
            get => SurfaceAdvanced.Transmission.Ior;
            set => SurfaceAdvanced.Transmission.Ior = value;
        }

        public float TransmissionAttenuationDistance
        {
            get => SurfaceAdvanced.Transmission.AttenuationDistance;
            set => SurfaceAdvanced.Transmission.AttenuationDistance = value;
        }

        public Vector3 TransmissionAttenuationColor
        {
            get => SurfaceAdvanced.Transmission.AttenuationColor;
            set => SurfaceAdvanced.Transmission.AttenuationColor = value;
        }

        public float ClearcoatFactor
        {
            get => SurfaceAdvanced.Clearcoat.Factor;
            set => SurfaceAdvanced.Clearcoat.Factor = value;
        }

        public float ClearcoatRoughness
        {
            get => SurfaceAdvanced.Clearcoat.Roughness;
            set => SurfaceAdvanced.Clearcoat.Roughness = value;
        }

        public Vector3 SheenColorFactor
        {
            get => SurfaceAdvanced.Sheen.ColorFactor;
            set => SurfaceAdvanced.Sheen.ColorFactor = value;
        }

        public float SheenRoughnessFactor
        {
            get => SurfaceAdvanced.Sheen.RoughnessFactor;
            set => SurfaceAdvanced.Sheen.RoughnessFactor = value;
        }

        public float SpecularFactor
        {
            get => SurfaceAdvanced.Specular.Factor;
            set => SurfaceAdvanced.Specular.Factor = value;
        }

        public Vector3 SpecularColorFactor
        {
            get => SurfaceAdvanced.Specular.ColorFactor;
            set => SurfaceAdvanced.Specular.ColorFactor = value;
        }

        public float Ior
        {
            get => SurfaceAdvanced.Ior.Value;
            set => SurfaceAdvanced.Ior.Value = value;
        }

        public TextureData? BaseColorTexture { get; set; }
        public TextureData? NormalTexture { get; set; }
        public TextureData? MetallicRoughnessTexture { get; set; }
        public TextureData? OcclusionTexture { get; set; }
        public TextureData? EmissiveTexture { get; set; }
        public MaterialTextureRuntimeParameters TextureRuntime { get; set; } = new();
        public MaterialExtensionTextures ExtensionTextures { get; set; } = new();

        public IShader? Shader { get; set; }
        public string? ShaderId { get; set; }

        public PbrFeatures Features { get; set; } = PbrFeatures.None;
    }
}

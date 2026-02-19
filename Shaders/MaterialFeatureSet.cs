using System;

namespace Avalonia3D.Shaders;

[Flags]
public enum MaterialFeatureSet
{
    None = 0,
    BaseColorMap = 1 << 0,
    NormalMap = 1 << 1,
    MetallicRoughnessMap = 1 << 2,
    OcclusionMap = 1 << 3,
    EmissiveMap = 1 << 4,
    ReflectionsIbl = 1 << 5,
    Transmission = 1 << 6,
    Clearcoat = 1 << 7,
    Sheen = 1 << 8,
    Specular = 1 << 9,
    Ior = 1 << 10,
    EmissiveStrength = 1 << 11,
    ClearcoatMap = 1 << 12,
    ClearcoatRoughnessMap = 1 << 13,
    ClearcoatNormalMap = 1 << 14,
    SheenColorMap = 1 << 15,
    SheenRoughnessMap = 1 << 16,
    SpecularMap = 1 << 17,
    SpecularColorMap = 1 << 18,
    TransmissionMap = 1 << 19,
    VolumeThicknessMap = 1 << 20,
    Anisotropy = 1 << 21,
    Iridescence = 1 << 22
}

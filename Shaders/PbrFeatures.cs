using System;

namespace Avalonia3D.Shaders;

[Flags]
public enum PbrFeatures
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
    EmissiveStrength = 1 << 11
}

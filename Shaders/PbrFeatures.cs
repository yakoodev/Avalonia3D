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
    Transmission = 1 << 6
}


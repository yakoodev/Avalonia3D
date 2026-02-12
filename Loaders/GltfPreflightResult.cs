using System;
using System.Collections.Generic;

namespace Avalonia3D.Loaders;

public enum GltfContainerKind
{
    Unknown = 0,
    GltfJson = 1,
    GlbBinary = 2
}

public sealed record GltfPreflightResult(
    GltfContainerKind ContainerKind,
    IReadOnlyList<string> ExternalUris,
    IReadOnlyList<string> Warnings,
    bool ExternalDependencyScanSupported)
{
    public static GltfPreflightResult Empty { get; } = new(
        GltfContainerKind.Unknown,
        Array.Empty<string>(),
        Array.Empty<string>(),
        ExternalDependencyScanSupported: false);
}


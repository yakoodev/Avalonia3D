namespace Avalonia3D.Shaders;

/// <summary>
/// Runtime debug policy for emissive texture contribution.
/// Keeps diagnostics centralized and extensible for future emissive troubleshooting modes.
/// </summary>
public enum EmissiveTextureDebugMode
{
    /// <summary>
    /// Default behavior: use emissive texture sampling as defined by the material.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Ignore emissive texture contribution (as if emissive map is absent).
    /// Useful to verify whether animation/factor pipeline works independently from UV/texture data.
    /// </summary>
    IgnoreTexture = 1,

    /// <summary>
    /// Force emissive texture multiplier to white (1,1,1) while keeping emissive-factor/intensity pipeline active.
    /// Useful to isolate texture content/UV issues from signal routing.
    /// </summary>
    ForceWhite = 2
}

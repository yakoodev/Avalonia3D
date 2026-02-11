namespace Avalonia3D.Rendering;

/// <summary>
/// Centralized PBR debug visualization modes.
/// Used by UI, runtime uniforms and diagnostics from a single source of truth.
/// </summary>
public enum PbrDebugViewMode
{
    None = 0,
    BaseColorOnly = 1,
    DirectLightOnly = 2,
    IblOnly = 3,
    EmissiveOnly = 4,
    AoOnly = 5,
    NormalsOnly = 6,
    BaseColorTexRaw = 7,
    BaseColorAfterSrgbDecode = 8,
    BaseColorAfterFactor = 9
}

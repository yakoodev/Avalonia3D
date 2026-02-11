using Silk.NET.OpenGL;
using Avalonia3D.Rendering;

namespace Avalonia3D.Rendering.Diagnostics;

public enum ColorDecodeMode
{
    NotRequired = 0,
    HardwareSrgb = 1,
    ManualShaderFallback = 2,
    MissingSrgbDecode = 3
}

public static class PbrColorPipelineDiagnostics
{
    public static ColorDecodeMode ResolveDecodeMode(TextureSemantic semantic, InternalFormat preferred, InternalFormat used, bool manualCompensationEnabled)
    {
        if (!TextureSemanticColorPolicy.RequiresSrgbDecode(semantic))
        {
            return ColorDecodeMode.NotRequired;
        }

        if (preferred == used && preferred == InternalFormat.SrgbAlpha)
        {
            return ColorDecodeMode.HardwareSrgb;
        }

        if (used == InternalFormat.Rgba && manualCompensationEnabled)
        {
            return ColorDecodeMode.ManualShaderFallback;
        }

        return ColorDecodeMode.MissingSrgbDecode;
    }
}

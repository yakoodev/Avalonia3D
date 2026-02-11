using Silk.NET.OpenGL;

namespace Avalonia3D.Rendering;

public enum TextureColorSpace
{
    Linear,
    Srgb
}

public static class TextureSemanticColorPolicy
{
    public static TextureColorSpace ResolveColorSpace(TextureSemantic semantic)
    {
        return semantic switch
        {
            TextureSemantic.BaseColor => TextureColorSpace.Srgb,
            TextureSemantic.Emissive => TextureColorSpace.Srgb,
            _ => TextureColorSpace.Linear
        };
    }

    public static bool RequiresSrgbDecode(TextureSemantic semantic)
        => ResolveColorSpace(semantic) == TextureColorSpace.Srgb;

    public static InternalFormat ResolvePreferredInternalFormat(TextureSemantic semantic)
        => RequiresSrgbDecode(semantic) ? InternalFormat.SrgbAlpha : InternalFormat.Rgba;

    public static InternalFormat ResolveFallbackInternalFormat(TextureSemantic semantic)
        => InternalFormat.Rgba;
}

using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class TextureSemanticColorPolicyTests
{
    [Theory]
    [InlineData(TextureSemantic.BaseColor, TextureColorSpace.Srgb)]
    [InlineData(TextureSemantic.Emissive, TextureColorSpace.Srgb)]
    [InlineData(TextureSemantic.Normal, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.MetallicRoughness, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.Occlusion, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.Clearcoat, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.ClearcoatRoughness, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.ClearcoatNormal, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.SheenColor, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.SheenRoughness, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.Specular, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.SpecularColor, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.Transmission, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.VolumeThickness, TextureColorSpace.Linear)]
    [InlineData(TextureSemantic.Extension, TextureColorSpace.Linear)]
    public void ResolveColorSpace_ReturnsExpectedColorSpace(TextureSemantic semantic, TextureColorSpace expected)
    {
        var actual = TextureSemanticColorPolicy.ResolveColorSpace(semantic);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(TextureSemantic.BaseColor, InternalFormat.SrgbAlpha)]
    [InlineData(TextureSemantic.Emissive, InternalFormat.SrgbAlpha)]
    [InlineData(TextureSemantic.Normal, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.SheenColor, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.SpecularColor, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Extension, InternalFormat.Rgba)]
    public void ResolvePreferredInternalFormat_ReturnsExpectedFormat(TextureSemantic semantic, InternalFormat expected)
    {
        var actual = TextureSemanticColorPolicy.ResolvePreferredInternalFormat(semantic);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(TextureSemantic.BaseColor)]
    [InlineData(TextureSemantic.Emissive)]
    [InlineData(TextureSemantic.Normal)]
    [InlineData(TextureSemantic.Extension)]
    public void ResolveFallbackInternalFormat_AlwaysReturnsRgba(TextureSemantic semantic)
    {
        var actual = TextureSemanticColorPolicy.ResolveFallbackInternalFormat(semantic);

        Assert.Equal(InternalFormat.Rgba, actual);
    }
}

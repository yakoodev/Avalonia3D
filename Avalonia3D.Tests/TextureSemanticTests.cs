using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using Xunit;

namespace Avalonia3D.Tests;

public class TextureSemanticTests
{
    [Theory]
    [InlineData(TextureSemantic.BaseColor, InternalFormat.SrgbAlpha)]
    [InlineData(TextureSemantic.Emissive, InternalFormat.SrgbAlpha)]
    [InlineData(TextureSemantic.Normal, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.MetallicRoughness, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Occlusion, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Clearcoat, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.ClearcoatRoughness, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.ClearcoatNormal, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.SheenColor, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.SheenRoughness, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Specular, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.SpecularColor, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Transmission, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.VolumeThickness, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Extension, InternalFormat.Rgba)]
    public void ResolveInternalFormat_ReturnsExpectedFormat(TextureSemantic semantic, InternalFormat expected)
    {
        var actual = RenderResourceManager.ResolveInternalFormat(semantic);

        Assert.Equal(expected, actual);
    }


    [Theory]
    [InlineData(TextureSemantic.BaseColor, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Emissive, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.Normal, InternalFormat.Rgba)]
    [InlineData(TextureSemantic.MetallicRoughness, InternalFormat.Rgba)]
    public void ResolveFallbackInternalFormat_ReturnsExpectedFormat(TextureSemantic semantic, InternalFormat expected)
    {
        var actual = RenderResourceManager.ResolveFallbackInternalFormat(semantic);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(TextureSemantic.BaseColor, true)]
    [InlineData(TextureSemantic.Emissive, true)]
    [InlineData(TextureSemantic.Normal, false)]
    [InlineData(TextureSemantic.MetallicRoughness, false)]
    [InlineData(TextureSemantic.Occlusion, false)]
    [InlineData(TextureSemantic.Extension, false)]
    public void IsSrgbSemantic_ReturnsExpectedValue(TextureSemantic semantic, bool expected)
    {
        var actual = RenderResourceManager.IsSrgbSemantic(semantic);

        Assert.Equal(expected, actual);
    }
}

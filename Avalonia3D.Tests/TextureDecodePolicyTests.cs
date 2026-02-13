using Avalonia3D.Loaders;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class TextureDecodePolicyTests
{
    [Fact]
    public void TextureDecodePolicy_Fast_UsesTriangle()
    {
        var policy = new TextureDecodePolicy(TextureDecodeMode.Fast);
        Assert.Same(KnownResamplers.Triangle, policy.GetResamplerFor(TextureDecodeMode.Fast));
    }

    [Fact]
    public void TextureDecodePolicy_Balanced_UsesBicubic()
    {
        var policy = new TextureDecodePolicy(TextureDecodeMode.Balanced);
        Assert.Same(KnownResamplers.Bicubic, policy.GetResamplerFor(TextureDecodeMode.Balanced));
    }

    [Fact]
    public void TextureDecodePolicy_Quality_UsesLanczos3()
    {
        var policy = new TextureDecodePolicy(TextureDecodeMode.Quality);
        Assert.Same(KnownResamplers.Lanczos3, policy.GetResamplerFor(TextureDecodeMode.Quality));
    }
}

using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class TextureColorManagementTests
{
    [Fact]
    public void ShouldFlagMissingSrgbDecode_WhenSrgbPreferredButRgbaUsed_ReturnsTrue()
    {
        var result = TextureColorManagement.ShouldFlagMissingSrgbDecode(
            TextureSemantic.BaseColor,
            InternalFormat.SrgbAlpha,
            InternalFormat.Rgba);

        Assert.True(result);
    }

    [Fact]
    public void HasMissingSrgbDecode_ReturnsTrueOnlyForMatchingSemanticFlag()
    {
        var flags = TextureColorFlags.BaseColorMissingSrgbDecode;

        Assert.True(TextureColorManagement.HasMissingSrgbDecode(flags, TextureSemantic.BaseColor));
        Assert.False(TextureColorManagement.HasMissingSrgbDecode(flags, TextureSemantic.Emissive));
    }
}

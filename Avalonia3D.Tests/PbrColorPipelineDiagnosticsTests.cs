using Avalonia3D.Rendering;
using Avalonia3D.Rendering.Diagnostics;
using Silk.NET.OpenGL;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "Rendering")]
public class PbrColorPipelineDiagnosticsTests
{
    [Fact]
    public void ResolveDecodeMode_ForBaseColorFallback_ReturnsManualShaderFallback()
    {
        var mode = PbrColorPipelineDiagnostics.ResolveDecodeMode(
            TextureSemantic.BaseColor,
            InternalFormat.SrgbAlpha,
            InternalFormat.Rgba,
            manualCompensationEnabled: true);

        Assert.Equal(ColorDecodeMode.ManualShaderFallback, mode);
    }

    [Fact]
    public void ResolveDecodeMode_ForLinearSemantic_ReturnsNotRequired()
    {
        var mode = PbrColorPipelineDiagnostics.ResolveDecodeMode(
            TextureSemantic.Normal,
            InternalFormat.Rgba,
            InternalFormat.Rgba,
            manualCompensationEnabled: true);

        Assert.Equal(ColorDecodeMode.NotRequired, mode);
    }
}

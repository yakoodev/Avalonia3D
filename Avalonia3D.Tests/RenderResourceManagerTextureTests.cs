using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "Rendering")]
public sealed class RenderResourceManagerTextureTests
{
    [Fact]
    public void SetupTextureForTests_WhenUploadPipelineLeavesGlError_DeletesTextureAndReturnsZero()
    {
        var gl = new FakeTextureGlAdapter
        {
            GeneratedTextureId = 42,
            ErrorScript = new[]
            {
                GLEnum.NoError, // clear stale errors before upload
                GLEnum.NoError, // upload succeeded
                GLEnum.InvalidOperation // after texture parameters/mipmap
            }
        };

        var manager = new RenderResourceManager(gl);
        var resources = new RenderResources();
        var texture = new TextureData
        {
            Width = 2,
            Height = 2,
            Data = new byte[2 * 2 * 4]
        };

        var textureId = manager.SetupTextureForTests(texture, TextureSemantic.BaseColor, resources, "TestModel", "TestMaterial");

        Assert.Equal(0u, textureId);
        Assert.Contains(42u, gl.DeletedTextures);
        Assert.Equal(1, gl.TexImage2DCalls);
    }

    private sealed class FakeTextureGlAdapter : RenderResourceManager.ITextureGlAdapter
    {
        private int _errorIndex;

        public uint GeneratedTextureId { get; set; }
        public GLEnum[] ErrorScript { get; set; } = [GLEnum.NoError];
        public int TexImage2DCalls { get; private set; }
        public List<uint> DeletedTextures { get; } = new();

        public uint GenTexture() => GeneratedTextureId;

        public void BindTexture(TextureTarget target, uint textureId)
        {
        }

        public GLEnum GetError()
        {
            if (_errorIndex >= ErrorScript.Length)
            {
                return GLEnum.NoError;
            }

            return ErrorScript[_errorIndex++];
        }

        public unsafe void TexImage2D(TextureTarget target, int level, int internalFormat, uint width, uint height, int border, PixelFormat format, PixelType type, void* data)
        {
            TexImage2DCalls++;
        }

        public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
        {
        }

        public void GenerateMipmap(TextureTarget target)
        {
        }

        public void DeleteTexture(uint textureId)
        {
            DeletedTextures.Add(textureId);
        }
    }
}

using Silk.NET.OpenGL;

namespace Avalonia3D.Rendering
{
    internal static class GlCompatibility
    {
        public static unsafe bool TryAllocateRgbaTexture2D(GL gl, int width, int height)
        {
            DrainErrors(gl);

            gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                null);

            if (gl.GetError() == GLEnum.NoError)
            {
                return true;
            }

            gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                null);

            return gl.GetError() == GLEnum.NoError;
        }

        public static void DrainErrors(GL gl)
        {
            for (var i = 0; i < 16; i++)
            {
                if (gl.GetError() == GLEnum.NoError)
                {
                    break;
                }
            }
        }
    }
}

using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering
{
    [Flags]
    public enum TextureColorFlags
    {
        None = 0,
        BaseColorMissingSrgbDecode = 1 << 0,
        EmissiveMissingSrgbDecode = 1 << 1
    }

    public static class TextureColorManagement
    {
        public static bool EnableManualSrgbDecodeCompensation { get; set; } = true;

        public static bool ShouldFlagMissingSrgbDecode(TextureSemantic semantic, InternalFormat preferred, InternalFormat used)
        {
            if (!EnableManualSrgbDecodeCompensation)
            {
                return false;
            }

            if (!RenderResourceManager.IsSrgbSemantic(semantic))
            {
                return false;
            }

            return preferred == InternalFormat.SrgbAlpha && used == InternalFormat.Rgba;
        }

        public static TextureColorFlags GetMissingSrgbDecodeFlag(TextureSemantic semantic)
        {
            return semantic switch
            {
                TextureSemantic.BaseColor => TextureColorFlags.BaseColorMissingSrgbDecode,
                TextureSemantic.Emissive => TextureColorFlags.EmissiveMissingSrgbDecode,
                _ => TextureColorFlags.None
            };
        }

        public static bool HasMissingSrgbDecode(TextureColorFlags flags, TextureSemantic semantic)
        {
            var semanticFlag = GetMissingSrgbDecodeFlag(semantic);
            return semanticFlag != TextureColorFlags.None && (flags & semanticFlag) != 0;
        }
    }
}

using System;
using System.Numerics;

namespace Avalonia3D.Model
{
    public class TextureData
    {
        public sealed class TextureTransformData
        {
            public Vector2 Offset { get; set; } = Vector2.Zero;
            public Vector2 Scale { get; set; } = Vector2.One;
            public float Rotation { get; set; }
            public int TexCoord { get; set; }
        }

        public int Width;
        public int Height;
        public byte[] Data; // RGBA8
        public bool DataIsPooled; // true если Data арендована из ArrayPool
        public TextureTransformData Transform { get; } = new();
    }
}

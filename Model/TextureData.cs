using System;

namespace Avalonia3D.Model
{
    public class TextureData
    {
        public int Width;
        public int Height;
        public byte[] Data; // RGBA8
        public bool DataIsPooled; // true если Data арендована из ArrayPool
    }
}
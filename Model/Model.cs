using System;
using System.Numerics;

namespace Avalonia3D.Model
{
    public class Model
    {
        public string Name { get; set; } = string.Empty;
        public string PrimitiveKey { get; set; } = string.Empty;
        public Vertex[] Vertices { get; set; } = [];

        public uint[] Indices { get; set; } = [];

        public TextureData TextureData { get; set; }

        public Matrix4x4 LocalMatrix { get; set; }
        
    }
}
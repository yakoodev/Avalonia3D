using System.Numerics;
using System.Runtime.InteropServices;

namespace Avalonia3D.Model
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position; // 0..11
        public Vector3 Normal;   // 12..23
        public Vector2 TexCoord; // 24..31
        public Vector2 TexCoord1; // 32..39
    }

}
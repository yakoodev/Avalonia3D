using Silk.NET.OpenGL;
using System.Numerics;

namespace Avalonia3D.Model
{
    public class Material
    {
        public Vector3 AlbedoColor { get; set; } = new(1, 1, 1);   // Базовый цвет (diffuse)
        public float Metallic { get; set; } = 0.0f;                // Насколько материал металлический
        public float Roughness { get; set; } = 1.0f;               // Насколько шероховатый
        public float Opacity { get; set; } = 1.0f;                 // Прозрачность

        public Texture? AlbedoMap { get; set; }                    // Текстура цвета
        public Texture? NormalMap { get; set; }                    // Нормали
        public Texture? MetallicRoughnessMap { get; set; }         // PBR текстура

        public Shader Shader { get; set; }                         // Шейдер, применяющий материал
    }
}

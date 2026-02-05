using System.Numerics;

namespace Avalonia3D.Lights
{
    public class Light
    {
        public float Intensity { get; set; } = 0.5f;
        public int Shininess { get; set; } = 16;
        public float SpecularStrength { get; set; } = 0.5f;
        public float AmbientStrength { get; set; } = 0.2f;
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Vector3 Color { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);

        public virtual Matrix4x4 LightSpaceMatrix { get; protected set; } = Matrix4x4.Identity;

        /// <summary>
        /// Вычислить матрицу пространства света (по умолчанию — Identity).
        /// </summary>
        public virtual void UpdateLightMatrix()
        {
            LightSpaceMatrix = Matrix4x4.Identity;
        }
    }
}

using System;
using System.Numerics;

namespace Avalonia3D.Lights
{
    public class PointLight : Light
    {
        public new Vector3 Position { get; set; }
        public float Range { get; set; } = 10f;

        public override void UpdateLightMatrix()
        {
            // Перспективная проекция для point light (направление не учитывается)
            float fov = MathF.PI / 2f; // 90° для cube-map
            float aspect = 1f;         // квадратная карта теней
            float near = 0.1f;
            float far = Range;

            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, near, far);
            Matrix4x4 view = Matrix4x4.CreateLookAt(Position, Position + Vector3.UnitZ, Vector3.UnitY);

            LightSpaceMatrix = view * projection;
        }
    }
}
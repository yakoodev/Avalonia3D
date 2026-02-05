using System.Numerics;

namespace Avalonia3D.Lights
{
    public class DirectionalLight : Light
    {
        /// <summary>
        /// Направление света (единичный вектор).
        /// </summary>
        public Vector3 Direction { get; set; } = -Vector3.UnitY;

        /// <summary>
        /// Цель освещения (обычно центр сцены).
        /// </summary>
        public Vector3 Target { get; set; } = Vector3.Zero;

        /// <summary>
        /// Дальность, на которой "располагается" источник (виртуально).
        /// </summary>
        public float Distance { get; set; } = 30f;

        public override void UpdateLightMatrix()
        {
            float size = 20f;
            float near = 0.1f;
            float far = 100f;

            var projection = Matrix4x4.CreateOrthographic(size, size, near, far);

            // Вычисляем позицию света от Target вдоль обратного направления
            var lightPos = Target - Vector3.Normalize(Direction) * Distance;

            var view = Matrix4x4.CreateLookAt(lightPos, Target, Vector3.UnitY);

            LightSpaceMatrix = view * projection;
        }
    }
}

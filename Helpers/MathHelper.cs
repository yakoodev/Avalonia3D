using System;
using System.Numerics;

namespace Avalonia3D.Helpers
{
    public static class MathHelper 
    {
        public const float ToRad = (float)Math.PI / 180;
        public const float ToDeg = (float)(180f / Math.PI);

        public static Vector3 GetScale(this Matrix4x4 m)
        {
            var scaleX = new Vector3(m.M11, m.M12, m.M13).Length();
            var scaleY = new Vector3(m.M21, m.M22, m.M23).Length();
            var scaleZ = new Vector3(m.M31, m.M32, m.M33).Length();

            return new Vector3(scaleX, scaleY, scaleZ);
        }

        public static Vector2 ProjectToScreen(Vector3 vertex, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, float width, float height)
        {
            var mvp = model * view * projection;
            Vector4 clip = Vector4.Transform(new Vector4(vertex, 1.0f), mvp);

            Vector3 ndc = new(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);

            float screenX = (ndc.X * 0.5f + 0.5f) * width;
            float screenY = (1.0f - (ndc.Y * 0.5f + 0.5f)) * height;

            return new Vector2(screenX, screenY);
        }

        public static Quaternion GetRotation(this Matrix4x4 m)
        {
            // 1. Достаём scale (как ты уже сделал)
            var scaleX = new Vector3(m.M11, m.M12, m.M13).Length();
            var scaleY = new Vector3(m.M21, m.M22, m.M23).Length();
            var scaleZ = new Vector3(m.M31, m.M32, m.M33).Length();

            // 2. Нормализуем базисные векторы (убираем масштаб)
            var m11 = m.M11 / scaleX;
            var m12 = m.M12 / scaleX;
            var m13 = m.M13 / scaleX;

            var m21 = m.M21 / scaleY;
            var m22 = m.M22 / scaleY;
            var m23 = m.M23 / scaleY;

            var m31 = m.M31 / scaleZ;
            var m32 = m.M32 / scaleZ;
            var m33 = m.M33 / scaleZ;

            // 3. Собираем чистую rotation matrix
            var rot = new Matrix4x4(
                m11, m12, m13, 0,
                m21, m22, m23, 0,
                m31, m32, m33, 0,
                0, 0, 0, 1
            );

            // 4. Преобразуем в Quaternion
            return Quaternion.CreateFromRotationMatrix(rot);
        }
    }
}

using System.Numerics;

namespace Avalonia3D.Animation
{
    public static class Interpolators
    {
        public static float LerpFloat(float a, float b, float t) => a + (b - a) * t;

        public static Vector3 LerpVector3(Vector3 a, Vector3 b, float t) =>
            Vector3.Lerp(a, b, t);

        public static Quaternion SlerpQuaternion(Quaternion a, Quaternion b, float t) =>
            Quaternion.Slerp(a, b, t);
    }
}

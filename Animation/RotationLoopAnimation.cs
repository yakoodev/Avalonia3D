using Serilog;
using System.Numerics;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Animation
{
    public class RotationLoopAnimation : IAnimation
    {
        private readonly SceneObject _target;
        private readonly Vector3 _axis;
        private readonly float _speed; // радиан/сек
        public bool Enable {  get; set; } = true;

        public RotationLoopAnimation(SceneObject target, Vector3 axis, float speed)
        {
            _target = target;
            _axis = Vector3.Normalize(axis);
            _speed = speed;
        }

        public bool IsSingtone => true;

        public bool Update(float deltaTime)
        {
            var deltaRotation = Quaternion.CreateFromAxisAngle(_axis, _speed * deltaTime);
            _target.Rotation = Quaternion.Normalize(Quaternion.Slerp(_target.Rotation, deltaRotation * _target.Rotation, 1.0f));
            return Enable; // никогда не завершится
        }
    }
}

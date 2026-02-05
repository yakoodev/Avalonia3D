using Avalonia3D.Model.StandObjects;
using System;
using System.Numerics;

namespace Avalonia3D.Animation
{
    public class RotationAnimation : IAnimation
    {
        private MeshObject _target;
        private Quaternion _start;
        private Quaternion _end;
        private float _duration;
        private float _elapsed;

        public RotationAnimation(MeshObject target, Quaternion end, float duration)
        {
            _target = target;
            _start = target.Rotation;
            _end = end;
            _duration = duration;
            _elapsed = 0;
        }

        public bool IsSingtone => true;

        public bool Update(float deltaTime)
        {
            _elapsed += deltaTime;
            float t = Math.Clamp(_elapsed / _duration, 0, 1);

            // Slerp плавно интерполирует кватернионы
            _target.Rotation = Quaternion.Slerp(_start, _end, t);

            return _elapsed < _duration;
        }
    }
}

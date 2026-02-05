using System;

namespace Avalonia3D.Animation
{
    public class PropertyAnimation<T> : IAnimation
    {
        private readonly Action<T> _setter;
        private readonly Func<T> _getter;
        private readonly T _start;
        private readonly T _end;
        private readonly float _duration;
        private float _elapsed;
        private readonly Func<T, T, float, T> _interpolator;
        private readonly float _speedFactor;

        public bool IsSingtone => false;

        public PropertyAnimation(Func<T> getter, Action<T> setter,
                         T end, float duration,
                         Func<T, T, float, T> interpolator,
                         float speedFactor = 1f)
        {
            _getter = getter;
            _setter = setter;
            _start = getter();
            _end = end;
            _duration = duration;
            _speedFactor = speedFactor;
            _elapsed = 0;
            _interpolator = interpolator;
        }

        public bool Update(float deltaTime)
        {
            _elapsed += deltaTime * _speedFactor; // учитываем скорость
            float t = Math.Clamp(_elapsed / _duration, 0f, 1f);

            var value = _interpolator(_start, _end, t);
            _setter(value);

            return _elapsed < _duration;
        }
    }
}

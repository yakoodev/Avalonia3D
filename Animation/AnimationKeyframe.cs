using System;

namespace Avalonia3D.Animation
{
    public readonly struct AnimationKeyframe<T>
    {
        public AnimationKeyframe(float time, T value)
        {
            if (time < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(time), "Keyframe time must be non-negative.");
            }

            Time = time;
            Value = value;
        }

        public float Time { get; }
        public T Value { get; }
    }
}

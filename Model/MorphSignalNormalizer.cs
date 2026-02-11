using System;

namespace Avalonia3D.Model
{
    /// <summary>
    /// Tracks an observed signal range at runtime and maps incoming values to normalized [0..1] activation.
    /// Keeps fallback behavior resilient when source clips use non-unit weight ranges.
    /// </summary>
    public sealed class MorphSignalNormalizer
    {
        private const float MinRange = 0.0001f;
        private float _observedMin = float.PositiveInfinity;
        private float _observedMax = float.NegativeInfinity;

        public void Reset()
        {
            _observedMin = float.PositiveInfinity;
            _observedMax = float.NegativeInfinity;
        }

        public float Normalize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            if (value < _observedMin)
            {
                _observedMin = value;
            }

            if (value > _observedMax)
            {
                _observedMax = value;
            }

            var range = _observedMax - _observedMin;
            if (range < MinRange)
            {
                // Early warm-up: fall back to clamped raw value.
                return Math.Clamp(value, 0f, 1f);
            }

            return Math.Clamp((value - _observedMin) / range, 0f, 1f);
        }
    }
}

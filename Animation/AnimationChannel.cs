using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Animation
{
    public enum AnimationTargetProperty
    {
        Position,
        Rotation,
        Scale,
        EmissiveIntensity,
        EmissiveColor,
        MorphWeights
    }

    public class AnimationChannel
    {
        public AnimationChannel(string targetNodeKey, AnimationTargetProperty property)
        {
            TargetNodeKey = targetNodeKey;
            Property = property;
        }

        public string TargetNodeKey { get; }
        public string TargetNodeName => TargetNodeKey;
        public AnimationTargetProperty Property { get; }

        public List<AnimationKeyframe<Vector3>> Vector3Keyframes { get; } = [];
        public List<AnimationKeyframe<Quaternion>> QuaternionKeyframes { get; } = [];
        public List<AnimationKeyframe<float>> FloatKeyframes { get; } = [];
        public List<AnimationKeyframe<float[]>> FloatArrayKeyframes { get; } = [];

        public bool HasData => Vector3Keyframes.Count > 0 || QuaternionKeyframes.Count > 0 || FloatKeyframes.Count > 0 || FloatArrayKeyframes.Count > 0;

        public void AddKeyframe(float time, Vector3 value)
        {
            Vector3Keyframes.Add(new AnimationKeyframe<Vector3>(time, value));
            Vector3Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public void AddKeyframe(float time, Quaternion value)
        {
            QuaternionKeyframes.Add(new AnimationKeyframe<Quaternion>(time, value));
            QuaternionKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public void AddKeyframe(float time, float value)
        {
            FloatKeyframes.Add(new AnimationKeyframe<float>(time, value));
            FloatKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public void AddKeyframe(float time, float[] value)
        {
            var copy = value == null ? [] : (float[])value.Clone();
            FloatArrayKeyframes.Add(new AnimationKeyframe<float[]>(time, copy));
            FloatArrayKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public float Duration
        {
            get
            {
                float max = 0;
                if (Vector3Keyframes.Count > 0)
                {
                    max = System.MathF.Max(max, Vector3Keyframes[^1].Time);
                }

                if (QuaternionKeyframes.Count > 0)
                {
                    max = System.MathF.Max(max, QuaternionKeyframes[^1].Time);
                }

                if (FloatKeyframes.Count > 0)
                {
                    max = System.MathF.Max(max, FloatKeyframes[^1].Time);
                }

                if (FloatArrayKeyframes.Count > 0)
                {
                    max = System.MathF.Max(max, FloatArrayKeyframes[^1].Time);
                }

                return max;
            }
        }

        public Vector3 SampleVector3(float time)
        {
            if (Vector3Keyframes.Count == 0)
            {
                return Vector3.Zero;
            }

            if (Vector3Keyframes.Count == 1)
            {
                return Vector3Keyframes[0].Value;
            }

            return Sample(time, Vector3Keyframes, Vector3.Lerp);
        }

        public Quaternion SampleQuaternion(float time)
        {
            if (QuaternionKeyframes.Count == 0)
            {
                return Quaternion.Identity;
            }

            if (QuaternionKeyframes.Count == 1)
            {
                return QuaternionKeyframes[0].Value;
            }

            return Sample(time, QuaternionKeyframes, Quaternion.Slerp);
        }

        public float SampleFloat(float time)
        {
            if (FloatKeyframes.Count == 0)
            {
                return 0f;
            }

            if (FloatKeyframes.Count == 1)
            {
                return FloatKeyframes[0].Value;
            }

            return Sample(time, FloatKeyframes, static (a, b, t) => a + ((b - a) * t));
        }

        public float[] SampleFloatArray(float time)
        {
            if (FloatArrayKeyframes.Count == 0)
            {
                return [];
            }

            if (FloatArrayKeyframes.Count == 1)
            {
                return (float[])FloatArrayKeyframes[0].Value.Clone();
            }

            return Sample(time, FloatArrayKeyframes, static (a, b, t) => LerpFloatArrays(a, b, t));
        }

        private static float[] LerpFloatArrays(float[] a, float[] b, float t)
        {
            if (a == null || a.Length == 0)
            {
                return b == null ? [] : (float[])b.Clone();
            }

            if (b == null || b.Length == 0)
            {
                return (float[])a.Clone();
            }

            var len = System.Math.Min(a.Length, b.Length);
            var result = new float[len];
            for (var i = 0; i < len; i++)
            {
                result[i] = a[i] + ((b[i] - a[i]) * t);
            }

            return result;
        }

        private static T Sample<T>(float time, List<AnimationKeyframe<T>> keyframes, System.Func<T, T, float, T> lerp)
        {
            if (time <= keyframes[0].Time)
            {
                return keyframes[0].Value;
            }

            if (time >= keyframes[^1].Time)
            {
                return keyframes[^1].Value;
            }

            var index = keyframes.FindIndex(k => k.Time >= time);
            if (index <= 0)
            {
                return keyframes[0].Value;
            }

            var previous = keyframes[index - 1];
            var next = keyframes[index];
            var segment = next.Time - previous.Time;
            if (segment <= 0)
            {
                return next.Value;
            }

            var t = (time - previous.Time) / segment;
            return lerp(previous.Value, next.Value, t);
        }
    }
}

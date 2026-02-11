using System;
using Avalonia3D.Model;

namespace Avalonia3D.Animation
{
    public sealed class AnimationClipPlayer : IAnimation
    {
        private bool _isStopped;

        public AnimationClipPlayer(AnimationClip clip, SceneGraph sceneGraph, Action<AnimationClipPlayer>? onCompleted = null)
        {
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            SceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
            OnCompleted = onCompleted;
            RebindNodes();
        }

        public AnimationClip Clip { get; }
        public SceneGraph SceneGraph { get; private set; }
        public bool Loop { get; private set; }
        public float Speed { get; private set; } = 1f;
        public bool IsPaused { get; private set; }
        public float Time { get; private set; }
        public bool IsSingtone => false;

        private Action<AnimationClipPlayer>? OnCompleted { get; }

        public void SetSceneGraph(SceneGraph sceneGraph)
        {
            SceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
            RebindNodes();
        }

        public void Play(bool loop, float speed)
        {
            Loop = loop;
            Speed = speed;
            IsPaused = false;
            Time = 0;
            _isStopped = false;
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        public void Stop()
        {
            IsPaused = false;
            Time = 0;
            _isStopped = true;
        }

        public bool Update(float deltaTime)
        {
            if (_isStopped)
            {
                return false;
            }

            if (IsPaused)
            {
                return true;
            }

            var duration = Clip.Duration;
            if (duration <= 0)
            {
                Apply(0);
                OnCompleted?.Invoke(this);
                return false;
            }

            Time += deltaTime * Speed;
            if (Time >= duration)
            {
                if (Loop)
                {
                    Time %= duration;
                }
                else
                {
                    Apply(duration);
                    OnCompleted?.Invoke(this);
                    return false;
                }
            }

            Apply(Time);
            return true;
        }

        private void Apply(float time)
        {
            foreach (var channel in Clip.Channels)
            {
                channel.Binding?.Apply(channel, time);
            }
        }

        private void RebindNodes()
        {
            foreach (var channel in Clip.Channels)
            {
                channel.Binding ??= CreateDefaultBinding(channel);
                channel.Binding?.Rebind(SceneGraph);
            }
        }

        private IAnimationTargetBinding? CreateDefaultBinding(AnimationChannel channel)
        {
            return channel.Property switch
            {
                AnimationTargetProperty.Position or AnimationTargetProperty.Rotation or AnimationTargetProperty.Scale
                    => new NodeTransformBinding(channel.TargetNodeKey, channel.Property),
                AnimationTargetProperty.MorphWeights
                    => new NodeMorphBinding(channel.TargetNodeKey),
                AnimationTargetProperty.EmissiveColor or AnimationTargetProperty.EmissiveIntensity or AnimationTargetProperty.BaseColorFactor
                    => new NodeMaterialPropertyBinding(channel.TargetNodeKey, channel.Property),
                AnimationTargetProperty.TextureTransformOffset or AnimationTargetProperty.TextureTransformScale or AnimationTargetProperty.TextureTransformRotation or AnimationTargetProperty.TextureTransformTexCoord
                    => null,
                _ => null
            };
        }
    }
}

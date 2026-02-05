using System;
using System.Collections.Generic;
using Avalonia3D.Model;

namespace Avalonia3D.Animation
{
    public sealed class AnimationClipPlayer : IAnimation
    {
        private readonly Dictionary<AnimationChannel, SceneNode?> _channelNodes = new();

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
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public void Stop()
        {
            IsPaused = false;
            Time = 0;
        }

        public bool Update(float deltaTime)
        {
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
                    Time = Time % duration;
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
                if (!_channelNodes.TryGetValue(channel, out var node) || node == null)
                {
                    continue;
                }

                switch (channel.Property)
                {
                    case AnimationTargetProperty.Position:
                        if (channel.Vector3Keyframes.Count > 0)
                        {
                            node.Position = channel.SampleVector3(time);
                        }
                        break;
                    case AnimationTargetProperty.Scale:
                        if (channel.Vector3Keyframes.Count > 0)
                        {
                            node.Scale = channel.SampleVector3(time);
                        }
                        break;
                    case AnimationTargetProperty.Rotation:
                        if (channel.QuaternionKeyframes.Count > 0)
                        {
                            node.Rotation = channel.SampleQuaternion(time);
                        }
                        break;
                }
            }
        }

        private void RebindNodes()
        {
            _channelNodes.Clear();
            foreach (var channel in Clip.Channels)
            {
                var node = SceneGraph.FindNode(channel.TargetNodeName);
                _channelNodes[channel] = node;
            }
        }
    }
}

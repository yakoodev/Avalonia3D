using System;
using System.Collections.Generic;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Serilog;

namespace Avalonia3D.Animation
{
    public sealed class AnimationClipPlayer : IAnimation
    {
        private readonly Dictionary<AnimationChannel, SceneNode?> _channelNodes = new();
        private readonly Dictionary<AnimationChannel, MeshObject?> _channelMaterialTargets = new();
        private readonly Dictionary<AnimationChannel, IReadOnlyList<MeshObject>> _channelMorphTargets = new();
        private readonly HashSet<AnimationChannel> _loggedMorphChannelBindings = new();
        private readonly HashSet<AnimationChannel> _loggedMorphChannelMissingTargets = new();
        private bool _isStopped;
        private AnimationMaterialTargetResolver _materialTargetResolver;
        private AnimationMorphTargetResolver _morphTargetResolver;

        public AnimationClipPlayer(AnimationClip clip, SceneGraph sceneGraph, Action<AnimationClipPlayer>? onCompleted = null)
        {
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            SceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
            OnCompleted = onCompleted;
            _materialTargetResolver = new AnimationMaterialTargetResolver(SceneGraph);
            _morphTargetResolver = new AnimationMorphTargetResolver(SceneGraph);
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
            _materialTargetResolver = new AnimationMaterialTargetResolver(SceneGraph);
            _morphTargetResolver = new AnimationMorphTargetResolver(SceneGraph);
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
                switch (channel.Property)
                {
                    case AnimationTargetProperty.Position:
                    case AnimationTargetProperty.Scale:
                    case AnimationTargetProperty.Rotation:
                        ApplyNodeTransformChannel(channel, time);
                        break;
                    case AnimationTargetProperty.EmissiveIntensity:
                    case AnimationTargetProperty.EmissiveColor:
                        ApplyMaterialChannel(channel, time);
                        break;
                    case AnimationTargetProperty.MorphWeights:
                        ApplyMorphWeightsChannel(channel, time);
                        break;
                }
            }
        }

        private void ApplyNodeTransformChannel(AnimationChannel channel, float time)
        {
            if (!_channelNodes.TryGetValue(channel, out var node) || node == null)
            {
                return;
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

        private void ApplyMaterialChannel(AnimationChannel channel, float time)
        {
            if (!_channelMaterialTargets.TryGetValue(channel, out var meshObject) || meshObject?.Material == null)
            {
                return;
            }

            switch (channel.Property)
            {
                case AnimationTargetProperty.EmissiveIntensity:
                    if (channel.FloatKeyframes.Count > 0)
                    {
                        meshObject.Material.EmissiveIntensity = channel.SampleFloat(time);
                    }
                    break;
                case AnimationTargetProperty.EmissiveColor:
                    if (channel.Vector3Keyframes.Count > 0)
                    {
                        meshObject.Material.EmissiveFactor = channel.SampleVector3(time);
                    }
                    break;
            }
        }


        private void ApplyMorphWeightsChannel(AnimationChannel channel, float time)
        {
            if (!_channelNodes.TryGetValue(channel, out var node) || node == null)
            {
                return;
            }

            if (channel.FloatArrayKeyframes.Count > 0)
            {
                var weights = channel.SampleFloatArray(time);
                node.MorphWeights = weights;

                if (!_loggedMorphChannelBindings.Contains(channel))
                {
                    _loggedMorphChannelBindings.Add(channel);
                    Log.Debug("Applying morph channel for node '{NodeKey}': weights={WeightCount}, keyframes={KeyframeCount}",
                        channel.TargetNodeKey,
                        weights?.Length ?? 0,
                        channel.FloatArrayKeyframes.Count);
                }

                if (_channelMorphTargets.TryGetValue(channel, out var targets))
                {
                    if (targets.Count == 0 && !_loggedMorphChannelMissingTargets.Contains(channel))
                    {
                        _loggedMorphChannelMissingTargets.Add(channel);
                        Log.Warning("Morph channel for node '{NodeKey}' resolved no mesh targets at runtime.", channel.TargetNodeKey);
                    }

                    foreach (var target in targets)
                    {
                        target.SetMorphWeights(weights);
                    }
                }
            }
        }

        private void RebindNodes()
        {
            _channelNodes.Clear();
            _channelMaterialTargets.Clear();
            _channelMorphTargets.Clear();
            _loggedMorphChannelBindings.Clear();
            _loggedMorphChannelMissingTargets.Clear();

            foreach (var channel in Clip.Channels)
            {
                var node = SceneGraph.FindNodeByKey(channel.TargetNodeKey);
                _channelNodes[channel] = node;
                _channelMaterialTargets[channel] = NeedsMaterialTarget(channel.Property)
                    ? node != null ? _materialTargetResolver.ResolveByNode(node) : null
                    : null;

                _channelMorphTargets[channel] = channel.Property == AnimationTargetProperty.MorphWeights
                    ? _morphTargetResolver.ResolveTargets(channel.TargetNodeKey)
                    : [];
            }
        }

        private static bool NeedsMaterialTarget(AnimationTargetProperty property)
        {
            return property == AnimationTargetProperty.EmissiveIntensity
                || property == AnimationTargetProperty.EmissiveColor;
        }
    }
}

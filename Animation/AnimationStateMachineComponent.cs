using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Avalonia3D.Animation;

public sealed class AnimationStateMachineComponent : IAnimationStateMachine
{
    private const string MoveParameterName = "Move";
    private const string ActionParameterName = "Action";

    private readonly AnimatorComponent _animatorComponent;
    private readonly Dictionary<string, AnimationParameter> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StateDefinition> _states = new(StringComparer.Ordinal);
    private readonly List<IAnimationTransition> _transitions = [];

    private readonly AnimationMorphTargetResolver _morphResolver;
    private readonly AnimationMaterialTargetResolver _materialResolver;

    private StateDefinition _current;
    private ActiveStatePlayback? _next;
    private float _transitionTime;

    public AnimationStateMachineComponent(
        AnimatorComponent animatorComponent,
        string idleClip,
        string moveClip,
        string actionClip,
        float defaultCrossFade = 0.2f)
    {
        _animatorComponent = animatorComponent ?? throw new ArgumentNullException(nameof(animatorComponent));
        _morphResolver = new AnimationMorphTargetResolver(animatorComponent.SceneGraph);
        _materialResolver = new AnimationMaterialTargetResolver(animatorComponent.SceneGraph);

        RegisterState("Idle", idleClip, loop: true, playbackSpeed: 1f);
        RegisterState("Move", moveClip, loop: true, playbackSpeed: 1f);
        RegisterState("Action", actionClip, loop: false, playbackSpeed: 1f);

        _transitions.Add(new AnimationTransition("Idle", "Move", 1, true, defaultCrossFade, (p, _) => GetBool(p, MoveParameterName)));
        _transitions.Add(new AnimationTransition("Move", "Idle", 1, true, defaultCrossFade, (p, _) => !GetBool(p, MoveParameterName)));
        _transitions.Add(new AnimationTransition("Idle", "Action", 10, true, defaultCrossFade, (p, _) => GetBool(p, ActionParameterName)));
        _transitions.Add(new AnimationTransition("Move", "Action", 10, true, defaultCrossFade, (p, _) => GetBool(p, ActionParameterName)));
        _transitions.Add(new AnimationTransition("Action", "Move", 1, false, defaultCrossFade, (p, completed) => completed && GetBool(p, MoveParameterName)));
        _transitions.Add(new AnimationTransition("Action", "Idle", 1, false, defaultCrossFade, (p, completed) => completed && !GetBool(p, MoveParameterName)));

        _current = _states["Idle"];
    }

    public string CurrentState => _current.Name;
    public bool IsInTransition => _next != null;

    public void SetStateSpeed(string stateName, float playbackSpeed)
    {
        if (_states.TryGetValue(stateName, out var state))
        {
            state.PlaybackSpeed = MathF.Max(0f, playbackSpeed);
        }
    }

    public void SetParameter(AnimationParameter parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name))
        {
            return;
        }

        _parameters[parameter.Name] = parameter;
    }

    public IReadOnlyDictionary<string, AnimationParameter> GetParameters() => _parameters;

    public void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            return;
        }

        var currentPlayback = _current.Playback;
        currentPlayback.Advance(deltaTime);

        var interruptTransition = SelectTransition(_current.Name, _current.Playback.IsCompleted, requireInterrupt: _next != null);
        if (interruptTransition != null)
        {
            BeginTransition(_states[interruptTransition.ToState], interruptTransition.CrossFadeDuration);
            ConsumeTriggerParameters();
        }

        if (_next == null)
        {
            var transition = SelectTransition(_current.Name, _current.Playback.IsCompleted, requireInterrupt: false);
            if (transition != null)
            {
                BeginTransition(_states[transition.ToState], transition.CrossFadeDuration);
                ConsumeTriggerParameters();
            }
        }

        if (_next == null)
        {
            ApplySingle(_current.Playback);
            return;
        }

        _next.Advance(deltaTime);
        _transitionTime += deltaTime;

        var duration = MathF.Max(0.0001f, _next.CrossFadeDuration);
        var alpha = Math.Clamp(_transitionTime / duration, 0f, 1f);
        ApplyBlend(_current.Playback, _next, alpha);

        if (alpha >= 1f)
        {
            _current = _states[_next.Name];
            _next = null;
            _transitionTime = 0f;
        }
    }

    private void RegisterState(string stateName, string clipName, bool loop, float playbackSpeed)
    {
        if (!_animatorComponent.TryGetClip(clipName, out var clip))
        {
            throw new InvalidOperationException($"Clip '{clipName}' is not registered in AnimatorComponent.");
        }

        _states[stateName] = new StateDefinition(stateName, new ActiveStatePlayback(clip, loop, playbackSpeed));
    }

    private void BeginTransition(StateDefinition targetState, float crossFadeDuration)
    {
        _next = new ActiveStatePlayback(targetState.Playback.Clip, targetState.Playback.Loop, targetState.Playback.PlaybackSpeed)
        {
            Name = targetState.Name,
            CrossFadeDuration = MathF.Max(0.01f, crossFadeDuration)
        };
        _transitionTime = 0f;
    }

    private IAnimationTransition? SelectTransition(string fromState, bool sourceCompleted, bool requireInterrupt)
    {
        return _transitions
            .Where(t => string.Equals(t.FromState, fromState, StringComparison.Ordinal)
                && (!requireInterrupt || t.CanInterrupt)
                && t.CanTransition(_parameters, sourceCompleted))
            .OrderByDescending(t => t.Priority)
            .FirstOrDefault();
    }

    private static bool GetBool(IReadOnlyDictionary<string, AnimationParameter> parameters, string name)
    {
        return parameters.TryGetValue(name, out var p) && p.Type != AnimationParameterType.Float && p.BoolValue;
    }

    private void ConsumeTriggerParameters()
    {
        foreach (var key in _parameters.Where(static x => x.Value.Type == AnimationParameterType.Trigger).Select(static x => x.Key).ToArray())
        {
            _parameters[key] = AnimationParameter.Bool(key, false);
        }
    }

    private void ApplySingle(ActiveStatePlayback playback)
    {
        foreach (var channel in playback.Clip.Channels)
        {
            ApplySample(channel, playback.Time, 1f, null, 0f);
        }
    }

    private void ApplyBlend(ActiveStatePlayback from, ActiveStatePlayback to, float alpha)
    {
        var fromMap = from.Clip.Channels.ToDictionary(static c => GetChannelKey(c), StringComparer.Ordinal);
        var toMap = to.Clip.Channels.ToDictionary(static c => GetChannelKey(c), StringComparer.Ordinal);

        var allKeys = fromMap.Keys.Concat(toMap.Keys).Distinct(StringComparer.Ordinal);
        foreach (var key in allKeys)
        {
            fromMap.TryGetValue(key, out var fromChannel);
            toMap.TryGetValue(key, out var toChannel);

            if (fromChannel == null)
            {
                ApplySample(toChannel!, to.Time, alpha, null, 0f);
                continue;
            }

            if (toChannel == null)
            {
                ApplySample(fromChannel, from.Time, 1f - alpha, null, 0f);
                continue;
            }

            ApplySample(fromChannel, from.Time, 1f - alpha, toChannel, alpha, to.Time);
        }
    }

    private void ApplySample(AnimationChannel channel, float time, float weight, AnimationChannel? blendWith, float blendWeight, float blendTime = 0f)
    {
        var node = _animatorComponent.SceneGraph.FindNodeByKey(channel.TargetNodeKey);
        var mesh = _materialResolver.ResolveByNodeKey(channel.TargetNodeKey);

        if (channel.Vector3Keyframes.Count > 0)
        {
            var value = channel.SampleVector3(time);
            if (blendWith is { Vector3Keyframes.Count: > 0 })
            {
                value = Vector3.Lerp(value, blendWith.SampleVector3(blendTime), blendWeight);
            }

            value *= Math.Clamp(weight + blendWeight, 0f, 1f);
            switch (channel.Property)
            {
                case AnimationTargetProperty.Position when node != null:
                    node.Position = value;
                    break;
                case AnimationTargetProperty.Scale when node != null:
                    node.Scale = value;
                    break;
                case AnimationTargetProperty.EmissiveColor when mesh?.Material != null:
                    mesh.Material.EmissiveFactor = value;
                    break;
                case AnimationTargetProperty.BaseColorFactor when mesh?.Material != null:
                    mesh.Material.BaseColorFactor = new Vector4(value, mesh.Material.BaseColorFactor.W);
                    break;
            }
        }

        if (channel.QuaternionKeyframes.Count > 0 && node != null)
        {
            var value = channel.SampleQuaternion(time);
            if (blendWith is { QuaternionKeyframes.Count: > 0 })
            {
                value = Quaternion.Slerp(value, blendWith.SampleQuaternion(blendTime), blendWeight);
            }

            node.Rotation = value;
        }

        if (channel.FloatKeyframes.Count > 0 && channel.Property == AnimationTargetProperty.EmissiveIntensity && mesh?.Material != null)
        {
            var value = channel.SampleFloat(time);
            if (blendWith is { FloatKeyframes.Count: > 0 })
            {
                value = Interpolators.LerpFloat(value, blendWith.SampleFloat(blendTime), blendWeight);
            }

            mesh.Material.EmissiveIntensity = value;
        }

        if (channel.FloatArrayKeyframes.Count > 0 && channel.Property == AnimationTargetProperty.MorphWeights && node != null)
        {
            var value = channel.SampleFloatArray(time);
            if (blendWith is { FloatArrayKeyframes.Count: > 0 })
            {
                var to = blendWith.SampleFloatArray(blendTime);
                var len = Math.Min(value.Length, to.Length);
                var blend = new float[len];
                for (var i = 0; i < len; i++)
                {
                    blend[i] = Interpolators.LerpFloat(value[i], to[i], blendWeight);
                }

                value = blend;
            }

            node.MorphWeights = value;
            foreach (var target in _morphResolver.ResolveTargets(channel.TargetNodeKey))
            {
                target.SetMorphWeights(value);
            }
        }
    }

    private static string GetChannelKey(AnimationChannel channel) => $"{channel.TargetNodeKey}:{channel.Property}";

    private sealed class StateDefinition
    {
        public StateDefinition(string name, ActiveStatePlayback playback)
        {
            Name = name;
            Playback = playback;
        }

        public string Name { get; }
        public ActiveStatePlayback Playback { get; }
        public float PlaybackSpeed
        {
            get => Playback.PlaybackSpeed;
            set => Playback.PlaybackSpeed = value;
        }
    }

    private sealed class ActiveStatePlayback : IAnimationBlendNode
    {
        public ActiveStatePlayback(AnimationClip clip, bool loop, float playbackSpeed)
        {
            Clip = clip;
            Loop = loop;
            PlaybackSpeed = playbackSpeed;
            Name = clip.Name;
        }

        public AnimationClip Clip { get; }
        public string Name { get; set; }
        public string ClipName => Clip.Name;
        public bool Loop { get; }
        public float PlaybackSpeed { get; set; }
        public float CrossFadeDuration { get; set; }
        public float Time { get; private set; }
        public bool IsCompleted { get; private set; }

        public void Advance(float deltaTime)
        {
            if (IsCompleted || deltaTime <= 0f)
            {
                return;
            }

            var duration = Clip.Duration;
            if (duration <= 0f)
            {
                IsCompleted = true;
                Time = 0f;
                return;
            }

            Time += deltaTime * PlaybackSpeed;
            if (Time >= duration)
            {
                if (Loop)
                {
                    Time %= duration;
                }
                else
                {
                    Time = duration;
                    IsCompleted = true;
                }
            }
        }
    }

    private sealed class AnimationTransition : IAnimationTransition
    {
        private readonly Func<IReadOnlyDictionary<string, AnimationParameter>, bool, bool> _condition;

        public AnimationTransition(string fromState, string toState, int priority, bool canInterrupt, float crossFadeDuration, Func<IReadOnlyDictionary<string, AnimationParameter>, bool, bool> condition)
        {
            FromState = fromState;
            ToState = toState;
            Priority = priority;
            CanInterrupt = canInterrupt;
            CrossFadeDuration = crossFadeDuration;
            _condition = condition;
        }

        public string FromState { get; }
        public string ToState { get; }
        public int Priority { get; }
        public bool CanInterrupt { get; }
        public float CrossFadeDuration { get; }

        public bool CanTransition(IReadOnlyDictionary<string, AnimationParameter> parameters, bool sourceCompleted) =>
            _condition(parameters, sourceCompleted);
    }
}

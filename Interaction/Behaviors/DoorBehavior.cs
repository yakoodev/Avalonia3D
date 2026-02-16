using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia3D.Animation;
using Avalonia3D.Model;

namespace Avalonia3D.Interaction.Behaviors;

public enum DoorNodeTargetKeyMode
{
    SemanticId,
    StableId,
    Name,
    Path
}

public readonly record struct DoorRuntimeRotationFallback(string TargetKey, DoorNodeTargetKeyMode KeyMode, Vector3 Axis, float OpenAngleDegrees)
{
    public Vector3 NormalizedAxis => Axis == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(Axis);
    public float OpenAngleRadians => OpenAngleDegrees * (MathF.PI / 180f);
}

public sealed class DoorBehavior : ISceneBehavior, ISceneCommandHandler
{
    private Scene3D? _scene;
    private SceneNode? _fallbackNode;
    private Quaternion _fallbackClosedRotation = Quaternion.Identity;
    private bool _hasFallbackClosedRotation;
    private bool _isOpen;
    private bool _isTransitionInProgress;

    public DoorBehavior(string semanticId, string? openClipName = null, string? closeClipName = null)
        : this(semanticId, openClipName, closeClipName, runtimeFallback: null)
    {
    }

    public DoorBehavior(
        string semanticId,
        string? openClipName,
        string? closeClipName,
        DoorRuntimeRotationFallback? runtimeFallback)
    {
        SemanticId = semanticId;
        OpenClipName = openClipName;
        CloseClipName = closeClipName;
        RuntimeFallback = runtimeFallback;
    }

    public string Id => $"door:{SemanticId}";
    public string SemanticId { get; }
    public string? OpenClipName { get; }
    public string? CloseClipName { get; }
    public DoorRuntimeRotationFallback? RuntimeFallback { get; }

    public bool CanHandle(SceneCommand command)
    {
        return string.Equals(command.TargetSemanticId, SemanticId, StringComparison.Ordinal)
            && (command.Action == SceneCommandAction.Open
                || command.Action == SceneCommandAction.Close
                || command.Action == SceneCommandAction.Toggle);
    }

    public bool Handle(SceneCommand command)
    {
        if (_scene == null || _isTransitionInProgress)
        {
            return false;
        }

        return command.Action switch
        {
            SceneCommandAction.Open => TryPlay(true),
            SceneCommandAction.Close => TryPlay(false),
            SceneCommandAction.Toggle => TryPlay(!_isOpen),
            _ => false
        };
    }

    public void Attach(Scene3D scene)
    {
        _scene = scene;
        _fallbackNode = ResolveFallbackNode(scene.SceneGraph);
        if (_fallbackNode != null)
        {
            _fallbackClosedRotation = _fallbackNode.Rotation;
            _hasFallbackClosedRotation = true;
        }

        scene.AnimatorComponent.ClipCompleted += OnClipCompleted;
    }

    public void Detach(Scene3D scene)
    {
        scene.AnimatorComponent.ClipCompleted -= OnClipCompleted;
        _fallbackNode = null;
        _hasFallbackClosedRotation = false;
        _isTransitionInProgress = false;
        _scene = null;
    }

    private bool TryPlay(bool open)
    {
        if (_scene == null)
        {
            return false;
        }

        var clipName = ResolveClipName(_scene.AnimatorComponent, open);
        if (clipName == null)
        {
            if (!TryApplyFallbackRotation(open))
            {
                return false;
            }

            _isOpen = open;
            return true;
        }

        if (!_scene.AnimatorComponent.PlayClip(clipName))
        {
            return false;
        }

        _isTransitionInProgress = true;
        _isOpen = open;
        return true;
    }

    private string? ResolveClipName(AnimatorComponent animatorComponent, bool open)
    {
        var configured = open ? OpenClipName : CloseClipName;
        if (!string.IsNullOrWhiteSpace(configured) && HasRegisteredClip(animatorComponent, configured))
        {
            return configured;
        }

        var suffix = open ? ".open" : ".close";
        var conventionalName = $"{SemanticId}{suffix}";

        foreach (var clipName in animatorComponent.GetClipNames())
        {
            if (string.Equals(clipName, conventionalName, StringComparison.Ordinal))
            {
                return clipName;
            }
        }

        return null;
    }

    private bool HasRegisteredClip(AnimatorComponent animatorComponent, string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return false;
        }

        foreach (var registeredClipName in animatorComponent.GetClipNames())
        {
            if (string.Equals(registeredClipName, clipName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private SceneNode? ResolveFallbackNode(SceneGraph graph)
    {
        if (RuntimeFallback == null || string.IsNullOrWhiteSpace(RuntimeFallback.Value.TargetKey))
        {
            return null;
        }

        var fallback = RuntimeFallback.Value;
        return fallback.KeyMode switch
        {
            DoorNodeTargetKeyMode.SemanticId => graph.FindNodeBySemanticId(fallback.TargetKey),
            DoorNodeTargetKeyMode.StableId => graph.FindNodeByStableId(fallback.TargetKey),
            DoorNodeTargetKeyMode.Name => graph.FindNode(fallback.TargetKey),
            DoorNodeTargetKeyMode.Path => graph.FindNodeByPath(fallback.TargetKey),
            _ => graph.FindNodeByKey(fallback.TargetKey)
        };
    }

    private bool TryApplyFallbackRotation(bool open)
    {
        if (_scene == null || RuntimeFallback == null)
        {
            return false;
        }

        _fallbackNode ??= ResolveFallbackNode(_scene.SceneGraph);
        if (_fallbackNode == null)
        {
            return false;
        }

        if (!_hasFallbackClosedRotation)
        {
            _fallbackClosedRotation = _fallbackNode.Rotation;
            _hasFallbackClosedRotation = true;
        }

        if (!open)
        {
            _fallbackNode.Rotation = _fallbackClosedRotation;
            return true;
        }

        var fallback = RuntimeFallback.Value;
        var delta = Quaternion.CreateFromAxisAngle(fallback.NormalizedAxis, fallback.OpenAngleRadians);
        _fallbackNode.Rotation = Quaternion.Normalize(delta * _fallbackClosedRotation);
        return true;
    }

    private void OnClipCompleted(object? sender, ClipPlaybackCompletedEventArgs args)
    {
        if (_scene == null)
        {
            return;
        }

        var openClip = ResolveClipName(_scene.AnimatorComponent, open: true);
        var closeClip = ResolveClipName(_scene.AnimatorComponent, open: false);
        if (string.Equals(args.ClipName, openClip, StringComparison.Ordinal)
            || string.Equals(args.ClipName, closeClip, StringComparison.Ordinal))
        {
            _isTransitionInProgress = false;
        }
    }
}

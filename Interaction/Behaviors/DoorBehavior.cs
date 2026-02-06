using System;
using System.Collections.Generic;
using Avalonia3D.Animation;
using Avalonia3D.Model;

namespace Avalonia3D.Interaction.Behaviors;

public sealed class DoorBehavior : ISceneBehavior, ISceneCommandHandler
{
    private Scene3D? _scene;
    private bool _isOpen;
    private bool _isTransitionInProgress;

    public DoorBehavior(string semanticId, string? openClipName = null, string? closeClipName = null)
    {
        SemanticId = semanticId;
        OpenClipName = openClipName;
        CloseClipName = closeClipName;
    }

    public string Id => $"door:{SemanticId}";
    public string SemanticId { get; }
    public string? OpenClipName { get; }
    public string? CloseClipName { get; }

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
        scene.AnimatorComponent.ClipCompleted += OnClipCompleted;
    }

    public void Detach(Scene3D scene)
    {
        scene.AnimatorComponent.ClipCompleted -= OnClipCompleted;
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
            return false;
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
        if (!string.IsNullOrWhiteSpace(configured))
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

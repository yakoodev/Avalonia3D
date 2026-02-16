using System;
using System.Numerics;
using Avalonia3D.Model;

namespace Avalonia3D.Interaction.Behaviors;

public enum WheelNodeTargetKeyMode
{
    SemanticId,
    StableId,
    Name,
    Path
}

public sealed class WheelRotationBehavior : IUpdatableBehavior, ISceneCommandHandler
{
    private SceneNode? _node;
    private bool _isSpinning;
    private Vector3 _rotationAxis;

    public WheelRotationBehavior(string semanticId, float radiansPerSecond)
        : this(semanticId, radiansPerSecond, WheelNodeTargetKeyMode.SemanticId, Vector3.UnitX)
    {
    }

    public WheelRotationBehavior(string targetKey, float radiansPerSecond, WheelNodeTargetKeyMode keyMode, Vector3 rotationAxis)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            throw new ArgumentException("Target key is required.", nameof(targetKey));
        }

        TargetKey = targetKey;
        RadiansPerSecond = radiansPerSecond;
        KeyMode = keyMode;
        RotationAxis = rotationAxis;
    }

    public string Id => $"wheel:{TargetKey}";
    public string TargetKey { get; }
    public string SemanticId => TargetKey;
    public WheelNodeTargetKeyMode KeyMode { get; }
    public float RadiansPerSecond { get; set; }
    public Vector3 RotationAxis
    {
        get => _rotationAxis;
        set => _rotationAxis = value == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(value);
    }

    public void Attach(Scene3D scene)
    {
        _node = ResolveNode(scene.SceneGraph);
    }

    public void Detach(Scene3D scene)
    {
        _node = null;
    }

    public void Update(float deltaTime)
    {
        if (!_isSpinning || _node == null)
        {
            return;
        }

        var delta = Quaternion.CreateFromAxisAngle(_rotationAxis, RadiansPerSecond * deltaTime);
        _node.Rotation = Quaternion.Normalize(delta * _node.Rotation);
    }

    public bool CanHandle(SceneCommand command)
    {
        return string.Equals(command.TargetSemanticId, TargetKey, StringComparison.Ordinal)
            && (command.Action == SceneCommandAction.Open
                || command.Action == SceneCommandAction.Close
                || command.Action == SceneCommandAction.Toggle);
    }

    public bool Handle(SceneCommand command)
    {
        _isSpinning = command.Action switch
        {
            SceneCommandAction.Open => true,
            SceneCommandAction.Close => false,
            SceneCommandAction.Toggle => !_isSpinning,
            _ => _isSpinning
        };

        return true;
    }

    private SceneNode? ResolveNode(SceneGraph graph)
    {
        return KeyMode switch
        {
            WheelNodeTargetKeyMode.SemanticId => graph.FindNodeBySemanticId(TargetKey),
            WheelNodeTargetKeyMode.StableId => graph.FindNodeByStableId(TargetKey),
            WheelNodeTargetKeyMode.Name => graph.FindNode(TargetKey),
            WheelNodeTargetKeyMode.Path => graph.FindNodeByPath(TargetKey),
            _ => graph.FindNodeByKey(TargetKey)
        };
    }
}

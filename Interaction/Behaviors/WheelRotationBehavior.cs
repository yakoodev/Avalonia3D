using System;
using System.Numerics;
using Avalonia3D.Model;

namespace Avalonia3D.Interaction.Behaviors;

public sealed class WheelRotationBehavior : IUpdatableBehavior, ISceneCommandHandler
{
    private Scene3D? _scene;
    private SceneNode? _node;
    private bool _isSpinning;

    public WheelRotationBehavior(string semanticId, float radiansPerSecond)
    {
        SemanticId = semanticId;
        RadiansPerSecond = radiansPerSecond;
    }

    public string Id => $"wheel:{SemanticId}";
    public string SemanticId { get; }
    public float RadiansPerSecond { get; set; }

    public void Attach(Scene3D scene)
    {
        _scene = scene;
        _node = scene.SceneGraph.FindNodeBySemanticId(SemanticId);
    }

    public void Detach(Scene3D scene)
    {
        _node = null;
        _scene = null;
    }

    public void Update(float deltaTime)
    {
        if (!_isSpinning || _node == null)
        {
            return;
        }

        var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitX, RadiansPerSecond * deltaTime);
        _node.Rotation = Quaternion.Normalize(delta * _node.Rotation);
    }

    public bool CanHandle(SceneCommand command)
    {
        return string.Equals(command.TargetSemanticId, SemanticId, StringComparison.Ordinal)
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
}

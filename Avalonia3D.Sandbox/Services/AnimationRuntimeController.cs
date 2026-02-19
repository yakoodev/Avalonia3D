using Avalonia3D.Animation;
using Avalonia3D.Model;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia3D.Sandbox.Services;

public sealed class AnimationRuntimeController : IAnimationRuntimeController
{
    private readonly Scene3D _scene;
    private readonly Dictionary<string, NodePose> _car2PoseSnapshot = new(StringComparer.Ordinal);

    public AnimationRuntimeController(Scene3D scene)
    {
        _scene = scene;
    }

    public IReadOnlyList<string> GetAvailableClips()
    {
        return _scene.AnimatorComponent.GetClipNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ClipPlaybackState GetClipState(string clipName)
    {
        return _scene.AnimatorComponent.GetClipState(clipName);
    }

    public void PlayClip(string clipName, bool loop, float speed)
    {
        _scene.AnimatorComponent.PlayClip(clipName, loop, speed);
    }

    public void PauseClip(string clipName)
    {
        _scene.AnimatorComponent.PauseClip(clipName);
    }

    public void StopClip(string clipName)
    {
        _scene.AnimatorComponent.StopClip(clipName);
    }

    public int RotateCar2Wheels(float radians)
    {
        var wheels = ResolveCar2WheelNodes();
        if (wheels.Count == 0)
        {
            return 0;
        }

        var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitX, radians);
        foreach (var wheel in wheels)
        {
            wheel.Rotation = Quaternion.Normalize(delta * wheel.Rotation);
        }

        return wheels.Count;
    }

    public bool TrySetCar2RootPositionDelta(Vector3 delta)
    {
        CaptureCar2Pose();
        var root = ResolveCar2RootNode();
        if (root is null)
        {
            return false;
        }

        root.Position += delta;
        return true;
    }

    public bool TrySetCar2RootYaw(float radians)
    {
        CaptureCar2Pose();
        var root = ResolveCar2RootNode();
        if (root is null)
        {
            return false;
        }

        var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians);
        root.Rotation = Quaternion.Normalize(delta * root.Rotation);
        return true;
    }

    public int ResetCar2Pose()
    {
        if (_car2PoseSnapshot.Count == 0)
        {
            CaptureCar2Pose();
        }

        if (_car2PoseSnapshot.Count == 0)
        {
            return 0;
        }

        var restored = 0;
        foreach (var node in EnumerateSceneNodes(_scene.SceneGraph.Root))
        {
            if (!_car2PoseSnapshot.TryGetValue(node.GetPath(), out var pose))
            {
                continue;
            }

            node.Position = pose.Position;
            node.Rotation = pose.Rotation;
            node.Scale = pose.Scale;
            restored++;
        }

        return restored;
    }

    public void CaptureCar2Pose()
    {
        if (_car2PoseSnapshot.Count > 0)
        {
            return;
        }

        var root = ResolveCar2RootNode();
        if (root is not null)
        {
            _car2PoseSnapshot[root.GetPath()] = NodePose.FromNode(root);
        }

        foreach (var wheel in ResolveCar2WheelNodes())
        {
            _car2PoseSnapshot[wheel.GetPath()] = NodePose.FromNode(wheel);
        }
    }

    private SceneNode? ResolveCar2RootNode() => _scene.SceneGraph.Root.Children.FirstOrDefault();

    private List<SceneNode> ResolveCar2WheelNodes()
    {
        return EnumerateSceneNodes(_scene.SceneGraph.Root)
            .Where(node => !string.IsNullOrWhiteSpace(node.Name)
                && node.Name.Contains("_tire_", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IEnumerable<SceneNode> EnumerateSceneNodes(SceneNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in EnumerateSceneNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private readonly record struct NodePose(Vector3 Position, Quaternion Rotation, Vector3 Scale)
    {
        public static NodePose FromNode(SceneNode node) => new(node.Position, node.Rotation, node.Scale);
    }
}

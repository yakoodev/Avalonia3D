using System;
using System.Numerics;
using Avalonia3D.Animation;
using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

public class SceneAndAnimationTests
{
    [Fact]
    public void CreateModelMatrix_WithNestedNodes_ComputesWorldMatrix()
    {
        var root = new SceneNode
        {
            Position = new Vector3(1f, 0f, 0f),
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f),
            Scale = new Vector3(2f, 2f, 2f)
        };

        var child = new SceneNode
        {
            Position = new Vector3(0f, 3f, 0f),
            Scale = new Vector3(1f, 2f, 1f)
        };

        root.AddChild(child);

        var worldMatrix = child.CreateModelMatrix();
        var expectedMatrix =
            Matrix4x4.CreateScale(child.Scale)
            * Matrix4x4.CreateFromQuaternion(child.Rotation)
            * Matrix4x4.CreateTranslation(child.Position)
            * Matrix4x4.CreateScale(root.Scale)
            * Matrix4x4.CreateFromQuaternion(root.Rotation)
            * Matrix4x4.CreateTranslation(root.Position);

        AssertMatrixEqual(expectedMatrix, worldMatrix);
    }

    [Fact]
    public void FindByName_FindsNestedNodeByName()
    {
        var graph = new SceneGraph();
        var level1 = new SceneNode { Name = "Level1" };
        var level2 = new SceneNode { Name = "Level2" };
        var target = new SceneNode { Name = "TargetNode" };

        graph.Root.AddChild(level1);
        level1.AddChild(level2);
        level2.AddChild(target);

        var found = graph.FindNode("TargetNode");

        Assert.Same(target, found);
    }

    [Fact]
    public void AnimationClipPlayer_Update_AppliesChannelValueToNode()
    {
        var graph = new SceneGraph();
        var animatedNode = new SceneNode { Name = "Arm" };
        graph.Root.AddChild(animatedNode);

        var clip = new AnimationClip("MoveArm");
        var channel = new AnimationChannel("Arm", AnimationTargetProperty.Position);
        channel.AddKeyframe(0f, new Vector3(0f, 0f, 0f));
        channel.AddKeyframe(1f, new Vector3(10f, 0f, 0f));
        clip.Channels.Add(channel);

        var player = new AnimationClipPlayer(clip, graph);
        player.Play(loop: false, speed: 1f);

        var isRunning = player.Update(0.5f);

        Assert.True(isRunning);
        AssertVectorEqual(new Vector3(5f, 0f, 0f), animatedNode.Position);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual, float epsilon = 0.0001f)
    {
        Assert.True(Vector3.Distance(expected, actual) <= epsilon,
            $"Expected vector {expected}, actual {actual}.");
    }

    private static void AssertMatrixEqual(Matrix4x4 expected, Matrix4x4 actual, float epsilon = 0.0001f)
    {
        Assert.True(MathF.Abs(expected.M11 - actual.M11) <= epsilon);
        Assert.True(MathF.Abs(expected.M12 - actual.M12) <= epsilon);
        Assert.True(MathF.Abs(expected.M13 - actual.M13) <= epsilon);
        Assert.True(MathF.Abs(expected.M14 - actual.M14) <= epsilon);

        Assert.True(MathF.Abs(expected.M21 - actual.M21) <= epsilon);
        Assert.True(MathF.Abs(expected.M22 - actual.M22) <= epsilon);
        Assert.True(MathF.Abs(expected.M23 - actual.M23) <= epsilon);
        Assert.True(MathF.Abs(expected.M24 - actual.M24) <= epsilon);

        Assert.True(MathF.Abs(expected.M31 - actual.M31) <= epsilon);
        Assert.True(MathF.Abs(expected.M32 - actual.M32) <= epsilon);
        Assert.True(MathF.Abs(expected.M33 - actual.M33) <= epsilon);
        Assert.True(MathF.Abs(expected.M34 - actual.M34) <= epsilon);

        Assert.True(MathF.Abs(expected.M41 - actual.M41) <= epsilon);
        Assert.True(MathF.Abs(expected.M42 - actual.M42) <= epsilon);
        Assert.True(MathF.Abs(expected.M43 - actual.M43) <= epsilon);
        Assert.True(MathF.Abs(expected.M44 - actual.M44) <= epsilon);
    }
}

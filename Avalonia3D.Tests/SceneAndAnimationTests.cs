using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia3D.Animation;
using Avalonia3D.Loaders;
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
        var level1 = new SceneNode { Name = "Level1", StableId = "node:1" };
        var level2 = new SceneNode { Name = "Level2", StableId = "node:2" };
        var target = new SceneNode { Name = "TargetNode", StableId = "node:3" };

        graph.Root.AddChild(level1);
        level1.AddChild(level2);
        level2.AddChild(target);

        var foundByName = graph.FindNode("TargetNode");
        var foundById = graph.FindNodeByKey("node:3");

        Assert.Same(target, foundByName);
        Assert.Same(target, foundById);
    }

    [Fact]
    public void GltfImporter_ExtractsAnimationClip_FromTestAsset()
    {
        var importer = new GltfSceneImporter();
        var path = GetTestAssetPath("Fox.gltf");

        var result = importer.ImportWithAnimations(path);

        Assert.NotNull(result.Graph);
        Assert.NotEmpty(result.Clips);
        Assert.All(result.Clips, clip => Assert.NotEmpty(clip.Channels));
    }

    [Fact]
    public void AnimationClipPlayer_Update_AppliesChannelValueToNode()
    {
        var graph = new SceneGraph();
        var animatedNode = new SceneNode { Name = "Arm", StableId = "node:arm" };
        graph.Root.AddChild(animatedNode);

        var clip = new AnimationClip("MoveArm");
        var channel = new AnimationChannel("node:arm", AnimationTargetProperty.Position);
        channel.AddKeyframe(0f, new Vector3(0f, 0f, 0f));
        channel.AddKeyframe(1f, new Vector3(10f, 0f, 0f));
        clip.Channels.Add(channel);

        var player = new AnimationClipPlayer(clip, graph);
        player.Play(loop: false, speed: 1f);

        var isRunning = player.Update(0.5f);

        Assert.True(isRunning);
        AssertVectorEqual(new Vector3(5f, 0f, 0f), animatedNode.Position);
    }

    [Fact]
    public void AnimationClipPlayer_RespectsLoopAndNonLoopCompletion()
    {
        var graph = new SceneGraph();
        var node = new SceneNode { Name = "Node", StableId = "node:test" };
        graph.Root.AddChild(node);

        var clip = new AnimationClip("Move");
        var channel = new AnimationChannel("node:test", AnimationTargetProperty.Position);
        channel.AddKeyframe(0f, Vector3.Zero);
        channel.AddKeyframe(1f, new Vector3(1f, 0f, 0f));
        clip.Channels.Add(channel);

        var nonLoop = new AnimationClipPlayer(clip, graph);
        nonLoop.Play(loop: false, speed: 1f);
        var activeAfterEnd = nonLoop.Update(1.1f);

        Assert.False(activeAfterEnd);
        AssertVectorEqual(new Vector3(1f, 0f, 0f), node.Position);

        var loop = new AnimationClipPlayer(clip, graph);
        loop.Play(loop: true, speed: 1f);
        var loopStillActive = loop.Update(1.1f);

        Assert.True(loopStillActive);
        Assert.InRange(node.Position.X, 0.09f, 0.11f);
    }

    [Fact]
    public void AnimationClipPlayer_MissingNode_DoesNotThrow()
    {
        var graph = new SceneGraph();
        var clip = new AnimationClip("NoNodeClip");
        var channel = new AnimationChannel("node:missing", AnimationTargetProperty.Scale);
        channel.AddKeyframe(0f, Vector3.One);
        channel.AddKeyframe(1f, new Vector3(2f, 2f, 2f));
        clip.Channels.Add(channel);

        var player = new AnimationClipPlayer(clip, graph);
        player.Play(loop: false, speed: 1f);

        var exception = Record.Exception(() => player.Update(0.5f));

        Assert.Null(exception);
    }

    private static string GetTestAssetPath(string fileName)
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(current, "Avalonia3D.Sandbox", "Assets", "TestScenes", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Path.GetFullPath(Path.Combine(current, ".."));
        }

        throw new FileNotFoundException($"Test asset {fileName} not found.");
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

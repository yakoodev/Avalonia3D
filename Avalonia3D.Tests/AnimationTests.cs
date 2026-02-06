using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia3D.Animation;
using Avalonia3D.Loaders;
using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class AnimationTests
{
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
        var (graph, node, clip) = CreateSingleNodeClip(duration: 1f);
        var player = new AnimationClipPlayer(clip, graph);

        player.Play(loop: false, speed: 1f);
        var isRunning = player.Update(0.5f);

        Assert.True(isRunning);
        AssertVectorEqual(new Vector3(5f, 0f, 0f), node.Position);
    }

    [Fact]
    public void AnimationClipPlayer_PlayPauseResumeStop_FullLifecycle()
    {
        var (graph, node, clip) = CreateSingleNodeClip(duration: 2f);
        var player = new AnimationClipPlayer(clip, graph);

        player.Play(loop: false, speed: 1f);
        Assert.True(player.Update(0.5f));
        Assert.InRange(node.Position.X, 2.49f, 2.51f);

        player.Pause();
        Assert.True(player.Update(0.5f));
        Assert.InRange(node.Position.X, 2.49f, 2.51f);

        player.Resume();
        Assert.True(player.Update(0.5f));
        Assert.InRange(node.Position.X, 4.99f, 5.01f);

        player.Stop();
        Assert.False(player.Update(0.1f));
        Assert.Equal(0f, player.Time);
    }

    [Fact]
    public void AnimationClipPlayer_Loop_WrapsByDuration()
    {
        var (graph, node, clip) = CreateSingleNodeClip(duration: 1f);
        var player = new AnimationClipPlayer(clip, graph);

        player.Play(loop: true, speed: 1f);
        var isRunning = player.Update(1.2f);

        Assert.True(isRunning);
        Assert.InRange(node.Position.X, 1.99f, 2.01f);
    }

    [Fact]
    public void AnimationClipPlayer_SpeedOverOne_AcceleratesPlayback()
    {
        var (graph, node, clip) = CreateSingleNodeClip(duration: 2f);
        var player = new AnimationClipPlayer(clip, graph);

        player.Play(loop: false, speed: 2f);
        Assert.True(player.Update(0.5f));

        Assert.InRange(node.Position.X, 4.99f, 5.01f);
    }

    [Fact]
    public void AnimationClipPlayer_ZeroDurationClip_CompletesImmediatelyAndAppliesFirstFrame()
    {
        var graph = new SceneGraph();
        var node = new SceneNode { Name = "Arm", StableId = "node:arm" };
        graph.Root.AddChild(node);

        var clip = new AnimationClip("Hold");
        var channel = new AnimationChannel("node:arm", AnimationTargetProperty.Position);
        channel.AddKeyframe(0f, new Vector3(3f, 2f, 1f));
        clip.Channels.Add(channel);

        var completed = false;
        var player = new AnimationClipPlayer(clip, graph, _ => completed = true);
        player.Play(loop: false, speed: 1f);

        var isRunning = player.Update(0.16f);

        Assert.False(isRunning);
        Assert.True(completed);
        AssertVectorEqual(new Vector3(3f, 2f, 1f), node.Position);
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

    private static (SceneGraph Graph, SceneNode Node, AnimationClip Clip) CreateSingleNodeClip(float duration)
    {
        var graph = new SceneGraph();
        var node = new SceneNode { Name = "Arm", StableId = "node:arm" };
        graph.Root.AddChild(node);

        var clip = new AnimationClip("MoveArm");
        var channel = new AnimationChannel("node:arm", AnimationTargetProperty.Position);
        channel.AddKeyframe(0f, new Vector3(0f, 0f, 0f));
        channel.AddKeyframe(duration, new Vector3(10f, 0f, 0f));
        clip.Channels.Add(channel);

        return (graph, node, clip);
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
}

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia3D.Animation;
using Avalonia3D.Loaders;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
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




    [Fact]
    public void AnimationClipPlayer_Update_AppliesMorphWeightsToNode()
    {
        var graph = new SceneGraph();
        var node = new SceneNode { Name = "Eye", StableId = "node:eye" };
        graph.Root.AddChild(node);

        var clip = new AnimationClip("Morph");
        var channel = new AnimationChannel("node:eye", AnimationTargetProperty.MorphWeights);
        channel.AddKeyframe(0f, new float[] { 0f, 1f });
        channel.AddKeyframe(1f, new float[] { 1f, 0f });
        clip.Channels.Add(channel);

        var player = new AnimationClipPlayer(clip, graph);
        player.Play(loop: false, speed: 1f);

        Assert.True(player.Update(0.5f));
        Assert.Equal(2, node.MorphWeights.Length);
        Assert.InRange(node.MorphWeights[0], 0.49f, 0.51f);
        Assert.InRange(node.MorphWeights[1], 0.49f, 0.51f);
    }

    [Fact]
    public void AnimationClipPlayer_Update_AppliesEmissiveColorToMaterial()
    {
        var graph = new SceneGraph();
        var group = new MeshGroup { Name = "Arm" };
        group.Node.StableId = "node:arm";
        graph.AddRoot(group);

        var model = new Avalonia3D.Model.Model
        {
            Name = "mesh",
            Vertices =
            [
                new Vertex { Position = Vector3.Zero }
            ],
            Material = new Material { EmissiveFactor = Vector3.Zero }
        };

        var mesh = new MeshObject { Name = "ArmMesh" };
        mesh.AssignModel(model);
        group.Add(mesh);

        var clip = new AnimationClip("EmissiveColor");
        var channel = new AnimationChannel("node:arm", AnimationTargetProperty.EmissiveColor);
        channel.AddKeyframe(0f, Vector3.Zero);
        channel.AddKeyframe(1f, new Vector3(0.8f, 0.2f, 0.4f));
        clip.Channels.Add(channel);

        var player = new AnimationClipPlayer(clip, graph);
        player.Play(loop: false, speed: 1f);

        Assert.True(player.Update(0.5f));
        AssertVectorEqual(new Vector3(0.4f, 0.1f, 0.2f), mesh.Material!.EmissiveFactor);
    }

    [Fact]
    public void AnimationClipPlayer_Update_AppliesEmissiveIntensityToMaterial()
    {
        var graph = new SceneGraph();
        var group = new MeshGroup { Name = "Arm" };
        group.Node.StableId = "node:arm";
        graph.AddRoot(group);

        var model = new Avalonia3D.Model.Model
        {
            Name = "mesh",
            Vertices =
            [
                new Vertex { Position = Vector3.Zero }
            ],
            Material = new Material { EmissiveIntensity = 1f }
        };

        var mesh = new MeshObject { Name = "ArmMesh" };
        mesh.AssignModel(model);
        group.Add(mesh);

        var clip = new AnimationClip("EmissiveIntensity");
        var channel = new AnimationChannel("node:arm", AnimationTargetProperty.EmissiveIntensity);
        channel.AddKeyframe(0f, 1f);
        channel.AddKeyframe(1f, 3f);
        clip.Channels.Add(channel);

        var player = new AnimationClipPlayer(clip, graph);
        player.Play(loop: false, speed: 1f);

        Assert.True(player.Update(0.5f));
        Assert.InRange(mesh.Material!.EmissiveIntensity, 1.99f, 2.01f);
    }

    [Fact]
    public void AnimatorComponent_RuntimeFallback_CanAnimateMaterialEmissive()
    {
        var graph = new SceneGraph();
        var group = new MeshGroup { Name = "Arm" };
        group.Node.StableId = "node:arm";
        graph.AddRoot(group);

        var model = new Avalonia3D.Model.Model
        {
            Name = "mesh",
            Vertices =
            [
                new Vertex { Position = Vector3.Zero }
            ],
            Material = new Material { EmissiveIntensity = 1f, EmissiveFactor = Vector3.Zero }
        };

        var mesh = new MeshObject();
        mesh.AssignModel(model);
        group.Add(mesh);

        var component = new AnimatorComponent(graph, new Animator());

        Assert.True(component.SetNodeMaterialEmissiveIntensity("node:arm", 2.5f));
        Assert.True(component.SetNodeMaterialEmissiveColor("node:arm", new Vector3(0.1f, 0.3f, 0.5f)));

        Assert.InRange(mesh.Material!.EmissiveIntensity, 2.49f, 2.51f);
        AssertVectorEqual(new Vector3(0.1f, 0.3f, 0.5f), mesh.Material!.EmissiveFactor);
    }

    [Fact]
    public void AnimatorComponent_RegisterAndPlayClip_UpdatesPlaybackStateInvariant()
    {
        var (graph, _, clip) = CreateSingleNodeClip(duration: 1f);
        var animator = new Animator();
        var component = new AnimatorComponent(graph, animator);

        component.RegisterClip(clip);
        var beforePlayState = component.GetClipState(clip.Name);

        Assert.True(beforePlayState.IsRegistered);
        Assert.False(beforePlayState.IsPlaying);
        Assert.Equal(clip.Duration, beforePlayState.Duration);

        var started = component.PlayClip(clip.Name, loop: true, speed: 1.5f);
        var playingState = component.GetClipState(clip.Name);

        Assert.True(started);
        Assert.True(playingState.IsRegistered);
        Assert.True(playingState.IsPlaying);
        Assert.True(playingState.Loop);
        Assert.Equal(1.5f, playingState.Speed);
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

using System;
using System.Numerics;
using Avalonia3D.Animation;
using Avalonia3D.Interaction.Behaviors;
using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class BehaviorIntegrationTests
{
    [Fact]
    public void Scene3D_CommandBus_DispatchesToDoorBehavior()
    {
        var scene = new Scene3D();
        var graph = new SceneGraph();
        graph.Root.AddChild(new SceneNode { SemanticId = "door.main", StableId = "door.main" });
        scene.AnimatorComponent.SetSceneGraph(graph);

        var doorBehavior = new DoorBehavior(
            "door.main",
            openClipName: "door.main.open",
            closeClipName: "door.main.close",
            runtimeFallback: new DoorRuntimeRotationFallback("door.main", DoorNodeTargetKeyMode.SemanticId, Vector3.UnitY, 90f));
        scene.RegisterBehavior(doorBehavior);

        scene.AnimatorComponent.RegisterClip(CreateClip("door.main.open"));
        scene.AnimatorComponent.RegisterClip(CreateClip("door.main.close"));

        var opened = scene.DispatchCommand(SceneCommand.Open("door.main"));

        Assert.True(opened);
        Assert.True(scene.AnimatorComponent.GetClipState("door.main.open").IsPlaying);

        var rotatedZ = Vector3.Transform(Vector3.UnitZ, graph.FindNodeBySemanticId("door.main")!.Rotation);
        Assert.InRange(rotatedZ.Z, 0.99f, 1.01f);
    }

    [Fact]
    public void DoorBehavior_RuntimeFallback_RotatesNode_WhenClipsMissing()
    {
        var scene = new Scene3D();
        var graph = new SceneGraph();
        var doorNode = new SceneNode { SemanticId = "door.main", StableId = "door.main" };
        graph.Root.AddChild(doorNode);
        scene.AnimatorComponent.SetSceneGraph(graph);

        var doorBehavior = new DoorBehavior(
            "door.main",
            openClipName: "door.main.open",
            closeClipName: "door.main.close",
            runtimeFallback: new DoorRuntimeRotationFallback("door.main", DoorNodeTargetKeyMode.SemanticId, Vector3.UnitY, 90f));
        scene.RegisterBehavior(doorBehavior);

        Assert.True(scene.DispatchCommand(SceneCommand.Open("door.main")));

        var rotatedOpen = Vector3.Transform(Vector3.UnitZ, doorNode.Rotation);
        Assert.InRange(rotatedOpen.X, 0.99f, 1.01f);
        Assert.InRange(rotatedOpen.Z, -0.01f, 0.01f);

        Assert.True(scene.DispatchCommand(SceneCommand.Close("door.main")));

        var rotatedClosed = Vector3.Transform(Vector3.UnitZ, doorNode.Rotation);
        Assert.InRange(rotatedClosed.X, -0.01f, 0.01f);
        Assert.InRange(rotatedClosed.Z, 0.99f, 1.01f);
    }

    [Fact]
    public void AnimatorComponent_PlayClip_RaisesClipCompletedEvent()
    {
        var graph = new SceneGraph();
        graph.Root.AddChild(new SceneNode { StableId = "door.main" });

        var animator = new Animator();
        var component = new AnimatorComponent(graph, animator);
        component.RegisterClip(CreateClip("door.main.open"));

        string? completedClip = null;
        component.ClipCompleted += (_, args) => completedClip = args.ClipName;

        Assert.True(component.PlayClip("door.main.open"));
        animator.Update(1f);

        Assert.Equal("door.main.open", completedClip);
    }

    [Fact]
    public void WheelRotationBehavior_CanResolveNodeByName_AndRotateUsingConfiguredAxis()
    {
        var scene = new Scene3D();
        var wheelNode = new SceneNode { Name = "WheelLF", StableId = "node:wheel.lf" };
        scene.SceneGraph.Root.AddChild(wheelNode);

        var behavior = new WheelRotationBehavior("WheelLF", MathF.PI, WheelNodeTargetKeyMode.Name, Vector3.UnitY);
        behavior.Attach(scene);

        Assert.True(behavior.CanHandle(SceneCommand.Open("WheelLF")));
        Assert.True(behavior.Handle(SceneCommand.Open("WheelLF")));

        behavior.Update(1f);

        var rotatedZ = Vector3.Transform(Vector3.UnitZ, wheelNode.Rotation);
        Assert.InRange(rotatedZ.Z, -1.01f, -0.99f);
    }

    [Fact]
    public void WheelRotationBehavior_DefaultConstructor_KeepsSemanticIdAndXAxisBehavior()
    {
        var scene = new Scene3D();
        var wheelNode = new SceneNode { SemanticId = "wheel.front.left", StableId = "node:wheel.lf" };
        scene.SceneGraph.Root.AddChild(wheelNode);

        var behavior = new WheelRotationBehavior("wheel.front.left", MathF.PI);
        behavior.Attach(scene);
        behavior.Handle(SceneCommand.Open("wheel.front.left"));
        behavior.Update(1f);

        var rotatedZ = Vector3.Transform(Vector3.UnitZ, wheelNode.Rotation);
        Assert.InRange(rotatedZ.Z, -1.01f, -0.99f);
    }

    private static AnimationClip CreateClip(string name)
    {
        var clip = new AnimationClip(name);
        var channel = new AnimationChannel("door.main", AnimationTargetProperty.Position);
        channel.AddKeyframe(0f, new System.Numerics.Vector3(0f, 0f, 0f));
        channel.AddKeyframe(0.5f, new System.Numerics.Vector3(1f, 0f, 0f));
        clip.Channels.Add(channel);
        return clip;
    }
}

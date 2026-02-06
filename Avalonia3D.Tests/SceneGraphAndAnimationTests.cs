using System.Numerics;
using Avalonia3D.Animation;
using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

public class SceneGraphAndAnimationTests
{
    [Fact]
    public void CreateModelMatrix_UsesParentTransforms_ForNestedNodes()
    {
        var parent = new SceneNode { Name = "Parent", Position = new Vector3(10f, 0f, 0f) };
        var child = new SceneNode { Name = "Child", Position = new Vector3(0f, 5f, 0f) };
        var grandChild = new SceneNode { Name = "GrandChild", Position = new Vector3(0f, 0f, 2f) };

        parent.AddChild(child);
        child.AddChild(grandChild);

        var worldMatrix = grandChild.CreateModelMatrix();
        var worldPosition = Vector3.Transform(Vector3.Zero, worldMatrix);

        Assert.Equal(10f, worldPosition.X, 3);
        Assert.Equal(5f, worldPosition.Y, 3);
        Assert.Equal(2f, worldPosition.Z, 3);
    }

    [Fact]
    public void FindNode_ReturnsNestedNodeByName()
    {
        var sceneGraph = new SceneGraph();
        var a = new SceneNode { Name = "A" };
        var b = new SceneNode { Name = "B" };
        var target = new SceneNode { Name = "Target" };

        sceneGraph.Root.AddChild(a);
        a.AddChild(b);
        b.AddChild(target);

        var found = sceneGraph.FindNode("Target");

        Assert.Same(target, found);
    }

    [Fact]
    public void AnimationClipPlayer_Update_AppliesClipToTargetNode()
    {
        var sceneGraph = new SceneGraph();
        var animatedNode = new SceneNode { Name = "Arm" };
        sceneGraph.Root.AddChild(animatedNode);

        var clip = new AnimationClip("MoveArm");
        var channel = new AnimationChannel("Arm", AnimationTargetProperty.Position);
        channel.AddKeyframe(0f, new Vector3(0f, 0f, 0f));
        channel.AddKeyframe(2f, new Vector3(10f, 0f, 0f));
        clip.Channels.Add(channel);

        var player = new AnimationClipPlayer(clip, sceneGraph);
        player.Play(loop: false, speed: 1f);

        var stillPlaying = player.Update(1f);

        Assert.True(stillPlaying);
        Assert.Equal(5f, animatedNode.Position.X, 3);
        Assert.Equal(0f, animatedNode.Position.Y, 3);
        Assert.Equal(0f, animatedNode.Position.Z, 3);
    }
}

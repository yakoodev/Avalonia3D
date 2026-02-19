using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using System.Numerics;

namespace Avalonia3D.Tests;

public sealed class AnimationRuntimeControllerTests
{
    [Fact]
    public void RotateCar2Wheels_RotatesOnlyWheelNodes()
    {
        var scene = CreateCar2LikeScene();
        var controller = new AnimationRuntimeController(scene);

        var rotated = controller.RotateCar2Wheels(MathF.PI / 4f);

        Assert.Equal(2, rotated);
        Assert.NotEqual(Quaternion.Identity, scene.SceneGraph.FindNode("front_tire_left")!.Rotation);
        Assert.Equal(Quaternion.Identity, scene.SceneGraph.FindNode("body")!.Rotation);
    }

    [Fact]
    public void ResetCar2Pose_RestoresCapturedTransform()
    {
        var scene = CreateCar2LikeScene();
        var controller = new AnimationRuntimeController(scene);

        controller.CaptureCar2Pose();
        controller.TrySetCar2RootPositionDelta(new Vector3(3f, 0f, 0f));
        controller.RotateCar2Wheels(MathF.PI / 3f);

        var restored = controller.ResetCar2Pose();

        Assert.Equal(3, restored);
        Assert.Equal(Vector3.Zero, scene.SceneGraph.FindNode("carRoot")!.Position);
        Assert.Equal(Quaternion.Identity, scene.SceneGraph.FindNode("front_tire_left")!.Rotation);
    }

    private static Scene3D CreateCar2LikeScene()
    {
        var scene = new Scene3D();
        var root = new SceneNode { Name = "carRoot" };
        root.AddChild(new SceneNode { Name = "front_tire_left" });
        root.AddChild(new SceneNode { Name = "rear_tire_right" });
        root.AddChild(new SceneNode { Name = "body" });
        scene.SceneGraph.Root.AddChild(root);
        return scene;
    }
}

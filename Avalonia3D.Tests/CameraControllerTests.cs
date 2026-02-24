using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Sandbox.Services;
using System.Numerics;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class CameraControllerTests
{
    [Theory]
    [InlineData(10f, 5f, -0.1f, 0.05f)]
    [InlineData(-25f, -10f, 0.25f, -0.1f)]
    [InlineData(0f, 30f, 0f, 0.3f)]
    public void Orbit_TableDriven_ChangesYawAndPitch(float deltaX, float deltaY, float expectedYaw, float expectedPitch)
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera, new SceneGraph()) { OrbitSensitivity = 0.01f };

        controller.Orbit(new Vector2(deltaX, deltaY));

        Assert.InRange(camera.Yaw, expectedYaw - 0.001f, expectedYaw + 0.001f);
        Assert.InRange(camera.Pitch, expectedPitch - 0.001f, expectedPitch + 0.001f);
    }

    [Theory]
    [InlineData(10f, 0f, -1f, 0f)]
    [InlineData(0f, 10f, 0f, 1f)]
    [InlineData(-5f, -5f, 0.5f, -0.5f)]
    public void Pan_TableDriven_MovesTargetInCameraPlane(float deltaX, float deltaY, float expectedX, float expectedY)
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera, new SceneGraph()) { PanSensitivity = 0.1f };

        controller.Pan(new Vector2(deltaX, deltaY));

        Assert.InRange(camera.Target.X, expectedX - 0.01f, expectedX + 0.01f);
        Assert.InRange(camera.Target.Y, expectedY - 0.01f, expectedY + 0.01f);
    }

    [Theory]
    [InlineData(1f, 2f, 8f)]
    [InlineData(-0.5f, 2f, 11f)]
    [InlineData(3f, 1f, 7f)]
    public void Dolly_TableDriven_ChangesDistance(float input, float sensitivity, float expectedDistance)
    {
        var camera = CreateCamera();
        camera.Distance = 10f;
        var controller = new CameraController(camera, new SceneGraph()) { DollySensitivity = sensitivity };

        controller.Dolly(input);

        Assert.InRange(camera.Distance, expectedDistance - 0.01f, expectedDistance + 0.01f);
    }

    [Theory]
    [InlineData(-1f, 1f, 0f)]
    [InlineData(10f, 14f, 12f)]
    [InlineData(-20f, -16f, -18f)]
    public void FrameAll_TableDriven_CentersTargetByBounds(float minX, float maxX, float expectedCenterX)
    {
        var camera = CreateCamera();
        var graph = new SceneGraph();
        graph.AddRoot(CreateMesh(new Vector3(minX, -1f, -1f), new Vector3(maxX, 1f, 1f)));

        var controller = new CameraController(camera, graph);

        var framed = controller.FrameAll();

        Assert.True(framed);
        Assert.InRange(camera.Target.X, expectedCenterX - 0.001f, expectedCenterX + 0.001f);
        Assert.True(camera.Distance > 0f);
    }

    [Fact]
    public void FrameAll_UsesActualSceneGraphFromAccessor()
    {
        var camera = CreateCamera();
        var graphA = new SceneGraph();
        var graphB = new SceneGraph();
        graphB.AddRoot(CreateMesh(new Vector3(10f, -1f, -1f), new Vector3(12f, 1f, 1f)));

        var useSecond = false;
        var controller = new CameraController(camera, () => useSecond ? graphB : graphA);

        var beforeSwitch = controller.FrameAll();
        useSecond = true;
        var afterSwitch = controller.FrameAll();

        Assert.False(beforeSwitch);
        Assert.True(afterSwitch);
        Assert.InRange(camera.Target.X, 10.9f, 11.1f);
    }

    [Fact]
    public void SceneCameraFramer_TryFrame_ReturnsFalseForEmptyScene()
    {
        var camera = CreateCamera();
        var graph = new SceneGraph();

        var result = SceneCameraFramer.TryFrame(graph, camera);

        Assert.False(result);
    }

    [Fact]
    public void ResetView_RestoresCapturedHomeIncludingClipPlanes()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera, new SceneGraph());

        camera.Target = new Vector3(3f, 2f, 1f);
        camera.Distance = 22f;
        camera.Pitch = -0.15f;
        camera.Yaw = 0.44f;
        camera.Near = 0.25f;
        camera.Far = 900f;
        camera.Fov = 0.9f;
        controller.CaptureHomeView();

        controller.Orbit(new Vector2(30f, 10f));
        controller.Dolly(2f);
        camera.Near = 1f;
        camera.Far = 10f;
        camera.Fov = 0.5f;

        controller.ResetView();

        Assert.True(Vector3.Distance(camera.Target, new Vector3(3f, 2f, 1f)) < 0.001f);
        Assert.InRange(camera.Distance, 21.99f, 22.01f);
        Assert.InRange(camera.Pitch, -0.151f, -0.149f);
        Assert.InRange(camera.Yaw, 0.439f, 0.441f);
        Assert.InRange(camera.Near, 0.249f, 0.251f);
        Assert.InRange(camera.Far, 899.9f, 900.1f);
        Assert.InRange(camera.Fov, 0.899f, 0.901f);
    }


    [Fact]
    public void Dolly_WhenZoomingTooClose_ClampsDistanceToPositiveMinimum()
    {
        var camera = CreateCamera();
        camera.Distance = 0.2f;
        var controller = new CameraController(camera, new SceneGraph()) { DollySensitivity = 1f };

        controller.Dolly(10f);

        Assert.True(camera.Distance >= 0.05f);
    }

    [Fact]
    public void Dolly_WhenDistanceShrinks_UpdatesNearClipPlaneToAvoidAggressiveClipping()
    {
        var camera = CreateCamera();
        camera.Distance = 30f;
        camera.Near = 2f;
        var controller = new CameraController(camera, new SceneGraph()) { DollySensitivity = 2f };

        controller.Dolly(10f);

        Assert.True(camera.Distance < 30f);
        Assert.True(camera.Near < 2f);
        Assert.True(camera.Near >= 0.01f);
    }

    [Fact]
    public void Dolly_WhenDistanceShrinks_AllowsFarClipPlaneToShrinkForDepthPrecision()
    {
        var camera = CreateCamera();
        camera.Distance = 40f;
        camera.Far = 2000f;
        var controller = new CameraController(camera, new SceneGraph()) { DollySensitivity = 2f };

        controller.Dolly(10f);

        Assert.True(camera.Distance < 40f);
        Assert.True(camera.Far < 2000f);
        Assert.True(camera.Far > camera.Near);
    }
    private static Camera CreateCamera()
    {
        return new Camera
        {
            Distance = 12f,
            Near = 0.1f,
            Far = 500f,
            Fov = MathF.PI / 4
        };
    }

    private static MeshObject CreateMesh(Vector3 min, Vector3 max)
    {
        var mesh = new MeshObject();
        var model = new Model.Model
        {
            Vertices =
            [
                new Vertex { Position = min },
                new Vertex { Position = max }
            ]
        };

        mesh.AssignModel(model);
        return mesh;
    }
}

using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using System.Numerics;
using Xunit;

namespace Avalonia3D.Tests;

public class CameraControllerTests
{
    [Fact]
    public void Orbit_ChangesYawAndPitch()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera, new SceneGraph())
        {
            OrbitSensitivity = 0.01f
        };

        controller.Orbit(new Vector2(10f, 5f));

        Assert.InRange(camera.Yaw, -0.101f, -0.099f);
        Assert.InRange(camera.Pitch, 0.049f, 0.051f);
    }

    [Fact]
    public void Pan_ShiftsTarget()
    {
        var camera = CreateCamera();
        camera.Distance = 10f;
        var controller = new CameraController(camera, new SceneGraph())
        {
            PanSensitivity = 0.1f
        };

        controller.Pan(new Vector2(10f, 0f));

        Assert.True(camera.Target.X < -0.9f && camera.Target.X > -1.1f);
    }

    [Fact]
    public void Dolly_ChangesDistance()
    {
        var camera = CreateCamera();
        camera.Distance = 10f;
        var controller = new CameraController(camera, new SceneGraph())
        {
            DollySensitivity = 2f
        };

        controller.Dolly(1f);

        Assert.InRange(camera.Distance, 7.99f, 8.01f);
    }

    [Fact]
    public void FrameAll_FitsBoundsFromScene()
    {
        var camera = CreateCamera();
        var graph = new SceneGraph();
        graph.AddRoot(CreateMesh(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f)));

        var controller = new CameraController(camera, graph);

        var result = controller.FrameAll();

        Assert.True(result);
        Assert.True(Vector3.Distance(camera.Target, Vector3.Zero) < 0.001f);
        Assert.True(camera.Distance > 0f);
    }

    [Fact]
    public void ResetView_ReturnsDefaults()
    {
        var camera = CreateCamera();
        camera.Target = new Vector3(2f, 1f, -1f);
        camera.Distance = 11f;
        camera.Pitch = 0.2f;
        camera.Yaw = 0.3f;

        var controller = new CameraController(camera, new SceneGraph());
        controller.Orbit(new Vector2(30f, 10f));
        controller.Pan(new Vector2(20f, 10f));
        controller.Dolly(2f);

        controller.ResetView();

        Assert.True(Vector3.Distance(camera.Target, new Vector3(2f, 1f, -1f)) < 0.001f);
        Assert.InRange(camera.Distance, 10.99f, 11.01f);
        Assert.InRange(camera.Pitch, 0.199f, 0.201f);
        Assert.InRange(camera.Yaw, 0.299f, 0.301f);
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

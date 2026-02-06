using Avalonia3D.Model;
using System;
using System.Numerics;

namespace Avalonia3D.Interaction.CameraController;

public class CameraController
{
    private readonly Camera _camera;
    private readonly SceneGraph _sceneGraph;

    private readonly float _defaultDistance;
    private readonly float _defaultPitch;
    private readonly float _defaultYaw;
    private readonly Vector3 _defaultTarget;

    public CameraController(Camera camera, SceneGraph sceneGraph)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));

        _defaultDistance = camera.Distance;
        _defaultPitch = camera.Pitch;
        _defaultYaw = camera.Yaw;
        _defaultTarget = camera.Target;
    }

    public Camera Camera => _camera;

    public CameraControlMode ControlMode { get; private set; } = CameraControlMode.Orbit;

    public float OrbitSensitivity { get; set; } = 0.01f;
    public float PanSensitivity { get; set; } = 0.01f;
    public float DollySensitivity { get; set; } = 2f;

    public void ToggleControlMode()
    {
        ControlMode = ControlMode == CameraControlMode.Orbit ? CameraControlMode.Pan : CameraControlMode.Orbit;
    }

    public void SetControlMode(CameraControlMode mode)
    {
        ControlMode = mode;
    }

    public void Orbit(Vector2 delta)
    {
        _camera.Yaw -= delta.X * OrbitSensitivity;
        _camera.Pitch += delta.Y * OrbitSensitivity;
    }

    public void Pan(Vector2 delta)
    {
        var cameraDirection = Vector3.Normalize(_camera.Position - _camera.Target);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, cameraDirection));
        var up = Vector3.Normalize(Vector3.Cross(cameraDirection, right));

        _camera.Target -= right * delta.X * PanSensitivity;
        _camera.Target += up * delta.Y * PanSensitivity;
    }

    public void Dolly(float delta)
    {
        _camera.Distance += delta * -DollySensitivity;
    }

    public bool FocusSelection(Vector3 selectionMin, Vector3 selectionMax, float minDistance = 1f)
    {
        return FitBounds(selectionMin, selectionMax, minDistance);
    }

    public bool FrameAll(float minDistance = 3f)
    {
        if (!CameraBoundsCalculator.TryComputeWorldBounds(_sceneGraph, out var min, out var max))
        {
            return false;
        }

        return FitBounds(min, max, minDistance);
    }

    public void ResetView()
    {
        _camera.Target = _defaultTarget;
        _camera.Pitch = _defaultPitch;
        _camera.Yaw = _defaultYaw;
        _camera.Distance = _defaultDistance;
    }

    private bool FitBounds(Vector3 min, Vector3 max, float minDistance)
    {
        var center = (min + max) * 0.5f;
        var extent = (max - min) * 0.5f;
        var radius = MathF.Max(extent.Length(), 0.5f);

        var halfFov = MathF.Max(_camera.Fov * 0.5f, 0.1f);
        var fitDistance = radius / MathF.Tan(halfFov);
        var requiredDistance = MathF.Max(fitDistance * 1.35f, minDistance);

        Camera.DefaultDistance = MathF.Max(Camera.DefaultDistance, requiredDistance * 2f);
        _camera.Target = center;
        _camera.Distance = requiredDistance;
        _camera.Near = MathF.Max(0.01f, _camera.Distance * 0.02f);
        _camera.Far = MathF.Max(_camera.Distance + radius * 4f, _camera.Distance * 3f);
        return true;
    }
}

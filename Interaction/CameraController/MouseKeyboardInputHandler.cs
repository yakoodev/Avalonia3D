using Avalonia.Input;
using Avalonia3D.Interfaces;
using System.Numerics;

namespace Avalonia3D.Interaction.CameraController;

public class MouseKeyboardInputHandler : IInputHandler
{
    private readonly CameraController _cameraController;
    private Vector2? _lastPosition;
    private bool _isPrimaryAction;

    public MouseKeyboardInputHandler(CameraController cameraController)
    {
        _cameraController = cameraController;
    }

    public void OnMouseMove(Vector2 position)
    {
        if (_lastPosition == null)
        {
            _lastPosition = position;
            return;
        }

        var delta = position - _lastPosition.Value;
        _lastPosition = position;

        if (!_isPrimaryAction)
        {
            return;
        }

        if (_cameraController.ControlMode == CameraControlMode.Orbit)
        {
            _cameraController.Orbit(delta);
            return;
        }

        _cameraController.Pan(delta);
    }

    public void OnMouseDown(Vector2 position, MouseButton button)
    {
        if (button is not (MouseButton.Left or MouseButton.Right))
        {
            return;
        }

        if (!_isPrimaryAction)
        {
            _lastPosition = position;
            _isPrimaryAction = true;

            if (button == MouseButton.Right)
            {
                _cameraController.SetControlMode(CameraControlMode.Pan);
                return;
            }

            _cameraController.SetControlMode(CameraControlMode.Orbit);
        }
    }

    public void OnMouseUp(Vector2 position, MouseButton button)
    {
        _isPrimaryAction = false;
        _lastPosition = null;
    }

    public void OnMouseWheel(float delta)
    {
        _cameraController.Dolly(delta);
    }

    public void OnKeyDown(Key key)
    {
        if (key == Key.Tab)
        {
            _cameraController.ToggleControlMode();
        }
    }

    public void OnKeyUp(Key key)
    {
    }
}

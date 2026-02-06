using Avalonia.Input;
using Avalonia3D.Interfaces;
using Serilog;
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
                Log.Debug("Input mode switched to Pan by RMB down");
                return;
            }

            _cameraController.SetControlMode(CameraControlMode.Orbit);
            Log.Debug("Input mode switched to Orbit by LMB down");
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
            Log.Debug("Input mode toggled by Tab. Current={Mode}", _cameraController.ControlMode);
        }
    }

    public void OnKeyUp(Key key)
    {
    }
}

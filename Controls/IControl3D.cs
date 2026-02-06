using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Model;
using System;

namespace Avalonia3D.Controls
{
    public interface IControl3D
    {
        float PanSensitivity { get; set; }
        float RotationSensitivity { get; set; }
        Scene3D Scene { get; }
        float ZoomSensitivity { get; set; }
        CameraController CameraController { get; }

        event Action<WriteableBitmap>? FrameReady;

        void HandlePointerMoved(PointerEventArgs e);
        void HandlePointerPressed(PointerPressedEventArgs e);
        void HandlePointerReleased(PointerReleasedEventArgs e);
        void HandlePointerWheelChanged(PointerWheelEventArgs e);
    }
}

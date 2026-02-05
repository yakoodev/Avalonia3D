using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
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

        event Action<WriteableBitmap>? FrameReady;

        public void HandlePointerMoved(PointerEventArgs e);
        public void HandlePointerPressed(PointerPressedEventArgs e);
        public void HandlePointerReleased(PointerReleasedEventArgs e);
        public void HandlePointerWheelChanged(PointerWheelEventArgs e);
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia3D.Backend;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System;
using System.ComponentModel;
using System.Numerics;
using System.Threading;

namespace Avalonia3D.Controls
{
    public class Model3DEmbeddedControl : Control, IControl3D
    {
        private GL? _gl;
        private readonly GLRenderer3D _renderer = new();
        private readonly IInputHandler _inputHandler;

        public Scene3D Scene => _renderer.Scene;
        public CameraController CameraController { get; }

        private EglGbmOffscreen ctx;
        private Thread _glThread;
        private bool _isRun = true;

        public event Action<WriteableBitmap>? FrameReady
        {
            add => _renderer.FrameReady += value;
            remove => _renderer.FrameReady -= value;
        }

        public Model3DEmbeddedControl()
        {
            CameraController = new CameraController(_renderer.Scene.Camera, () => _renderer.Scene.SceneGraph);
            _inputHandler = new MouseKeyboardInputHandler(CameraController);

            _glThread = new Thread(GLThreadProc)
            {
                IsBackground = true
            };
            _glThread.Start();
        }

        private void GLThreadProc()
        {
            ctx = new EglGbmOffscreen();
            uint w = (uint)Bounds.Width;
            uint h = (uint)Bounds.Height;
            if (w <= 0 || h <= 0)
            {
                w = 800;
                h = 600;
            }

            ctx.Init("/dev/dri/renderD128", w, h);
            _gl = ctx.GL;
            _renderer.Init(_gl);
            ApplySensitivity();

            while (_isRun)
            {
                if (_gl == null) return;

                w = (uint)Bounds.Width;
                h = (uint)Bounds.Height;
                if (w <= 0 || h <= 0) return;
                ctx.Resize(w, h);
                _renderer.Resize(w, h);
                _renderer.RenderFrame((int)w, (int)h);
                Thread.Sleep(40);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isRun = false;
            _renderer.Clear();
            ctx.Dispose();
            base.OnDetachedFromVisualTree(e);
        }

        private float _rotationSensitivity = 0.01f;
        private float _panSensitivity = 0.01f;
        private float _zoomSensitivity = 2f;

        [Category("Interaction")]
        public float RotationSensitivity { get => _rotationSensitivity; set { _rotationSensitivity = value; ApplySensitivity(); } }

        [Category("Interaction")]
        public float PanSensitivity { get => _panSensitivity; set { _panSensitivity = value; ApplySensitivity(); } }

        [Category("Interaction")]
        public float ZoomSensitivity { get => _zoomSensitivity; set { _zoomSensitivity = value; ApplySensitivity(); } }

        public void HandlePointerPressed(PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            var button = e.GetCurrentPoint(this).Properties.IsRightButtonPressed ? MouseButton.Right : MouseButton.Left;
            _inputHandler.OnMouseDown(new Vector2((float)point.X, (float)point.Y), button);
            e.Handled = true;
        }

        public void HandlePointerReleased(PointerReleasedEventArgs e)
        {
            var point = e.GetPosition(this);
            _inputHandler.OnMouseUp(new Vector2((float)point.X, (float)point.Y), e.InitialPressMouseButton);
            e.Handled = true;
        }

        public void HandlePointerMoved(PointerEventArgs e)
        {
            var point = e.GetPosition(this);
            _inputHandler.OnMouseMove(new Vector2((float)point.X, (float)point.Y));
            e.Handled = true;
        }

        public void HandlePointerWheelChanged(PointerWheelEventArgs e)
        {
            _inputHandler.OnMouseWheel((float)e.Delta.Y);
            e.Handled = true;
        }

        private void ApplySensitivity()
        {
            CameraController.OrbitSensitivity = RotationSensitivity;
            CameraController.PanSensitivity = PanSensitivity;
            CameraController.DollySensitivity = ZoomSensitivity;
        }
    }
}

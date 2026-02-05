using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia3D.Backend;
using Avalonia3D.Controls.Avalonia3D.Rendering;
using Avalonia3D.Model;
using Silk.NET.OpenGL;
using System;
using System.ComponentModel;
using System.Threading;

namespace Avalonia3D.Controls
{
    public class Model3DEmbeddedControl : Control, IControl3D
    {
        private GL? _gl;
        private readonly GLRenderer3D _renderer = new();
        public Scene3D Scene { get => _renderer.Scene; }

        private EglGbmOffscreen ctx;

        private Thread _glThread;
        private bool _isRun = true;

        // Событие кадра, проброшенное наружу
        public event Action<WriteableBitmap>? FrameReady
        {
            add => _renderer.FrameReady += value;
            remove => _renderer.FrameReady -= value;
        }

        public Model3DEmbeddedControl()
        {
            _glThread = new Thread(GLThreadProc)
            {
                IsBackground = true
            };
            _glThread.Start();
        }

        private void GLThreadProc()
        {
            // Создание контекста EGL здесь
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

            while (_isRun)
            {
                if (_gl == null) return;

                w = (uint)Bounds.Width;
                h = (uint)Bounds.Height;
                if (w <= 0 || h <= 0) return;
                ctx.Resize(w, h);
                _renderer.Resize(w, h);
                _renderer.RenderFrame((int)w, (int)h); // теперь контекст текущий в этом потоке
                Thread.Sleep(40);
            }
        }  

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _renderer.Clear();
            ctx.Dispose();
            base.OnDetachedFromVisualTree(e);
        }


        [Category("Interaction")]
        public float RotationSensitivity { get; set; } = 0.01f;

        [Category("Interaction")]
        public float PanSensitivity { get; set; } = 0.01f;

        [Category("Interaction")]
        public float ZoomSensitivity { get; set; } = 2f;

        #region Input Handling
        private Point? _lastMousePosition;
        private bool _isRotating;
        private bool _isDragging;
        private object _compositor;

        public void HandlePointerPressed(PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            _lastMousePosition = point;

            var properties = e.GetCurrentPoint(this).Properties;
            _isRotating = properties.IsLeftButtonPressed;
            _isDragging = properties.IsRightButtonPressed;

            if (_isRotating || _isDragging)
                e.Handled = true;
        }

        public void HandlePointerReleased(PointerReleasedEventArgs e)
        {
            _isDragging = false;
            _isRotating = false;
            _lastMousePosition = null;
            e.Handled = true;
        }

        public void HandlePointerMoved(PointerEventArgs e)
        {
            if (_lastMousePosition is not Point lastPoint)
                return;

            var currentPoint = e.GetPosition(this);
            var delta = currentPoint - lastPoint;
            _lastMousePosition = currentPoint;

            if (_isRotating)
            {
                _renderer.Scene.Camera.Rotate(new Vector(delta.X, delta.Y));
                e.Handled = true;
            }
            else if (_isDragging)
            {
                _renderer.Scene.Camera.Pan(delta);
                e.Handled = true;
            }
        }

        public void HandlePointerWheelChanged(PointerWheelEventArgs e)
        {
            _renderer.Scene.Camera.Distance += (float)(e.Delta.Y * -2.0f); // ZoomSensitivity
            e.Handled = true;
        }
        #endregion
    }
}

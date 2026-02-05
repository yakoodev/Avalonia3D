using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia3D.Controls.Avalonia3D.Rendering;
using Avalonia3D.Model;
using Silk.NET.OpenGL;
using System;
using System.ComponentModel;


namespace Avalonia3D.Controls
{
    public class Model3DControl : OpenGlControlBase, IControl3D
    {
        private GL? _gl;
        private readonly GLRenderer3D _renderer = new();
        public Scene3D Scene { get => _renderer.Scene; }

        // Событие кадра, проброшенное наружу
        public event Action<WriteableBitmap>? FrameReady
        {
            add => _renderer.FrameReady += value;
            remove => _renderer.FrameReady -= value;
        }

        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);
            _gl = GL.GetApi(gl.GetProcAddress);
            _renderer.Init(_gl);
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (_gl == null) return;

            int w = (int)Bounds.Width;
            int h = (int)Bounds.Height;
            if (w <= 0 || h <= 0) return;

            _renderer.Resize((uint)w, (uint)h);
            _renderer.RenderFrame(w, h);

            RequestNextFrameRendering();
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            _renderer.Clear();
            _gl = null;
            base.OnOpenGlDeinit(gl);
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
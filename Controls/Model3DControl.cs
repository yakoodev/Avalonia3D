using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia3D.Composition;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Avalonia3D.Controls
{
    public class Model3DControl : OpenGlControlBase, IControl3D
    {
        private GL? _gl;
        private readonly GLRenderer3D _renderer;
        private readonly IInputHandler _inputHandler;

        public Model3DControl()
            : this(null)
        {
        }

        public Model3DControl(ISceneBootstrap? sceneBootstrap)
        {
            _renderer = new GLRenderer3D(sceneBootstrap);
            CameraController = new CameraController(_renderer.Scene.Camera, () => _renderer.Scene.SceneGraph);
            _inputHandler = new MouseKeyboardInputHandler(CameraController);
        }

        public Scene3D Scene => _renderer.Scene;
        public CameraController CameraController { get; }

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
            ApplySensitivity();
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (_gl == null) return;

            int w = (int)Bounds.Width;
            int h = (int)Bounds.Height;
            if (w <= 0 || h <= 0) return;

            _renderer.FrameState.OutputFramebufferId = (uint)Math.Max(0, fb);
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

        private float _rotationSensitivity = 0.01f;
        private float _panSensitivity = 0.01f;
        private float _zoomSensitivity = 2f;

        [Category("Interaction")]
        public float RotationSensitivity
        {
            get => _rotationSensitivity;
            set
            {
                _rotationSensitivity = value;
                ApplySensitivity();
            }
        }

        [Category("Interaction")]
        public float PanSensitivity
        {
            get => _panSensitivity;
            set
            {
                _panSensitivity = value;
                ApplySensitivity();
            }
        }

        [Category("Interaction")]
        public float ZoomSensitivity
        {
            get => _zoomSensitivity;
            set
            {
                _zoomSensitivity = value;
                ApplySensitivity();
            }
        }

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

using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Rendering;
using Avalonia3D.Sandbox.Services;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Avalonia3D.Sandbox;

public class SandboxModel3DControl : OpenGlControlBase
{
    private GL? _gl;
    private readonly SandboxRenderer3D _renderer = new();
    private readonly RenderThreadScheduler _renderThreadScheduler = new();
    private readonly IInputHandler _inputHandler;

    public SandboxModel3DControl()
    {
        CameraController = new CameraController(_renderer.Scene.Camera, _renderer.Scene.SceneGraph);
        _inputHandler = new MouseKeyboardInputHandler(CameraController);
        _renderer.RendererInitialized += () => RendererInitialized?.Invoke(this, EventArgs.Empty);
    }

    public Scene3D Scene => _renderer.Scene;
    public CameraController CameraController { get; }

    public event EventHandler? RendererInitialized;

    public IRenderThreadScheduler RenderThreadScheduler => _renderThreadScheduler;

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

        int width = (int)Bounds.Width;
        int height = (int)Bounds.Height;
        if (width <= 0 || height <= 0) return;

        if (_lastFramebuffer != fb)
        {
            _lastFramebuffer = fb;
            Log.Information("Sandbox OpenGL target framebuffer: {FramebufferId}", fb);
        }

        _renderer.FrameState.OutputFramebufferId = (uint)Math.Max(0, fb);
        _renderThreadScheduler.ExecutePending();
        _renderer.Resize((uint)width, (uint)height);
        _renderer.RenderFrame(width, height);
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
    public float RotationSensitivity { get => _rotationSensitivity; set { _rotationSensitivity = value; ApplySensitivity(); } }

    [Category("Interaction")]
    public float PanSensitivity { get => _panSensitivity; set { _panSensitivity = value; ApplySensitivity(); } }

    [Category("Interaction")]
    public float ZoomSensitivity { get => _zoomSensitivity; set { _zoomSensitivity = value; ApplySensitivity(); } }

    private int _lastFramebuffer = -1;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        var button = e.GetCurrentPoint(this).Properties.IsRightButtonPressed ? MouseButton.Right : MouseButton.Left;
        _inputHandler.OnMouseDown(new Vector2((float)point.X, (float)point.Y), button);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        _inputHandler.OnMouseUp(new Vector2((float)point.X, (float)point.Y), e.InitialPressMouseButton);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        _inputHandler.OnMouseMove(new Vector2((float)point.X, (float)point.Y));
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _inputHandler.OnMouseWheel((float)e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _inputHandler.OnKeyDown(e.Key);
    }

    private void ApplySensitivity()
    {
        CameraController.OrbitSensitivity = RotationSensitivity;
        CameraController.PanSensitivity = PanSensitivity;
        CameraController.DollySensitivity = ZoomSensitivity;
    }
}

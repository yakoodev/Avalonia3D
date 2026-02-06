using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
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
    private bool _isPointerDragActive;
    private MouseButton _activeMouseButton = MouseButton.None;

    public SandboxModel3DControl()
    {
        Focusable = true;
        IsHitTestVisible = true;

        CameraController = new CameraController(_renderer.Scene.Camera, () => _renderer.Scene.SceneGraph);
        _inputHandler = new MouseKeyboardInputHandler(CameraController);
        _renderer.RendererInitialized += () => RendererInitialized?.Invoke(this, EventArgs.Empty);
    }

    public Scene3D Scene => _renderer.Scene;
    public CameraController CameraController { get; }

    public event EventHandler? RendererInitialized;

    public bool IsRendererInitialized { get; private set; }

    public IRenderThreadScheduler RenderThreadScheduler => _renderThreadScheduler;


    public void ApplyRenderQuality(RenderQualitySettings settings)
    {
        _renderThreadScheduler.Enqueue(() => _renderer.SetRenderQuality(settings));
    }

    public RenderQualitySettings GetRenderQuality() => _renderer.RenderQualitySettings;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Log.Information("SandboxModel3DControl attached. Focusable={Focusable}, HitTest={HitTest}, Bounds={Bounds}", Focusable, IsHitTestVisible, Bounds);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Log.Information("SandboxModel3DControl detached");
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _gl = GL.GetApi(gl.GetProcAddress);
        _renderer.Init(_gl);
        IsRendererInitialized = true;
        ApplySensitivity();
        Log.Information("SandboxModel3DControl initialized. Bounds={Bounds}", Bounds);
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
        IsRendererInitialized = false;
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
        HandlePointerPressed(e);
    }

    public void HandlePointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);
        _activeMouseButton = ResolveMouseButton(e);

        Log.Information("PointerPressed at {Point}. Button={Button}, Kind={Kind}", point, _activeMouseButton, e.GetCurrentPoint(this).Properties.PointerUpdateKind);

        if (_activeMouseButton == MouseButton.None)
        {
            return;
        }

        _isPointerDragActive = true;
        _inputHandler.OnMouseDown(new Vector2((float)point.X, (float)point.Y), _activeMouseButton);
        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        HandlePointerReleased(e);
    }

    public void HandlePointerReleased(PointerReleasedEventArgs e)
    {
        var point = e.GetPosition(this);
        var releasedButton = e.InitialPressMouseButton == MouseButton.None ? _activeMouseButton : e.InitialPressMouseButton;

        Log.Information("PointerReleased at {Point}. Button={Button}", point, releasedButton);

        _inputHandler.OnMouseUp(new Vector2((float)point.X, (float)point.Y), releasedButton);
        _isPointerDragActive = false;
        _activeMouseButton = MouseButton.None;

        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isPointerDragActive = false;
        _activeMouseButton = MouseButton.None;
        Log.Information("PointerCaptureLost");
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Log.Information("PointerEntered SandboxModel3DControl");
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        HandlePointerMoved(e);
    }

    public void HandlePointerMoved(PointerEventArgs e)
    {
        var point = e.GetPosition(this);

        if (_isPointerDragActive)
        {
            _inputHandler.OnMouseMove(new Vector2((float)point.X, (float)point.Y));
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        HandlePointerWheelChanged(e);
    }

    public void HandlePointerWheelChanged(PointerWheelEventArgs e)
    {
        _inputHandler.OnMouseWheel((float)e.Delta.Y);
        Log.Information("PointerWheel delta={Delta}", e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _inputHandler.OnKeyDown(e.Key);
    }

    private MouseButton ResolveMouseButton(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        return props.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
            PointerUpdateKind.RightButtonPressed => MouseButton.Right,
            _ when props.IsRightButtonPressed => MouseButton.Right,
            _ when props.IsLeftButtonPressed => MouseButton.Left,
            _ => MouseButton.None
        };
    }

    private void ApplySensitivity()
    {
        CameraController.OrbitSensitivity = RotationSensitivity;
        CameraController.PanSensitivity = PanSensitivity;
        CameraController.DollySensitivity = ZoomSensitivity;
    }
}

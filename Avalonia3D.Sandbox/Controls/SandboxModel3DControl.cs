using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Rendering;
using Avalonia3D.Sandbox.Services;
using Silk.NET.OpenGL;
using System;
using System.ComponentModel;

namespace Avalonia3D.Sandbox;

public class SandboxModel3DControl : OpenGlControlBase
{
    private GL? _gl;
    private readonly SandboxRenderer3D _renderer = new();
    private readonly RenderThreadScheduler _renderThreadScheduler = new();

    public SandboxModel3DControl()
    {
        _renderer.RendererInitialized += () => RendererInitialized?.Invoke(this, EventArgs.Empty);
    }

    public Scene3D Scene => _renderer.Scene;

    public event EventHandler? RendererInitialized;

    public IRenderThreadScheduler RenderThreadScheduler => _renderThreadScheduler;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _gl = GL.GetApi(gl.GetProcAddress);
        _renderer.Init(_gl);
        Scene.Camera.RotationSensitivity = RotationSensitivity;
        Scene.Camera.PanSensitivity = PanSensitivity;
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl == null) return;

        int width = (int)Bounds.Width;
        int height = (int)Bounds.Height;
        if (width <= 0 || height <= 0) return;

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

    [Category("Interaction")]
    public float RotationSensitivity { get; set; } = 0.01f;

    [Category("Interaction")]
    public float PanSensitivity { get; set; } = 0.01f;

    [Category("Interaction")]
    public float ZoomSensitivity { get; set; } = 2f;

    private Point? _lastMousePosition;
    private bool _isRotating;
    private bool _isDragging;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        _lastMousePosition = point;

        var properties = e.GetCurrentPoint(this).Properties;
        _isRotating = properties.IsLeftButtonPressed;
        _isDragging = properties.IsRightButtonPressed;

        if (_isRotating || _isDragging)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
        _isRotating = false;
        _lastMousePosition = null;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastMousePosition is not Point lastPoint)
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - lastPoint;
        _lastMousePosition = currentPoint;

        if (_isRotating)
        {
            Scene.Camera.Rotate(new Vector(delta.X, delta.Y));
            e.Handled = true;
        }
        else if (_isDragging)
        {
            Scene.Camera.Pan(delta);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Scene.Camera.Distance += (float)(e.Delta.Y * -ZoomSensitivity);
        e.Handled = true;
    }
}

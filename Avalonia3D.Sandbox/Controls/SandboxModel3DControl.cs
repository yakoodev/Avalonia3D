using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Avalonia3D.Sandbox.Rendering;
using Avalonia3D.Sandbox.Scenes;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox.Utilities;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Windows.Input;

namespace Avalonia3D.Sandbox;

public class SandboxModel3DControl : OpenGlControlBase
{
    public static readonly StyledProperty<string> SceneSourceProperty =
        AvaloniaProperty.Register<SandboxModel3DControl, string>(nameof(SceneSource), string.Empty);

    public static readonly StyledProperty<string?> SelectedSceneIdProperty =
        AvaloniaProperty.Register<SandboxModel3DControl, string?>(nameof(SelectedSceneId));

    public static readonly DirectProperty<SandboxModel3DControl, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<SandboxModel3DControl, bool>(nameof(IsLoading), c => c.IsLoading);

    public static readonly DirectProperty<SandboxModel3DControl, string?> LastLoadErrorProperty =
        AvaloniaProperty.RegisterDirect<SandboxModel3DControl, string?>(nameof(LastLoadError), c => c.LastLoadError);

    public static readonly DirectProperty<SandboxModel3DControl, bool> IsRendererReadyProperty =
        AvaloniaProperty.RegisterDirect<SandboxModel3DControl, bool>(nameof(IsRendererReady), c => c.IsRendererReady);

    public static readonly DirectProperty<SandboxModel3DControl, ICommand> LoadSceneCommandProperty =
        AvaloniaProperty.RegisterDirect<SandboxModel3DControl, ICommand>(nameof(LoadSceneCommand), c => c.LoadSceneCommand);

    public static readonly DirectProperty<SandboxModel3DControl, ICommand> FrameSceneCommandProperty =
        AvaloniaProperty.RegisterDirect<SandboxModel3DControl, ICommand>(nameof(FrameSceneCommand), c => c.FrameSceneCommand);

    public static readonly DirectProperty<SandboxModel3DControl, ICommand> ResetCameraCommandProperty =
        AvaloniaProperty.RegisterDirect<SandboxModel3DControl, ICommand>(nameof(ResetCameraCommand), c => c.ResetCameraCommand);

    private GL? _gl;
    private readonly SandboxRenderer3D _renderer = new();
    private readonly RenderThreadScheduler _renderThreadScheduler = new();
    private readonly IInputHandler _inputHandler;
    private readonly SceneLoader _sceneLoader;
    private readonly Dictionary<string, ISandboxScene> _sceneIndex = new(StringComparer.OrdinalIgnoreCase);
    private bool _isPointerDragActive;
    private MouseButton _activeMouseButton = MouseButton.None;
    private bool _isLoading;
    private string? _lastLoadError;
    private bool _isRendererReady;

    public SandboxModel3DControl()
    {
        Focusable = true;
        IsHitTestVisible = true;

        CameraController = new CameraController(_renderer.Scene.Camera, () => _renderer.Scene.SceneGraph);
        _inputHandler = new MouseKeyboardInputHandler(CameraController);
        _sceneLoader = new SceneLoader(_renderer.Scene, DefaultSceneSource, _renderThreadScheduler);
        _sceneLoader.SceneChanged += scene =>
        {
            SetAndRaise(IsLoadingProperty, ref _isLoading, false);
            SelectedSceneId = scene.Id;
            SetAndRaise(LastLoadErrorProperty, ref _lastLoadError, null);
        };

        LoadSceneCommand = new RelayCommand(parameter => RequestSceneLoad(parameter as string));
        FrameSceneCommand = new RelayCommand(_ => _renderThreadScheduler.Enqueue(() => CameraController.FrameAll()));
        ResetCameraCommand = new RelayCommand(_ => _renderThreadScheduler.Enqueue(() => CameraController.ResetView()));

        _renderer.RendererInitialized += () => RendererInitialized?.Invoke(this, EventArgs.Empty);

        SceneSource = DefaultSceneSource;

        BuildSceneIndex(SceneSource);
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SceneSourceProperty)
        {
            var source = change.NewValue as string;
            if (!string.IsNullOrWhiteSpace(source))
            {
                BuildSceneIndex(source);
            }
        }
        else if (change.Property == SelectedSceneIdProperty)
        {
            var selectedId = change.NewValue as string;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                RequestSceneLoad(selectedId);
            }
        }
    }

    private static string DefaultSceneSource => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "TestScenes");

    public Scene3D Scene => _renderer.Scene;
    public CameraController CameraController { get; }

    public event EventHandler? RendererInitialized;

    public bool IsRendererInitialized => _isRendererReady;

    public bool IsLoading => _isLoading;

    public string? LastLoadError => _lastLoadError;

    public bool IsRendererReady => _isRendererReady;

    public ICommand LoadSceneCommand { get; }

    public ICommand FrameSceneCommand { get; }

    public ICommand ResetCameraCommand { get; }

    public IRenderThreadScheduler RenderThreadScheduler => _renderThreadScheduler;

    public string SceneSource
    {
        get => GetValue(SceneSourceProperty);
        set => SetValue(SceneSourceProperty, value);
    }

    public string? SelectedSceneId
    {
        get => GetValue(SelectedSceneIdProperty);
        set => SetValue(SelectedSceneIdProperty, value);
    }

    public void ApplyGraphicsProfile(GraphicsProfile profile)
    {
        _renderThreadScheduler.Enqueue(() => _renderer.SetGraphicsProfile(profile));
    }

    public GraphicsProfile GetGraphicsProfile() => _renderer.GraphicsProfile;
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
        SetAndRaise(IsRendererReadyProperty, ref _isRendererReady, true);
        _sceneLoader.MarkRendererReady();
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
        SetAndRaise(IsRendererReadyProperty, ref _isRendererReady, false);
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

    public void HandlePointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);
        _activeMouseButton = ResolveMouseButton(e);

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

    public void HandlePointerReleased(PointerReleasedEventArgs e)
    {
        var point = e.GetPosition(this);
        var releasedButton = e.InitialPressMouseButton == MouseButton.None ? _activeMouseButton : e.InitialPressMouseButton;

        _inputHandler.OnMouseUp(new Vector2((float)point.X, (float)point.Y), releasedButton);
        _isPointerDragActive = false;
        _activeMouseButton = MouseButton.None;

        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        e.Handled = true;
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

    public void HandlePointerWheelChanged(PointerWheelEventArgs e)
    {
        _inputHandler.OnMouseWheel((float)e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        HandlePointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        HandlePointerReleased(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isPointerDragActive = false;
        _activeMouseButton = MouseButton.None;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        HandlePointerMoved(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        HandlePointerWheelChanged(e);
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

    private void BuildSceneIndex(string sceneSource)
    {
        _sceneIndex.Clear();

        foreach (var scene in SceneCatalog.CreateDefault(sceneSource))
        {
            _sceneIndex[scene.Id] = scene;
        }

        if (SelectedSceneId == null)
        {
            SelectedSceneId = _sceneIndex.Keys.FirstOrDefault();
        }
    }

    private void RequestSceneLoad(string? sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
        {
            return;
        }

        if (!_sceneIndex.TryGetValue(sceneId, out var scene))
        {
            SetAndRaise(LastLoadErrorProperty, ref _lastLoadError, $"Scene '{sceneId}' not found.");
            return;
        }

        try
        {
            SetAndRaise(IsLoadingProperty, ref _isLoading, true);
            SetAndRaise(LastLoadErrorProperty, ref _lastLoadError, null);
            _sceneLoader.Load(scene);
        }
        catch (Exception ex)
        {
            SetAndRaise(IsLoadingProperty, ref _isLoading, false);
            SetAndRaise(LastLoadErrorProperty, ref _lastLoadError, ex.Message);
            Log.Error(ex, "Failed to load scene {SceneId}", sceneId);
        }
    }
}

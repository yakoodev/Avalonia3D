using Avalonia.Media.Imaging;
using Avalonia3D.Composition;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;
using Serilog;
using System;
using System.Runtime.InteropServices;

namespace Avalonia3D.Sandbox.Rendering;

public sealed class SandboxRenderer3D : IRenderContext
{
    private const bool UseSeparateEmissiveTarget = false;
    private static readonly TimeSpan MetricsLogInterval = TimeSpan.FromSeconds(1);

    private GL? _gl;
    private IFramePresenter? _framePresenter;
    private Action<WriteableBitmap>? _frameReadyHandlers;
    private readonly RenderPipeline _renderPipeline = new(GraphicsProfile.Medium);
    private readonly EmissiveRenderTargetManager _emissiveTargetManager = new();
    private DateTime _nextMetricsLogUtc = DateTime.MinValue;
    private int _lastLoggedDrawCalls = -1;
    private int _lastLoggedCulledObjects = -1;

    public GL? GL => _gl;
    public Scene3D Scene { get; } = new();
    public RenderFrameState FrameState { get; } = new();

    public event Action<WriteableBitmap>? FrameReady
    {
        add
        {
            _frameReadyHandlers += value;
            EnsureFramePresenter();
        }
        remove
        {
            _frameReadyHandlers -= value;
            if (_frameReadyHandlers == null)
            {
                DisposeFramePresenter();
            }
        }
    }
    public event Action? RendererInitialized;

    public GraphicsProfile GraphicsProfile => _renderPipeline.Profile;
    public RenderQualitySettings RenderQualitySettings => _renderPipeline.Settings;

    public void SetGraphicsProfile(GraphicsProfile profile)
    {
        _renderPipeline.ApplyProfile(profile);
        Scene.ApplyGraphicsProfile(_renderPipeline.Profile);
    }

    public void Init(GL gl)
    {
        _gl = gl;
        Scene.Init(gl);
        Scene.ApplyGraphicsProfile(_renderPipeline.Profile);
        SceneShaderRegistryBootstrap.Configure(Scene, _renderPipeline.Profile.MaxLights);
        ConfigureOpenGLState();
        InitializeCameraDefaults();
        EnsureFramePresenter();
        LogOpenGlInfo();
        RendererInitialized?.Invoke();
    }

    public void Resize(uint width, uint height)
    {
        if (_gl == null) return;
        _gl.Viewport(0, 0, width, height);
        Scene.Camera.Width = (int)width;
        Scene.Camera.Height = (int)height;
        _framePresenter?.Resize((int)width, (int)height);
        if (UseSeparateEmissiveTarget)
        {
            _emissiveTargetManager.Ensure(_gl, FrameState, (int)width, (int)height);
        }
        else
        {
            FrameState.EmissiveFramebufferId = 0;
            FrameState.EmissiveTextureId = 0;
        }
    }

    public void RenderFrame(int width, int height)
    {
        if (_gl == null) return;
        FrameState.PbrDebugViewMode = Scene.PbrDebugViewMode;
        _renderPipeline.Execute(this, width, height);
        LogFrameMetricsIfNeeded();
        _framePresenter?.Present(_gl, width, height);
    }

    public void Clear()
    {
        Scene.Clear();
        DisposeFramePresenter();
        if (_gl != null)
        {
            _emissiveTargetManager.Release(_gl);
        }
        FrameState.ResetForwardTargets();
    }

    public void ReleaseContextResources()
    {
        DisposeFramePresenter();

        if (_gl != null)
        {
            _emissiveTargetManager.Release(_gl);
        }

        FrameState.ResetForwardTargets();
        Scene.OnContextLost();
        _gl = null;
    }

    private void EnsureFramePresenter()
    {
        if (_frameReadyHandlers == null || _framePresenter != null)
        {
            return;
        }

        _framePresenter = new PboFramePresenter();
        _framePresenter.FrameReady += OnFrameReady;
    }

    private void DisposeFramePresenter()
    {
        if (_framePresenter is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _framePresenter = null;
    }

    private void OnFrameReady(WriteableBitmap bitmap)
    {
        _frameReadyHandlers?.Invoke(bitmap);
    }

    private void ConfigureOpenGLState()
    {
        if (_gl == null) return;
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.Disable(EnableCap.CullFace);
        _gl.CullFace(GLEnum.Back);
        _gl.FrontFace(FrontFaceDirection.Ccw);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
        _gl.ColorMask(true, true, true, true);
    }

    private void InitializeCameraDefaults()
    {
        Scene.Camera.Distance = 12f;
        Scene.Camera.Pitch = -0.3f;
        Scene.Camera.Yaw = 0.6f;
        Scene.Camera.Fov = MathF.PI / 4;
        Scene.Camera.Near = 0.1f;
        Scene.Camera.Far = 200f;
    }

    private unsafe void LogOpenGlInfo()
    {
        if (_gl == null)
        {
            return;
        }

        var version = Marshal.PtrToStringAnsi((nint)_gl.GetString(GLEnum.Version)) ?? "<unknown>";
        var renderer = Marshal.PtrToStringAnsi((nint)_gl.GetString(GLEnum.Renderer)) ?? "<unknown>";
        var vendor = Marshal.PtrToStringAnsi((nint)_gl.GetString(GLEnum.Vendor)) ?? "<unknown>";
        Log.Information("OpenGL init. Vendor: {Vendor}, Renderer: {Renderer}, Version: {Version}", vendor, renderer, version);
    }

    private void LogFrameMetricsIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now < _nextMetricsLogUtc)
        {
            return;
        }

        var drawCalls = FrameState.Metrics.DrawCalls;
        var culledObjects = FrameState.Metrics.CulledObjects;
        if (drawCalls == _lastLoggedDrawCalls && culledObjects == _lastLoggedCulledObjects)
        {
            _nextMetricsLogUtc = now + MetricsLogInterval;
            return;
        }

        _lastLoggedDrawCalls = drawCalls;
        _lastLoggedCulledObjects = culledObjects;
        _nextMetricsLogUtc = now + MetricsLogInterval;
        Log.Information("Frame metrics: drawCalls={DrawCalls}, culled={Culled}", drawCalls, culledObjects);
    }

}

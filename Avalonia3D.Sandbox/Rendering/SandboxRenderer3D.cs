using Avalonia.Media.Imaging;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Sandbox.Rendering;

public sealed class SandboxRenderer3D : IRenderContext
{
    private GL? _gl;
    private IFramePresenter? _framePresenter;
    private readonly RenderPipeline _renderPipeline = new();

    public GL? GL => _gl;
    public Scene3D Scene { get; } = new();
    public RenderFrameState FrameState { get; } = new();

    public event Action<WriteableBitmap>? FrameReady;
    public event Action? RendererInitialized;

    public void Init(GL gl)
    {
        _gl = gl;
        Scene.Init(gl);
        Scene.Shaders.Add(GLShader.Create(gl));
        ConfigureOpenGLState();
        InitializeCameraDefaults();
        InitializeFramePresenter();
        RendererInitialized?.Invoke();
    }

    public void Resize(uint width, uint height)
    {
        if (_gl == null) return;
        _gl.Viewport(0, 0, width, height);
        Scene.Camera.Width = (int)width;
        Scene.Camera.Height = (int)height;
        _framePresenter?.Resize((int)width, (int)height);
    }

    public void RenderFrame(int width, int height)
    {
        if (_gl == null) return;
        _renderPipeline.Execute(this, width, height);
        _framePresenter?.Present(_gl, width, height);
    }

    public void Clear()
    {
        Scene.Clear();
        if (_framePresenter is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _framePresenter = null;
    }

    private void InitializeFramePresenter()
    {
        _framePresenter = new PboFramePresenter();
        _framePresenter.FrameReady += bitmap => FrameReady?.Invoke(bitmap);
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
}

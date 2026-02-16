using Avalonia.Media.Imaging;
using Avalonia3D.Composition;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Serilog;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Controls
{
    public class GLRenderer3D : IRenderContext
    {
        private GL? _gl;
        private IFramePresenter? _framePresenter;
        private readonly RenderPipeline _renderPipeline = new();
        private readonly EmissiveRenderTargetManager _emissiveTargetManager = new();
        private readonly ISceneBootstrap _sceneBootstrap;
        private bool _loggedOpenGlErrorDrain;

        public Scene3D Scene { get; } = new();

        public GL? GL => _gl;
        public RenderFrameState FrameState { get; } = new();

        public event Action<WriteableBitmap>? FrameReady;

        public GLRenderer3D(ISceneBootstrap? sceneBootstrap = null)
        {
            _sceneBootstrap = sceneBootstrap ?? DefaultSceneBootstrap.Instance;
        }

        public void Init(GL gl)
        {
            _gl = gl;
            Scene.Init(gl);
            _sceneBootstrap.Bootstrap(Scene, _renderPipeline.Profile);
            ConfigureOpenGLState();
            InitializeFramePresenter();
        }

        public void Resize(uint width, uint height)
        {
            if (_gl == null) return;
            _gl.Viewport(0, 0, width, height);
            Scene.Camera.Width = (int)width;
            Scene.Camera.Height = (int)height;
            _framePresenter?.Resize((int)width, (int)height);
            _emissiveTargetManager.Ensure(_gl, FrameState, (int)width, (int)height);
        }

        public void RenderFrame(int w, int h)
        {
            if (_gl == null) return;

            _renderPipeline.Execute(this, w, h);

            _framePresenter?.Present(_gl, w, h);
            DrainResidualErrors(_gl);
        }

        public void Clear()
        {
            Scene.Clear();
            if (_framePresenter is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _framePresenter = null;
            if (_gl != null)
            {
            _emissiveTargetManager.Release(_gl);
            }
            FrameState.ResetForwardTargets();
        }

        private void DrainResidualErrors(GL gl)
        {
            var drained = false;
            GLEnum lastError = GLEnum.NoError;

            for (var i = 0; i < 16; i++)
            {
                var error = gl.GetError();
                if (error == GLEnum.NoError)
                {
                    break;
                }

                drained = true;
                lastError = error;
            }

            if (drained && !_loggedOpenGlErrorDrain)
            {
                _loggedOpenGlErrorDrain = true;
                Log.Warning("Drained residual OpenGL errors before returning control to Avalonia. LastError={LastError}", lastError);
            }
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
    }
}

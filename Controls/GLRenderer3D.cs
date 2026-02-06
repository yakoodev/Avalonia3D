using Avalonia.Media.Imaging;
using Avalonia3D.Interfaces;
using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Plugins.Wheel;
using Avalonia3D.Rendering;
using Avalonia3D.Shaders;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Controls
{
    public class GLRenderer3D : IRenderContext
    {
        private GL? _gl;
        private IFramePresenter? _framePresenter;
        private readonly RenderPipeline _renderPipeline = new();

        public Scene3D Scene { get; } = new();
        private readonly WheelSceneModule _wheelModule = new();

        public GL? GL => _gl;
        public RenderFrameState FrameState { get; } = new();

        public event Action<WriteableBitmap>? FrameReady;

        public void Init(GL gl)
        {
            _gl = gl;
            Scene.Init(gl);
            Scene.ShaderRegistry.Register(ShaderIds.Pbr, GLShader.Create);
            Scene.ShaderRegistry.Register(ShaderIds.PbrBaseColor, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap));
            Scene.ShaderRegistry.Register(ShaderIds.PbrBaseColorNormal, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap));
            Scene.ShaderRegistry.Register(ShaderIds.PbrBaseColorNormalMetallicRoughness, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap));
            Scene.ShaderRegistry.Register(ShaderIds.PbrBaseColorNormalMetallicRoughnessAoEmissive, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap));
            Scene.ShaderRegistry.Register(ShaderIds.PbrFull, glContext => GLShader.Create(glContext, PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap | PbrFeatures.ReflectionsIbl));
            Scene.ShaderRegistry.Register(ShaderIds.Unlit, UnlitShader.Create);
            Scene.ShaderRegistry.Register(ShaderIds.NormalsDebug, NormalsDebugShader.Create);
            Scene.ShaderRegistry.SetDefault(ShaderIds.Pbr);
            Scene.ActiveShaderId = ShaderIds.Pbr;
            Scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
            Scene.BindRenderMode(ShaderRenderMode.Unlit, ShaderIds.Unlit);
            Scene.BindRenderMode(ShaderRenderMode.NormalsDebug, ShaderIds.NormalsDebug);
            Scene.RegisterModule(_wheelModule);
            _wheelModule.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "gltf"), Scene.Importer);
            ConfigureOpenGLState();
            InitializeCamera();
            InitializeFramePresenter();
        }

        public void Resize(uint width, uint height)
        {
            if (_gl == null) return;
            _gl.Viewport(0, 0, width, height);
            Scene.Camera.Width = (int)width;
            Scene.Camera.Height = (int)height;
            _framePresenter?.Resize((int)width, (int)height);
        }

        public void RenderFrame(int w, int h)
        {
            if (_gl == null) return;

            // Рисуем сцену
            _renderPipeline.Execute(this, w, h);

            _framePresenter?.Present(_gl, w, h);
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

        private void InitializeCamera()
        {
            Scene.Lights.Add(new Light()
            {
                Position = new Vector3(0f, 600.0f, 600.0f),
                Color = new Vector3(1f, 1f, 1f),
                Intensity = 0.5f
            });

            Scene.Lights.Add(new Light()
            {
                Position = new Vector3(100f, 300, 300.0f),
                Color = new Vector3(1f, 1f, 1f),
                Intensity = 0.5f
            });

            Scene.Camera.Distance = Scene3DDefault.DistantionBase;
            Scene.Camera.Pitch = Scene3DDefault.PitchBase;
            Scene.Camera.Yaw = Scene3DDefault.YawBase;
            Scene.Camera.Fov = MathF.PI / 4;
            Scene.Camera.Near = 0.1f;
            Scene.Camera.Far = 1400f;
        }
    }
}

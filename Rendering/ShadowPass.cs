using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Shaders;
using Silk.NET.Core.Loader;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Avalonia3D.Rendering
{
    public sealed class ShadowPass : IRenderPass
    {
        private uint _depthMap;
        private uint _framebuffer;
        private ShadowShader? _shadowShader;
        private readonly RenderQualitySettings _settings;
        private bool _shadowSupportChecked;
        private bool _supportsShadowPass = true;

        public ShadowPass(RenderQualitySettings settings)
        {
            _settings = settings.Validate();
        }

        public string Name => "ShadowPass";

        public void Execute(RenderPipelineContext context)
        {
            if (!_settings.ShadowsEnabled || context.Scene.Lights.Count == 0)
            {
                DisableShadows(context);
                return;
            }

            var gl = context.Gl;
            if (!IsShadowPassSupported(gl) || !EnsureResources(gl))
            {
                DisableShadows(context);
                return;
            }

            var lightSpaceMatrix = CalculateLightSpaceMatrix(context.Scene);
            context.RenderContext.FrameState.ShadowMapId = _depthMap;
            context.RenderContext.FrameState.LightSpaceMatrix = lightSpaceMatrix;

            gl.Viewport(0, 0, (uint)_settings.ShadowMapSize, (uint)_settings.ShadowMapSize);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            gl.ColorMask(false, false, false, false);
            gl.Enable(EnableCap.DepthTest);
            gl.Disable(EnableCap.Blend);
            gl.DepthMask(true);

            _shadowShader ??= new ShadowShader(gl);
            _shadowShader.Use();

            foreach (var obj in context.AllObjects)
            {
                RenderDepth(obj, context, lightSpaceMatrix);
            }

            gl.ColorMask(true, true, true, true);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private static void DisableShadows(RenderPipelineContext context)
        {
            context.RenderContext.FrameState.ShadowMapId = null;
            context.RenderContext.FrameState.LightSpaceMatrix = Matrix4x4.Identity;
        }

        private void RenderDepth(MeshObject obj, RenderPipelineContext context, Matrix4x4 lightSpaceMatrix)
        {
            if (_shadowShader == null)
            {
                return;
            }

            _shadowShader.SetUniforms(context.RenderContext, obj, lightSpaceMatrix);
            obj.RenderModel();
        }

        private unsafe bool EnsureResources(GL gl)
        {
            if (_depthMap != 0 && _framebuffer != 0)
            {
                return true;
            }

            _depthMap = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, _depthMap);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24,
                (uint)_settings.ShadowMapSize, (uint)_settings.ShadowMapSize, 0, PixelFormat.DepthComponent, PixelType.Float, null);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _framebuffer = gl.GenFramebuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D, _depthMap, 0);
            ConfigureDepthOnlyTargets(gl);

            var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            if (status == GLEnum.FramebufferComplete)
            {
                return true;
            }

            _supportsShadowPass = false;
            return false;
        }


        private static void ConfigureDepthOnlyTargets(GL gl)
        {
            try
            {
                gl.DrawBuffer(GLEnum.None);
                gl.ReadBuffer(GLEnum.None);
            }
            catch (SymbolLoadingException)
            {
                // На некоторых GLES-драйверах glDrawBuffer/glReadBuffer не экспортируются.
                // Для depth-only FBO это допустимо: продолжаем без вызовов.
            }
        }

        private bool IsShadowPassSupported(GL gl)
        {
            if (_shadowSupportChecked)
            {
                return _supportsShadowPass;
            }

            _shadowSupportChecked = true;
            try
            {
                _supportsShadowPass = gl.GetError() == GLEnum.NoError;
            }
            catch
            {
                _supportsShadowPass = false;
            }

            return _supportsShadowPass;
        }

        private static Matrix4x4 CalculateLightSpaceMatrix(Scene3D scene)
        {
            var lightPos = scene.Lights[0].Position;
            var lightView = Matrix4x4.CreateLookAt(lightPos, Vector3.Zero, Vector3.UnitY);
            var lightProjection = Matrix4x4.CreateOrthographic(1200f, 1200f, 1f, 2500f);
            return lightProjection * lightView;
        }
    }
}

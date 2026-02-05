using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Avalonia3D.Rendering
{
    public sealed class ShadowPass : IRenderPass
    {
        private const int DefaultShadowMapSize = 2048;
        private uint _depthMap;
        private uint _framebuffer;
        private int _shadowMapSize = DefaultShadowMapSize;
        private ShadowShader? _shadowShader;

        public string Name => "ShadowPass";

        public void Execute(RenderPipelineContext context)
        {
            if (context.Scene.Lights.Count == 0)
            {
                context.RenderContext.FrameState.ShadowMapId = null;
                context.RenderContext.FrameState.LightSpaceMatrix = Matrix4x4.Identity;
                return;
            }

            var gl = context.Gl;
            EnsureResources(gl);

            var lightSpaceMatrix = CalculateLightSpaceMatrix(context.Scene);
            context.RenderContext.FrameState.ShadowMapId = _depthMap;
            context.RenderContext.FrameState.LightSpaceMatrix = lightSpaceMatrix;

            gl.Viewport(0, 0, (uint)_shadowMapSize, (uint)_shadowMapSize);
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

        private void RenderDepth(MeshObject obj, RenderPipelineContext context, Matrix4x4 lightSpaceMatrix)
        {
            if (_shadowShader == null)
            {
                return;
            }

            _shadowShader.SetUniforms(context.RenderContext, obj, lightSpaceMatrix);
            obj.RenderModel();
        }

        private void EnsureResources(GL gl)
        {
            if (_depthMap != 0 && _framebuffer != 0)
            {
                return;
            }

            _depthMap = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, _depthMap);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24,
                (uint)_shadowMapSize, (uint)_shadowMapSize, 0, PixelFormat.DepthComponent, PixelType.Float, null);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _framebuffer = gl.GenFramebuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D, _depthMap, 0);
            gl.DrawBuffer(GLEnum.None);
            gl.ReadBuffer(GLEnum.None);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
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

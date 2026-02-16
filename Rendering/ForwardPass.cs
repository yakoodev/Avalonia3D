using System;
using Serilog;
using Silk.NET.OpenGL;

namespace Avalonia3D.Rendering
{
    public sealed class ForwardPass : IRenderPass
    {
        private readonly GraphicsProfile _settings;
        private bool _loggedMrtFallback;

        public ForwardPass(GraphicsProfile settings)
        {
            _settings = settings.Validate();
        }

        public string Name => "ForwardPass";

        public void Execute(RenderPipelineContext context)
        {
            var gl = context.Gl;
            var frameState = context.RenderContext.FrameState;
            frameState.AmbientStrengthClamp = _settings.PbrTuning.AmbientStrengthClamp;
            frameState.SeparateEmissiveTarget = context.RenderContext.FrameState.HasEmissiveTarget;
            frameState.SeparateEmissiveSurfaceScale = _settings.PbrTuning.SeparateEmissiveSurfaceScale;

            if (!_settings.Reflections.Enabled || _settings.Reflections.Mode == ReflectionMode.Off)
            {
                frameState.EnvironmentReflectionTextureId = null;
                frameState.EnvironmentReflectionMaxLod = 0f;
                frameState.ReflectionIntensity = 0f;
                frameState.IblDiffuseIntensity = 0f;
                frameState.IblSpecularIntensity = 0f;
                frameState.ReflectionContributionClamp = 0f;
                frameState.ReflectionsEnabled = false;
                frameState.ReflectionMode = ReflectionMode.Off;
            }

            gl.Viewport(0, 0, (uint)context.Width, (uint)context.Height);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, context.RenderContext.FrameState.OutputFramebufferId);
            gl.Enable(EnableCap.DepthTest);

            var hasEmissiveTarget = context.RenderContext.FrameState.HasEmissiveTarget;
            if (hasEmissiveTarget && !TryConfigureEmissiveDrawBuffers(gl))
            {
                hasEmissiveTarget = false;
                frameState.EmissiveFramebufferId = 0;
                frameState.EmissiveTextureId = 0;
                frameState.SeparateEmissiveTarget = false;

                if (!_loggedMrtFallback)
                {
                    _loggedMrtFallback = true;
                    Log.Warning("ForwardPass disabled emissive MRT for current context due to DrawBuffers/OpenGL compatibility.");
                }
            }

            if (_settings.MsaaPolicy == MsaaPolicy.Disabled)
            {
                gl.Disable(EnableCap.Multisample);
            }
            else
            {
                gl.Enable(EnableCap.Multisample);
            }

            gl.Disable(EnableCap.Blend);
            gl.DepthMask(true);
            ClearForwardTargets(gl, hasEmissiveTarget);

            foreach (var obj in context.OpaqueObjects)
            {
                obj.Render(context.RenderContext);
            }

            if (context.TransparentObjects.Count > 0)
            {
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                gl.DepthMask(false);

                foreach (var obj in context.TransparentObjects)
                {
                    obj.Render(context.RenderContext);
                }

                gl.DepthMask(true);
                gl.Disable(EnableCap.Blend);
            }
        }

        private bool TryConfigureEmissiveDrawBuffers(GL gl)
        {
            Span<GLEnum> drawBuffers = stackalloc GLEnum[]
            {
                GLEnum.ColorAttachment0,
                GLEnum.ColorAttachment1
            };
            return TrySetDrawBuffers(gl, drawBuffers);
        }

        private void ClearForwardTargets(GL gl, bool hasEmissiveTarget)
        {
            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (!hasEmissiveTarget)
            {
                return;
            }

            Span<GLEnum> emissiveOnlyBuffer = stackalloc GLEnum[] { GLEnum.ColorAttachment1 };
            if (TrySetDrawBuffers(gl, emissiveOnlyBuffer))
            {
                gl.ClearColor(0f, 0f, 0f, 0f);
                gl.Clear(ClearBufferMask.ColorBufferBit);
                TryConfigureEmissiveDrawBuffers(gl);
                gl.ClearColor(0f, 0f, 0f, 0f);
            }
        }

        private static bool TrySetDrawBuffers(GL gl, ReadOnlySpan<GLEnum> drawBuffers)
        {
            GlCompatibility.DrainErrors(gl);
            gl.DrawBuffers(drawBuffers);
            var error = gl.GetError();
            if (error == GLEnum.NoError)
            {
                return true;
            }

            GlCompatibility.DrainErrors(gl);
            return false;
        }

    }
}

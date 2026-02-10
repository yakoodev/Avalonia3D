using System;
using Silk.NET.OpenGL;

namespace Avalonia3D.Rendering
{
    public sealed class ForwardPass : IRenderPass
    {
        private readonly GraphicsProfile _settings;

        public ForwardPass(GraphicsProfile settings)
        {
            _settings = settings.Validate();
        }

        public string Name => "ForwardPass";

        public void Execute(RenderPipelineContext context)
        {
            var gl = context.Gl;
            gl.Viewport(0, 0, (uint)context.Width, (uint)context.Height);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, context.RenderContext.FrameState.OutputFramebufferId);
            gl.Enable(EnableCap.DepthTest);

            if (context.RenderContext.FrameState.HasEmissiveTarget)
            {
                Span<GLEnum> drawBuffers = stackalloc GLEnum[]
                {
                    GLEnum.ColorAttachment0,
                    GLEnum.ColorAttachment1
                };
                gl.DrawBuffers(drawBuffers);
            }
            else
            {
                Span<GLEnum> drawBuffers = stackalloc GLEnum[] { GLEnum.ColorAttachment0 };
                gl.DrawBuffers(drawBuffers);
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
            gl.ClearColor(_settings.Background.Red, _settings.Background.Green, _settings.Background.Blue, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

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
    }
}

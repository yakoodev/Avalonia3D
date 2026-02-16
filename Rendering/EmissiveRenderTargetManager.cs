using Serilog;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering
{
    public sealed class EmissiveRenderTargetManager
    {
        private uint _textureId;
        private uint _framebufferId;
        private int _width;
        private int _height;
        private bool _mrtSupportProbed;
        private bool _supportsSecondaryColorAttachment = true;
        private bool _loggedIncompleteFramebuffer;
        private bool _loggedUnsupportedCapabilities;

        public unsafe void Ensure(GL gl, RenderFrameState frameState, int width, int height)
        {
            if (frameState.OutputFramebufferId == 0 || width <= 0 || height <= 0)
            {
                frameState.EmissiveFramebufferId = 0;
                frameState.EmissiveTextureId = 0;
                return;
            }

            if (_textureId == 0)
            {
                _textureId = gl.GenTexture();
            }

            if (_framebufferId != frameState.OutputFramebufferId)
            {
                _framebufferId = frameState.OutputFramebufferId;
                _width = 0;
                _height = 0;
            }

            if (_width != width || _height != height)
            {
                _width = width;
                _height = height;

                gl.BindTexture(TextureTarget.Texture2D, _textureId);
                if (!GlCompatibility.TryAllocateRgbaTexture2D(gl, width, height))
                {
                    frameState.EmissiveFramebufferId = 0;
                    frameState.EmissiveTextureId = 0;
                    return;
                }
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            }

            if (!_mrtSupportProbed)
            {
                _supportsSecondaryColorAttachment = ProbeSecondaryColorAttachmentSupport(gl, _framebufferId);
                _mrtSupportProbed = true;
            }

            if (!_supportsSecondaryColorAttachment)
            {
                frameState.EmissiveFramebufferId = 0;
                frameState.EmissiveTextureId = 0;

                if (!_loggedUnsupportedCapabilities)
                {
                    _loggedUnsupportedCapabilities = true;
                    Log.Warning("Emissive render target disabled: secondary color attachment is not supported by current OpenGL context.");
                }

                return;
            }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebufferId);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _textureId, 0);

            var framebufferStatus = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (framebufferStatus != GLEnum.FramebufferComplete)
            {
                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, 0, 0);
                frameState.EmissiveFramebufferId = 0;
                frameState.EmissiveTextureId = 0;

                if (!_loggedIncompleteFramebuffer)
                {
                    _loggedIncompleteFramebuffer = true;
                    Log.Warning("Emissive render target disabled: framebuffer is incomplete. status={FramebufferStatus}, framebuffer={FramebufferId}", framebufferStatus, _framebufferId);
                }

                return;
            }

            frameState.EmissiveFramebufferId = _framebufferId;
            frameState.EmissiveTextureId = _textureId;
        }

        private bool ProbeSecondaryColorAttachmentSupport(GL gl, uint framebufferId)
        {
            if (framebufferId == 0 || _textureId == 0)
            {
                return false;
            }

            GlCompatibility.DrainErrors(gl);

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebufferId);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _textureId, 0);
            var attachError = gl.GetError();
            if (attachError != GLEnum.NoError)
            {
                return false;
            }

            Span<GLEnum> drawBuffers = stackalloc GLEnum[]
            {
                GLEnum.ColorAttachment0,
                GLEnum.ColorAttachment1
            };
            gl.DrawBuffers(drawBuffers);
            var drawBuffersError = gl.GetError();

            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, 0, 0);
            GlCompatibility.DrainErrors(gl);

            return drawBuffersError == GLEnum.NoError;
        }

        public void Release(GL gl)
        {
            if (_textureId != 0)
            {
                gl.DeleteTexture(_textureId);
            }

            _textureId = 0;
            _framebufferId = 0;
            _width = 0;
            _height = 0;
            _mrtSupportProbed = false;
            _supportsSecondaryColorAttachment = true;
            _loggedUnsupportedCapabilities = false;
        }
    }
}

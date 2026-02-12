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
        private bool _loggedIncompleteFramebuffer;

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
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
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
        }
    }
}

using Avalonia3D.Model;
using Serilog;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace Avalonia3D.Rendering
{
    public sealed class EnvironmentLightingPass : IRenderPass
    {
        private const TextureUnit ReflectionTextureUnit = TextureUnit.Texture6;

        private readonly GraphicsProfile _settings;
        private uint _environmentMapTexture;
        private string? _loadedPath;
        private bool _missingEnvironmentMapWarningLogged;

        public EnvironmentLightingPass(GraphicsProfile settings)
        {
            _settings = settings.Validate();
        }

        public string Name => "EnvironmentLightingPass";

        public void Execute(RenderPipelineContext context)
        {
            if (!_settings.Reflections.Enabled || _settings.Reflections.Mode == ReflectionMode.Off)
            {
                DisableReflections(context);
                return;
            }

            if (_settings.Reflections.Mode is ReflectionMode.ScreenSpace or ReflectionMode.Planar)
            {
                Log.Debug("Reflection mode {ReflectionMode} is not implemented yet. Keeping extension point active.", _settings.Reflections.Mode);
                DisableReflections(context);
                return;
            }

            if (!EnsureEnvironmentMap(context.Gl, _settings.Reflections.EnvironmentMapPath))
            {
                DisableReflections(context);
                return;
            }

            context.Gl.ActiveTexture(ReflectionTextureUnit);
            context.Gl.BindTexture(TextureTarget.Texture2D, _environmentMapTexture);

            context.RenderContext.FrameState.EnvironmentReflectionTextureId = _environmentMapTexture;
            context.RenderContext.FrameState.ReflectionIntensity = _settings.Reflections.Intensity;
            context.RenderContext.FrameState.ReflectionsEnabled = true;
            context.RenderContext.FrameState.ReflectionMode = _settings.Reflections.Mode;
        }

        private bool EnsureEnvironmentMap(GL gl, string? environmentMapPath)
        {
            if (string.IsNullOrWhiteSpace(environmentMapPath))
            {
                LogWarningOnce("Environment map path is not configured. Reflections are disabled.");
                return false;
            }

            var normalizedPath = environmentMapPath.Trim();
            if (_environmentMapTexture != 0 && string.Equals(_loadedPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!System.IO.File.Exists(normalizedPath))
            {
                LogWarningOnce($"Environment map '{normalizedPath}' was not found. Reflections are disabled.");
                return false;
            }

            var textureData = LoadTexture(normalizedPath);
            if (textureData == null)
            {
                LogWarningOnce($"Environment map '{normalizedPath}' could not be loaded. Reflections are disabled.");
                return false;
            }

            if (_environmentMapTexture != 0)
            {
                gl.DeleteTexture(_environmentMapTexture);
            }

            _environmentMapTexture = UploadEnvironmentTexture(gl, textureData);
            _loadedPath = normalizedPath;
            _missingEnvironmentMapWarningLogged = false;
            return _environmentMapTexture != 0;
        }

        private static TextureData? LoadTexture(string path)
        {
            try
            {
                using var image = Image.Load<Rgba32>(path);
                var textureData = new TextureData
                {
                    Width = image.Width,
                    Height = image.Height,
                    Data = new byte[image.Width * image.Height * 4],
                    DataIsPooled = false
                };

                image.CopyPixelDataTo(textureData.Data);
                return textureData;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Failed to load environment map from {Path}", path);
                return null;
            }
        }

        private static unsafe uint UploadEnvironmentTexture(GL gl, TextureData textureData)
        {
            var textureId = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, textureId);

            fixed (byte* dataPtr = textureData.Data)
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba,
                    (uint)textureData.Width, (uint)textureData.Height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, dataPtr);
            }

            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            gl.GenerateMipmap(TextureTarget.Texture2D);

            return textureId;
        }

        private void DisableReflections(RenderPipelineContext context)
        {
            context.RenderContext.FrameState.EnvironmentReflectionTextureId = null;
            context.RenderContext.FrameState.ReflectionIntensity = 0f;
            context.RenderContext.FrameState.ReflectionsEnabled = false;
            context.RenderContext.FrameState.ReflectionMode = ReflectionMode.Off;
        }

        private void LogWarningOnce(string message)
        {
            if (_missingEnvironmentMapWarningLogged)
            {
                return;
            }

            _missingEnvironmentMapWarningLogged = true;
            Log.Warning(message);
        }
    }
}

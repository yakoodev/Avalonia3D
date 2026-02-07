using Avalonia3D.Model;
using Serilog;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace Avalonia3D.Rendering
{
    public sealed class EnvironmentLightingPass : IRenderPass
    {
        private const TextureUnit ReflectionTextureUnit = TextureUnit.Texture6;

        private readonly GraphicsProfile _settings;
        private uint _environmentMapTexture;
        private string? _loadedPath;
        private bool _fallbackWarningLogged;
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
            var userPath = NormalizePath(environmentMapPath);
            if (TryLoadEnvironmentMap(gl, userPath, out _))
            {
                _fallbackWarningLogged = false;
                _missingEnvironmentMapWarningLogged = false;
                return true;
            }

            var fallbackPath = ResolveBuiltInEnvironmentMapPath();
            if (TryLoadEnvironmentMap(gl, fallbackPath, out _))
            {
                LogFallbackWarningOnce(userPath, fallbackPath);
                _missingEnvironmentMapWarningLogged = false;
                return true;
            }

            LogWarningOnce($"Unable to load environment map. User path='{userPath ?? "<empty>"}', fallback path='{fallbackPath ?? "<missing>"}'. Reflections are disabled.");
            return false;
        }

        private bool TryLoadEnvironmentMap(GL gl, string? environmentMapPath, out string? loadedPath)
        {
            loadedPath = null;
            if (string.IsNullOrWhiteSpace(environmentMapPath))
            {
                return false;
            }

            var normalizedPath = environmentMapPath.Trim();
            if (_environmentMapTexture != 0 && string.Equals(_loadedPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                loadedPath = normalizedPath;
                return true;
            }

            if (!File.Exists(normalizedPath))
            {
                return false;
            }

            var textureData = LoadTexture(normalizedPath);
            if (textureData == null)
            {
                return false;
            }

            if (_environmentMapTexture != 0)
            {
                gl.DeleteTexture(_environmentMapTexture);
            }

            _environmentMapTexture = UploadEnvironmentTexture(gl, textureData);
            _loadedPath = normalizedPath;
            loadedPath = normalizedPath;
            return _environmentMapTexture != 0;
        }

        private static string? NormalizePath(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }

        private static string? ResolveBuiltInEnvironmentMapPath()
        {
            var configuredPath = GraphicsProfile.DefaultEnvironmentMapPath;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return null;
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            var rooted = Path.Combine(AppContext.BaseDirectory, configuredPath);
            return File.Exists(rooted) ? rooted : configuredPath;
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

        private void LogFallbackWarningOnce(string? userPath, string? fallbackPath)
        {
            if (_fallbackWarningLogged)
            {
                return;
            }

            _fallbackWarningLogged = true;
            var reportedUserPath = userPath ?? "<empty>";
            Log.Warning("Environment map '{UserPath}' is missing or invalid. Using built-in fallback '{FallbackPath}'.", reportedUserPath, fallbackPath ?? "<missing>");
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

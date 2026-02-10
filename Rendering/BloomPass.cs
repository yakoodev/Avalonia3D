using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;

namespace Avalonia3D.Rendering
{
    public interface IBloomSourceResolver
    {
        BloomSourceResolution Resolve(GL gl, RenderPipelineContext context, BloomProfile bloomProfile);
    }

    public readonly record struct BloomSourceResolution(uint PrimaryTextureId, uint SecondaryTextureId, float SecondaryContribution, string SourceTag)
    {
        public static BloomSourceResolution None => new(0, 0, 0f, "none");

        public bool IsValid => PrimaryTextureId != 0;
    }

    public sealed class BloomSourceResolver : IBloomSourceResolver
    {
        private uint _sceneCopyTexture;
        private uint _sceneCopyFramebuffer;
        private int _cachedWidth;
        private int _cachedHeight;

        public BloomSourceResolution Resolve(GL gl, RenderPipelineContext context, BloomProfile bloomProfile)
        {
            var frameState = context.RenderContext.FrameState;
            var emissiveTextureId = frameState.EmissiveTextureId;
            var colorTextureId = ResolveColorTexture(gl, context);

            if (emissiveTextureId != 0)
            {
                var secondaryContribution = colorTextureId != 0
                    ? Math.Clamp(bloomProfile.ColorAdditiveContribution, 0f, 1f)
                    : 0f;
                return new BloomSourceResolution(emissiveTextureId, colorTextureId, secondaryContribution, secondaryContribution > 0f ? "emissive+color" : "emissive");
            }

            if (colorTextureId != 0)
            {
                return new BloomSourceResolution(colorTextureId, 0, 0f, "color-fallback");
            }

            return BloomSourceResolution.None;
        }

        private uint ResolveColorTexture(GL gl, RenderPipelineContext context)
        {
            var forwardColorTextureId = context.RenderContext.FrameState.ForwardColorTextureId;
            if (forwardColorTextureId != 0)
            {
                return forwardColorTextureId;
            }

            if (!EnsureSceneCopyTarget(gl, context.Width, context.Height))
            {
                return 0;
            }

            gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, context.RenderContext.FrameState.OutputFramebufferId);
            gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _sceneCopyFramebuffer);
            gl.BlitFramebuffer(0, 0, context.Width, context.Height, 0, 0, context.Width, context.Height, (uint)ClearBufferMask.ColorBufferBit, GLEnum.Linear);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, context.RenderContext.FrameState.OutputFramebufferId);

            return _sceneCopyTexture;
        }

        private unsafe bool EnsureSceneCopyTarget(GL gl, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (_sceneCopyTexture != 0 && _cachedWidth == width && _cachedHeight == height)
            {
                return true;
            }

            if (_sceneCopyTexture == 0)
            {
                _sceneCopyTexture = gl.GenTexture();
            }

            if (_sceneCopyFramebuffer == 0)
            {
                _sceneCopyFramebuffer = gl.GenFramebuffer();
            }

            _cachedWidth = width;
            _cachedHeight = height;

            gl.BindTexture(TextureTarget.Texture2D, _sceneCopyTexture);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneCopyFramebuffer);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _sceneCopyTexture, 0);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            return true;
        }
    }

    internal static class BloomRuntimeSettingsResolver
    {
        public static (float Threshold, float Intensity, float MinContribution) Resolve(BloomProfile bloom, ShaderRenderMode renderMode, GraphicsProfile graphicsProfile)
        {
            if (renderMode == ShaderRenderMode.Unlit)
            {
                return (bloom.Threshold * 0.25f, bloom.Intensity * bloom.UnlitIntensityBoost, bloom.EmissiveMinContribution);
            }

            if (renderMode is ShaderRenderMode.Default or ShaderRenderMode.Pbr)
            {
                var postFxEnabled = graphicsProfile.PostFx.Effects.HasFlag(PostEffectsFlags.Bloom) && graphicsProfile.PostFx.Bloom.Enabled;
                if (!postFxEnabled)
                {
                    return (bloom.Threshold, bloom.Intensity, 0f);
                }

                var exposure = graphicsProfile.PbrTuning.Exposure;
                var reflection = graphicsProfile.Reflections.Enabled ? graphicsProfile.Reflections.Intensity : 0f;
                var normalizedExposure = Math.Clamp(exposure, 0.1f, 4f);
                var threshold = bloom.Threshold / MathF.Sqrt(normalizedExposure);
                var intensity = bloom.Intensity * (0.85f + (0.3f * normalizedExposure) + (0.25f * reflection));
                var minContribution = bloom.EmissiveMinContribution * Math.Clamp(0.5f + reflection, 0.25f, 1.5f);
                return (Math.Clamp(threshold, 0f, 16f), Math.Clamp(intensity, 0f, 8f), Math.Clamp(minContribution, 0f, 1f));
            }

            return (bloom.Threshold, bloom.Intensity, 0f);
        }
    }

    public sealed class BloomPass : IRenderPass
    {
        private readonly GraphicsProfile _settings;
        private readonly IBloomSourceResolver _sourceResolver;
        private uint _extractProgram;
        private uint _blurProgram;
        private uint _compositeProgram;
        private uint _vao;
        private uint _vbo;
        private readonly List<uint> _levelTextures = new();
        private readonly List<uint> _levelFramebuffers = new();
        private int _cachedWidth;
        private int _cachedHeight;
        private bool _failed;
        private bool _diagnosticsLogged;

        public BloomPass(GraphicsProfile settings, IBloomSourceResolver? sourceResolver = null)
        {
            _settings = settings.Validate();
            _sourceResolver = sourceResolver ?? new BloomSourceResolver();
        }

        public string Name => "BloomPass";

        public unsafe void Execute(RenderPipelineContext context)
        {
            if (_failed)
            {
                return;
            }

            var bloom = _settings.PostFx.Bloom;
            if (!_settings.PostFx.Effects.HasFlag(PostEffectsFlags.Bloom) || !bloom.Enabled || bloom.Intensity <= 0f)
            {
                return;
            }

            var runtimeSettings = BloomRuntimeSettingsResolver.Resolve(bloom, context.Scene.RenderMode, _settings);

            var gl = context.Gl;
            if (!EnsurePrograms(gl) || !EnsureQuad(gl) || !EnsureTargets(gl, context.Width, context.Height, bloom.Iterations))
            {
                _failed = true;
                return;
            }

            var source = _sourceResolver.Resolve(gl, context, bloom);
            if (!source.IsValid)
            {
                return;
            }

            gl.Disable(EnableCap.DepthTest);
            gl.Disable(EnableCap.Blend);
            gl.BindVertexArray(_vao);

            RenderBrightPass(gl, context.Width, context.Height, runtimeSettings.Threshold, runtimeSettings.MinContribution, source);
            RenderDownsampleBlurChain(gl, bloom.Radius);
            RenderComposite(gl, context.RenderContext.FrameState.OutputFramebufferId, runtimeSettings.Intensity);

            LogDiagnosticsOnce(context, runtimeSettings, source.SourceTag);

            gl.BindVertexArray(0);
            gl.UseProgram(0);
            gl.Viewport(0, 0, (uint)context.Width, (uint)context.Height);
        }

        private void RenderBrightPass(GL gl, int fullWidth, int fullHeight, float threshold, float minContribution, BloomSourceResolution source)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _levelFramebuffers[0]);
            gl.Viewport(0, 0, (uint)Math.Max(1, fullWidth / 2), (uint)Math.Max(1, fullHeight / 2));

            gl.UseProgram(_extractProgram);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, source.PrimaryTextureId);
            gl.Uniform1(gl.GetUniformLocation(_extractProgram, "uSceneTexture"), 0);

            gl.ActiveTexture(TextureUnit.Texture1);
            gl.BindTexture(TextureTarget.Texture2D, source.SecondaryTextureId);
            gl.Uniform1(gl.GetUniformLocation(_extractProgram, "uSecondaryTexture"), 1);
            gl.Uniform1(gl.GetUniformLocation(_extractProgram, "uSecondaryContribution"), source.SecondaryContribution);

            gl.Uniform1(gl.GetUniformLocation(_extractProgram, "uThreshold"), threshold);
            gl.Uniform1(gl.GetUniformLocation(_extractProgram, "uMinContribution"), minContribution);
            gl.Uniform1(gl.GetUniformLocation(_extractProgram, "uSoftKnee"), _settings.PostFx.Bloom.SoftKnee);
            gl.Uniform1(gl.GetUniformLocation(_extractProgram, "uNormalizationBoost"), _settings.PostFx.Bloom.NormalizationBoost);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        private void RenderDownsampleBlurChain(GL gl, float radius)
        {
            for (var level = 1; level < _levelTextures.Count; level++)
            {
                gl.BindFramebuffer(FramebufferTarget.Framebuffer, _levelFramebuffers[level]);

                var width = Math.Max(1, _cachedWidth >> (level + 1));
                var height = Math.Max(1, _cachedHeight >> (level + 1));
                gl.Viewport(0, 0, (uint)width, (uint)height);

                gl.UseProgram(_blurProgram);
                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, _levelTextures[level - 1]);
                gl.Uniform1(gl.GetUniformLocation(_blurProgram, "uSceneTexture"), 0);
                gl.Uniform2(gl.GetUniformLocation(_blurProgram, "uTexelSize"), 1f / Math.Max(1, width), 1f / Math.Max(1, height));
                gl.Uniform1(gl.GetUniformLocation(_blurProgram, "uRadius"), radius);
                gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }
        }

        private void RenderComposite(GL gl, uint outputFramebufferId, float intensity)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebufferId);
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            gl.UseProgram(_compositeProgram);

            for (var level = 0; level < _levelTextures.Count; level++)
            {
                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, _levelTextures[level]);
                gl.Uniform1(gl.GetUniformLocation(_compositeProgram, "uBloomTexture"), 0);
                var levelWeight = 1f / (1f + (level * 0.5f));
                gl.Uniform1(gl.GetUniformLocation(_compositeProgram, "uIntensity"), intensity * levelWeight);
                gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }

            gl.Disable(EnableCap.Blend);
        }

        private unsafe bool EnsureTargets(GL gl, int width, int height, int iterations)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var levels = Math.Clamp(iterations, 1, 8);
            if (_cachedWidth == width && _cachedHeight == height && _levelTextures.Count == levels)
            {
                return true;
            }

            _cachedWidth = width;
            _cachedHeight = height;
            _levelTextures.Clear();
            _levelFramebuffers.Clear();

            for (var level = 0; level < levels; level++)
            {
                var tex = gl.GenTexture();
                var fbo = gl.GenFramebuffer();
                var levelWidth = Math.Max(1, width >> (level + 1));
                var levelHeight = Math.Max(1, height >> (level + 1));

                gl.BindTexture(TextureTarget.Texture2D, tex);
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)levelWidth, (uint)levelHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);

                _levelTextures.Add(tex);
                _levelFramebuffers.Add(fbo);
            }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            return true;
        }

        private bool EnsurePrograms(GL gl)
        {
            if (_extractProgram != 0 && _blurProgram != 0 && _compositeProgram != 0)
            {
                return true;
            }

            const string vertex = @"#version 300 es
precision mediump float;
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUv;
out vec2 vUv;
void main()
{
    vUv = aUv;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";

            const string extractFragment = @"#version 300 es
precision mediump float;
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSceneTexture;
uniform sampler2D uSecondaryTexture;
uniform float uSecondaryContribution;
uniform float uThreshold;
uniform float uMinContribution;
uniform float uSoftKnee;
uniform float uNormalizationBoost;
void main()
{
    vec3 color = texture(uSceneTexture, vUv).rgb;
    vec3 additive = texture(uSecondaryTexture, vUv).rgb;
    color += additive * max(uSecondaryContribution, 0.0);

    float luminance = max(max(color.r, color.g), color.b);
    float knee = max(uThreshold * max(uSoftKnee, 0.0), 0.0001);
    float soft = clamp((luminance - uThreshold + knee) / (2.0 * knee), 0.0, 1.0);
    float contribution = max(luminance - uThreshold, 0.0) + soft * soft * knee;
    contribution = max(contribution, luminance * max(uMinContribution, 0.0));
    float normalization = contribution / max(luminance, 0.0001);
    FragColor = vec4(color * normalization * max(uNormalizationBoost, 0.0), 1.0);
}";

            const string blurFragment = @"#version 300 es
precision mediump float;
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSceneTexture;
uniform vec2 uTexelSize;
uniform float uRadius;
void main()
{
    vec3 sum = texture(uSceneTexture, vUv).rgb * 0.4;
    sum += texture(uSceneTexture, vUv + vec2(uTexelSize.x * uRadius, 0.0)).rgb * 0.15;
    sum += texture(uSceneTexture, vUv - vec2(uTexelSize.x * uRadius, 0.0)).rgb * 0.15;
    sum += texture(uSceneTexture, vUv + vec2(0.0, uTexelSize.y * uRadius)).rgb * 0.15;
    sum += texture(uSceneTexture, vUv - vec2(0.0, uTexelSize.y * uRadius)).rgb * 0.15;
    FragColor = vec4(sum, 1.0);
}";

            const string compositeFragment = @"#version 300 es
precision mediump float;
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uBloomTexture;
uniform float uIntensity;
void main()
{
    vec3 bloom = texture(uBloomTexture, vUv).rgb;
    FragColor = vec4(bloom * uIntensity, 1.0);
}";

            _extractProgram = CreateProgram(gl, vertex, extractFragment);
            _blurProgram = CreateProgram(gl, vertex, blurFragment);
            _compositeProgram = CreateProgram(gl, vertex, compositeFragment);

            return _extractProgram != 0 && _blurProgram != 0 && _compositeProgram != 0;
        }

        private unsafe bool EnsureQuad(GL gl)
        {
            if (_vao != 0 && _vbo != 0)
            {
                return true;
            }

            ReadOnlySpan<float> vertices =
            [
                -1f, -1f, 0f, 0f,
                 1f, -1f, 1f, 0f,
                 1f,  1f, 1f, 1f,
                -1f, -1f, 0f, 0f,
                 1f,  1f, 1f, 1f,
                -1f,  1f, 0f, 1f
            ];

            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();

            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            fixed (float* ptr = vertices)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            gl.BindVertexArray(0);

            return true;
        }

        private static uint CreateProgram(GL gl, string vertexSource, string fragmentSource)
        {
            var vertex = CompileShader(gl, ShaderType.VertexShader, vertexSource);
            var fragment = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);
            if (vertex == 0 || fragment == 0)
            {
                return 0;
            }

            var program = gl.CreateProgram();
            gl.AttachShader(program, vertex);
            gl.AttachShader(program, fragment);
            gl.LinkProgram(program);

            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
            gl.DeleteShader(vertex);
            gl.DeleteShader(fragment);

            if (linked == 0)
            {
                Log.Warning("BloomPass shader program link failed. Bloom pass is disabled for this session.");
                return 0;
            }

            return program;
        }

        private static uint CompileShader(GL gl, ShaderType type, string source)
        {
            var shader = gl.CreateShader(type);
            gl.ShaderSource(shader, source);
            gl.CompileShader(shader);
            gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
            if (status == 0)
            {
                Log.Warning("BloomPass {ShaderType} compilation failed. Bloom pass is disabled for this session.", type);
                return 0;
            }

            return shader;
        }

        private void LogDiagnosticsOnce(RenderPipelineContext context, (float Threshold, float Intensity, float MinContribution) runtimeSettings, string source)
        {
            if (_diagnosticsLogged)
            {
                return;
            }

            _diagnosticsLogged = true;
            Log.Information(
                "BloomPass active. Mode={Mode}, Source={Source}, SourceFbo={SourceFbo}, Size={Width}x{Height}, Threshold={Threshold:0.###}, Intensity={Intensity:0.###}, MinContribution={MinContribution:0.###}, Levels={Levels}",
                context.Scene.RenderMode,
                source,
                context.RenderContext.FrameState.OutputFramebufferId,
                context.Width,
                context.Height,
                runtimeSettings.Threshold,
                runtimeSettings.Intensity,
                runtimeSettings.MinContribution,
                _levelTextures.Count);
        }
    }
}

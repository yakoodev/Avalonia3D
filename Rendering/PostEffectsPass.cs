using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering
{
    public sealed class PostEffectsPass : IRenderPass
    {
        private readonly GraphicsProfile _settings;
        private uint _copyTexture;
        private uint _program;
        private uint _vao;
        private uint _vbo;
        private int _textureWidth;
        private int _textureHeight;
        private bool _failed;

        public PostEffectsPass(GraphicsProfile settings)
        {
            _settings = settings.Validate();
        }

        public string Name => "PostEffectsPass";

        public unsafe void Execute(RenderPipelineContext context)
        {
            if (_failed || !HasToneOrGamma(_settings.PostFx.Effects))
            {
                return;
            }

            var gl = context.Gl;
            if (!EnsureProgram(gl) || !EnsureQuad(gl) || !EnsureTexture(gl, context.Width, context.Height))
            {
                _failed = true;
                return;
            }

            gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, context.RenderContext.FrameState.OutputFramebufferId);
            gl.BindTexture(TextureTarget.Texture2D, _copyTexture);
            gl.CopyTexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 0, 0, (uint)context.Width, (uint)context.Height);

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, context.RenderContext.FrameState.OutputFramebufferId);
            gl.Disable(EnableCap.DepthTest);
            gl.Disable(EnableCap.Blend);

            gl.UseProgram(_program);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, _copyTexture);

            gl.Uniform1(gl.GetUniformLocation(_program, "uSceneTexture"), 0);
            gl.Uniform1(gl.GetUniformLocation(_program, "uApplyToneMapping"), _settings.PostFx.Effects.HasFlag(PostEffectsFlags.ToneMapping) ? 1 : 0);
            gl.Uniform1(gl.GetUniformLocation(_program, "uToneMappingOperator"), (int)_settings.PostFx.ToneMapping);
            gl.Uniform1(gl.GetUniformLocation(_program, "uApplyGamma"), _settings.PostFx.Effects.HasFlag(PostEffectsFlags.GammaCorrection) ? 1 : 0);
            gl.Uniform1(gl.GetUniformLocation(_program, "uGamma"), _settings.PostFx.Gamma);
            gl.Uniform1(gl.GetUniformLocation(_program, "uExposure"), _settings.PbrTuning.Exposure);
            gl.Uniform1(gl.GetUniformLocation(_program, "uWhitePoint"), _settings.PbrTuning.PbrWhitePoint);

            gl.BindVertexArray(_vao);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            gl.BindVertexArray(0);
            gl.UseProgram(0);
        }

        private unsafe bool EnsureTexture(GL gl, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (_copyTexture != 0 && _textureWidth == width && _textureHeight == height)
            {
                return true;
            }

            if (_copyTexture == 0)
            {
                _copyTexture = gl.GenTexture();
            }

            _textureWidth = width;
            _textureHeight = height;
            gl.BindTexture(TextureTarget.Texture2D, _copyTexture);
            if (!GlCompatibility.TryAllocateRgbaTexture2D(gl, width, height))
            {
                return false;
            }
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return true;
        }

        private bool EnsureProgram(GL gl)
        {
            if (_program != 0)
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

            const string fragment = @"#version 300 es
precision highp float;
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSceneTexture;
uniform int uApplyToneMapping;
uniform int uToneMappingOperator;
uniform int uApplyGamma;
uniform float uGamma;
uniform float uExposure;
uniform float uWhitePoint;

vec3 ApplyToneMapping(vec3 color)
{
    if (uToneMappingOperator == 1)
    {
        return color / (color + vec3(1.0));
    }

    return color;
}

void main()
{
    vec4 sceneSample = texture(uSceneTexture, vUv);
    vec3 color = max(sceneSample.rgb, vec3(0.0)) * max(uExposure, 0.0001);

    if (uApplyToneMapping == 1)
    {
        color = ApplyToneMapping(color);
        color *= max(uWhitePoint, 0.0001);
    }

    color = max(color, vec3(0.0));

    if (uApplyGamma == 1)
    {
        color = pow(color, vec3(1.0 / max(uGamma, 0.0001)));
    }

    FragColor = vec4(clamp(color, vec3(0.0), vec3(1.0)), sceneSample.a);
}";

            var vertexShader = CompileShader(gl, ShaderType.VertexShader, vertex);
            var fragmentShader = CompileShader(gl, ShaderType.FragmentShader, fragment);
            if (vertexShader == 0 || fragmentShader == 0)
            {
                return false;
            }

            _program = gl.CreateProgram();
            gl.AttachShader(_program, vertexShader);
            gl.AttachShader(_program, fragmentShader);
            gl.LinkProgram(_program);

            gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out var linked);
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);

            if (linked == 0)
            {
                _program = 0;
                return false;
            }

            return true;
        }

        private unsafe bool EnsureQuad(GL gl)
        {
            if (_vao != 0 && _vbo != 0)
            {
                return true;
            }

            ReadOnlySpan<float> vertices = new float[]
            {
                -1f, -1f, 0f, 0f,
                 1f, -1f, 1f, 0f,
                 1f,  1f, 1f, 1f,
                -1f, -1f, 0f, 0f,
                 1f,  1f, 1f, 1f,
                -1f,  1f, 0f, 1f
            };

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


        private static bool HasToneOrGamma(PostEffectsFlags effects)
        {
            return effects.HasFlag(PostEffectsFlags.ToneMapping) || effects.HasFlag(PostEffectsFlags.GammaCorrection);
        }

        private static uint CompileShader(GL gl, ShaderType type, string source)
        {
            var shader = gl.CreateShader(type);
            gl.ShaderSource(shader, source);
            gl.CompileShader(shader);
            gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
            return status == 0 ? 0u : shader;
        }
    }
}

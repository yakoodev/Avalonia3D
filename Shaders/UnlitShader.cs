using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Avalonia3D.Shaders;

public sealed class UnlitShader : IShader3D
{
    private GL _gl;
    private uint _program;
    private int _mvpLocation = -1;
    private int _baseColorFactorLocation = -1;
    private int _baseColorMapLocation = -1;
    private int _hasBaseColorMapLocation = -1;
    private int _alphaLocation = -1;
    private int _alphaModeLocation = -1;
    private int _alphaCutoffLocation = -1;
    private int _emissiveFactorLocation = -1;
    private int _emissiveIntensityLocation = -1;
    private int _materialEmissiveStrengthLocation = -1;
    private int _emissionColorLocation = -1;
    private int _emissiveMapLocation = -1;
    private int _hasEmissiveMapLocation = -1;
    private int _forceWhiteEmissiveMapLocation = -1;

    public uint Handle => _program;

    private UnlitShader(GL gl)
    {
        _gl = gl;
        _program = CreateProgram();
        CacheLocations();
    }

    public static IShader3D Create(GL gl) => new UnlitShader(gl);

    public void Use() => _gl.UseProgram(_program);

    public unsafe void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default)
    {
        var camera = renderContext.Scene.Camera;
        var modelMatrix = sceneObject.CreateModelMatrix();
        var viewProjection = camera.View * camera.Projection;
        var mvpMatrix = modelMatrix * viewProjection;

        if (_mvpLocation != -1)
        {
            _gl.UniformMatrix4(_mvpLocation, 1, false, (float*)&mvpMatrix);
        }

        var material = (sceneObject as IMaterialProvider)?.Material;
        var baseColorFactor = material?.BaseColorFactor ?? new Vector4(sceneObject.BaseColor, 1f);
        var alpha = sceneObject.Opacity;
        var alphaMode = material?.AlphaMode ?? (alpha < 0.999f ? MaterialAlphaMode.Blend : MaterialAlphaMode.Opaque);
        var alphaCutoff = material?.AlphaCutoff ?? 0.5f;
        var emissiveFactor = material?.EmissiveFactor ?? Vector3.Zero;
        var emissiveIntensity = material?.EmissiveIntensity ?? 1f;
        var materialEmissiveStrength = material?.EmissiveStrength ?? 1f;
        var emissionColor = EmissionUniformResolver.ResolveSceneEmissionColor(material, sceneObject);
        var forceWhiteEmissive = EmissionUniformResolver.ShouldForceWhiteEmissiveTexture();

        if (_baseColorFactorLocation != -1)
        {
            _gl.Uniform4(_baseColorFactorLocation, baseColorFactor.X, baseColorFactor.Y, baseColorFactor.Z, baseColorFactor.W);
        }

        if (_alphaLocation != -1)
        {
            _gl.Uniform1(_alphaLocation, alpha);
        }

        if (_alphaModeLocation != -1)
        {
            _gl.Uniform1(_alphaModeLocation, (int)alphaMode);
        }

        if (_alphaCutoffLocation != -1)
        {
            _gl.Uniform1(_alphaCutoffLocation, alphaCutoff);
        }

        if (_emissiveFactorLocation != -1)
        {
            _gl.Uniform3(_emissiveFactorLocation, emissiveFactor.X, emissiveFactor.Y, emissiveFactor.Z);
        }

        if (_emissiveIntensityLocation != -1)
        {
            _gl.Uniform1(_emissiveIntensityLocation, emissiveIntensity);
        }

        if (_materialEmissiveStrengthLocation != -1)
        {
            _gl.Uniform1(_materialEmissiveStrengthLocation, materialEmissiveStrength);
        }

        if (_emissionColorLocation != -1)
        {
            _gl.Uniform3(_emissionColorLocation, emissionColor.X, emissionColor.Y, emissionColor.Z);
        }

        if (_forceWhiteEmissiveMapLocation != -1)
        {
            _gl.Uniform1(_forceWhiteEmissiveMapLocation, forceWhiteEmissive ? 1 : 0);
        }
    }

    public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId = null)
    {
        if (_materialEmissiveStrengthLocation != -1)
        {
            _gl.Uniform1(_materialEmissiveStrengthLocation, material?.EmissiveStrength ?? 1f);
        }

        if (resources.BaseColorTextureId != 0)
        {
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, resources.BaseColorTextureId);
            if (_baseColorMapLocation != -1)
            {
                _gl.Uniform1(_baseColorMapLocation, 0);
            }
        }

        if (_hasBaseColorMapLocation != -1)
        {
            _gl.Uniform1(_hasBaseColorMapLocation, resources.BaseColorTextureId != 0 ? 1 : 0);
        }

        var emissiveTextureId = EmissionUniformResolver.ShouldSampleEmissiveTexture() ? resources.EmissiveTextureId : 0;
        if (emissiveTextureId != 0)
        {
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, emissiveTextureId);
            if (_emissiveMapLocation != -1)
            {
                _gl.Uniform1(_emissiveMapLocation, 1);
            }
        }

        if (_hasEmissiveMapLocation != -1)
        {
            _gl.Uniform1(_hasEmissiveMapLocation, emissiveTextureId != 0 ? 1 : 0);
        }
    }

    public void Dispose()
    {
        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
            _program = 0;
        }
    }

    private uint CreateProgram()
    {
        uint vertex = Compile(ShaderType.VertexShader, VertexShaderSource);
        uint fragment = Compile(ShaderType.FragmentShader, FragmentShaderSource);

        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);

        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
        return program;
    }

    internal const string VertexShaderSource = @"#version 300 es
precision mediump float;
layout(location = 0) in vec3 aPosition;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uMVP;
out vec2 TexCoord;
void main()
{
    TexCoord = aTexCoord;
    gl_Position = uMVP * vec4(aPosition, 1.0);
}";

    internal const string FragmentShaderSource = @"#version 300 es
precision mediump float;
in vec2 TexCoord;
layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 EmissiveColor;
uniform vec4 uBaseColorFactor;
uniform sampler2D uBaseColorMap;
uniform int uHasBaseColorMap;
uniform float uAlpha;
uniform int uAlphaMode;
uniform float uAlphaCutoff;
uniform vec3 uEmissiveFactor;
uniform float uEmissiveIntensity;
uniform float uMaterialEmissiveStrength;
uniform vec3 uEmissionColor;
uniform sampler2D uEmissiveMap;
uniform int uHasEmissiveMap;
uniform int uForceWhiteEmissiveMap;
void main()
{
    vec4 color = uBaseColorFactor;
    if (uHasBaseColorMap == 1)
    {
        color *= texture(uBaseColorMap, TexCoord);
    }

    float sampledAlpha = color.a * uAlpha;
    if (uAlphaMode == 1 && sampledAlpha < uAlphaCutoff)
    {
        discard;
    }

    vec3 emissive = uEmissiveFactor * max(uEmissiveIntensity, 0.0);
    emissive *= max(uMaterialEmissiveStrength, 0.0);
    if (uHasEmissiveMap == 1)
    {
        vec3 emissiveSample = uForceWhiteEmissiveMap == 1 ? vec3(1.0) : texture(uEmissiveMap, TexCoord).rgb;
        emissive *= emissiveSample;
    }

    float alpha = uAlphaMode == 2 ? sampledAlpha : 1.0;
    vec3 totalEmissive = max(emissive + uEmissionColor, vec3(0.0));
    FragColor = vec4(color.rgb + totalEmissive, alpha);
    EmissiveColor = vec4(totalEmissive, alpha);
}";

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        return shader;
    }

    private void CacheLocations()
    {
        _mvpLocation = _gl.GetUniformLocation(_program, "uMVP");
        _baseColorFactorLocation = _gl.GetUniformLocation(_program, "uBaseColorFactor");
        _baseColorMapLocation = _gl.GetUniformLocation(_program, "uBaseColorMap");
        _hasBaseColorMapLocation = _gl.GetUniformLocation(_program, "uHasBaseColorMap");
        _alphaLocation = _gl.GetUniformLocation(_program, "uAlpha");
        _alphaModeLocation = _gl.GetUniformLocation(_program, "uAlphaMode");
        _alphaCutoffLocation = _gl.GetUniformLocation(_program, "uAlphaCutoff");
        _emissiveFactorLocation = _gl.GetUniformLocation(_program, "uEmissiveFactor");
        _emissiveIntensityLocation = _gl.GetUniformLocation(_program, "uEmissiveIntensity");
        _materialEmissiveStrengthLocation = _gl.GetUniformLocation(_program, "uMaterialEmissiveStrength");
        _emissionColorLocation = _gl.GetUniformLocation(_program, "uEmissionColor");
        _emissiveMapLocation = _gl.GetUniformLocation(_program, "uEmissiveMap");
        _hasEmissiveMapLocation = _gl.GetUniformLocation(_program, "uHasEmissiveMap");
        _forceWhiteEmissiveMapLocation = _gl.GetUniformLocation(_program, "uForceWhiteEmissiveMap");
    }
}

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
        var alpha = material?.Opacity ?? sceneObject.Opacity;

        if (_baseColorFactorLocation != -1)
        {
            _gl.Uniform4(_baseColorFactorLocation, baseColorFactor.X, baseColorFactor.Y, baseColorFactor.Z, baseColorFactor.W);
        }

        if (_alphaLocation != -1)
        {
            _gl.Uniform1(_alphaLocation, alpha);
        }
    }

    public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId = null)
    {
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
        const string vert = @"#version 300 es
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

        const string frag = @"#version 300 es
precision mediump float;
in vec2 TexCoord;
out vec4 FragColor;
uniform vec4 uBaseColorFactor;
uniform sampler2D uBaseColorMap;
uniform int uHasBaseColorMap;
uniform float uAlpha;
void main()
{
    vec4 color = uBaseColorFactor;
    if (uHasBaseColorMap == 1)
    {
        color *= texture(uBaseColorMap, TexCoord);
    }

    FragColor = vec4(color.rgb, color.a * uAlpha);
}";

        uint vertex = Compile(ShaderType.VertexShader, vert);
        uint fragment = Compile(ShaderType.FragmentShader, frag);

        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);

        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
        return program;
    }

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
    }
}

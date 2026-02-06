using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Avalonia3D.Shaders;

public sealed class NormalsDebugShader : IShader3D
{
    private readonly GL _gl;
    private uint _program;
    private int _mvpLocation = -1;
    private int _modelLocation = -1;

    public uint Handle => _program;

    private NormalsDebugShader(GL gl)
    {
        _gl = gl;
        _program = CreateProgram();
        CacheLocations();
    }

    public static IShader3D Create(GL gl) => new NormalsDebugShader(gl);

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

        if (_modelLocation != -1)
        {
            _gl.UniformMatrix4(_modelLocation, 1, false, (float*)&modelMatrix);
        }
    }

    public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId = null)
    {
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
layout(location = 1) in vec3 aNormal;
uniform mat4 uMVP;
uniform mat4 uModel;
out vec3 Normal;
void main()
{
    gl_Position = uMVP * vec4(aPosition, 1.0);
    Normal = normalize(mat3(uModel) * aNormal);
}";

        const string frag = @"#version 300 es
precision mediump float;
in vec3 Normal;
out vec4 FragColor;
void main()
{
    vec3 n = normalize(Normal) * 0.5 + 0.5;
    FragColor = vec4(n, 1.0);
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
        _modelLocation = _gl.GetUniformLocation(_program, "uModel");
    }
}

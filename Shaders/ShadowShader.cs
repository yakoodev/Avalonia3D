using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Avalonia3D.Shaders
{
    public sealed class ShadowShader : IShader3D, IDisposable
    {
        private GL _gl;
        private uint _program;
        private int _lightSpaceLoc = -1;
        private int _modelLocation = -1;

        public uint Handle => _program;

        public ShadowShader(GL gl)
        {
            _gl = gl;
            _program = Create();
            CacheLocations();
        }

        public static IShader3D Create(GL gL)
        {
            var sh = new ShadowShader(gL);
            sh.Create();
            return sh;
        }

        private uint Create()
        {
            string vert = @"#version 300 es
        precision highp float;
        layout(location = 0) in vec3 aPosition;
        uniform mat4 uLightSpaceMatrix;
        uniform mat4 uModel;
        void main() {
            gl_Position = uLightSpaceMatrix * uModel * vec4(aPosition, 1.0);
        }";

            string frag = @"#version 300 es
        precision highp float;
        void main() { }";

            uint v = Compile(ShaderType.VertexShader, vert);
            uint f = Compile(ShaderType.FragmentShader, frag);

            uint p = _gl.CreateProgram();
            _gl.AttachShader(p, v);
            _gl.AttachShader(p, f);
            _gl.LinkProgram(p);

            _gl.DeleteShader(v);
            _gl.DeleteShader(f);
            return p;
        }

        private uint Compile(ShaderType type, string src)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, src);
            _gl.CompileShader(shader);
            return shader;
        }

        private void CacheLocations()
        {
            _lightSpaceLoc = _gl.GetUniformLocation(_program, "uLightSpaceMatrix");
            _modelLocation = _gl.GetUniformLocation(_program, "uModel");
        }

        public unsafe void SetUniforms(Matrix4x4 model, Matrix4x4 lightSpace)
        {
            if (_lightSpaceLoc != -1)
                _gl.UniformMatrix4(_lightSpaceLoc, 1, false, (float*)&lightSpace);

            if (_modelLocation != -1)
                _gl.UniformMatrix4(_modelLocation, 1, false, (float*)&model);
        }

        public void Use() => _gl.UseProgram(_program);

        public void Dispose()
        {
            if (_program != 0)
                _gl.DeleteProgram(_program);
        }

        public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId = null)
        {
        }

        public unsafe void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default)
        {
            var modelMatrix = sceneObject.CreateModelMatrix();

            if (_modelLocation != -1)
                _gl.UniformMatrix4(_modelLocation, 1, false, (float*)&modelMatrix);

            if (_lightSpaceLoc != -1)
                _gl.UniformMatrix4(_lightSpaceLoc, 1, false, (float*)&lightSpaceMatrix);
        }
    }
}

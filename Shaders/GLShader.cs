using Avalonia3D.Interfaces;
using Avalonia3D.Model.StandObjects;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace Avalonia3D.Shaders
{
    public sealed class GLShader : IShader3D, IDisposable
    {
        // Кэшированные uniform-локации
        private int _mvpLocation = -1;
        private int _modelLocation = -1;
        private int _lightPosLocation = -1;
        private int _viewPosLocation = -1;
        private int _lightColorLocation = -1;
        private int _hasTextureLocation = -1;
        private int _textureLocation = -1;
        private int _shadowMapLocation = -1;
        private int _lightSpaceMatrixLocation = -1;
        private int _modelColorLocation = -1;
        private int _ambientLocation = -1;
        private int _specularLocation = -1;
        private int _intensityLocation;
        private int _shininessLocation = -1;
        private int _alphaLocation = -1;
        private int _modelEmissionColorLocation = -1;

        public uint Handle => _shaderProgram;
        private GL _gl;
        private uint _shaderProgram;

        public void Dispose()
        {
            if (_shaderProgram != 0)
            {
                _gl?.DeleteProgram(_shaderProgram);
                _shaderProgram = 0;
            }
        }

        public void InitializeShaders(GL gL)
        {
            _gl = gL;
            _shaderProgram = CreateShaderProgram();
            CacheUniformLocations();
        }

        private uint CreateShaderProgram()
        {
            // Вершинный шейдер
            string vertSource = @"#version 300 es
precision mediump float;
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 uMVP;
uniform mat4 uModel;
uniform mat4 uLightSpaceMatrix;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;
out vec4 FragPosLightSpace;

void main()
{
    gl_Position = uMVP * vec4(aPosition, 1.0);
    FragPos = vec3(uModel * vec4(aPosition, 1.0));
    mat3 normalMatrix = transpose(inverse(mat3(uModel)));
    Normal = normalize(mat3(uModel) * aNormal);
    TexCoord = aTexCoord;
    FragPosLightSpace = uLightSpaceMatrix * vec4(FragPos, 1.0);
}";

            // Фрагментный шейдер
            string fragSource = @"#version 300 es
precision mediump float;

in vec2 TexCoord;
in vec3 Normal;
in vec3 FragPos;
in vec4 FragPosLightSpace;

out vec4 FragColor;

uniform sampler2D uTexture;
uniform int uHasTexture;

uniform sampler2D uShadowMap;
uniform vec3 uLightPos[2];
uniform vec3 uLightColor[2];
uniform float uIntensity[2];

uniform vec3 uViewPos;

uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform int uShininess;

uniform vec3 uModelColor;
uniform vec3 uEmissionColor;

uniform float uAlpha; 

float ShadowCalculation(vec4 fragPosLightSpace, vec3 normal, vec3 lightDir)
{
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    projCoords = projCoords * 0.5 + 0.5;

    float closestDepth = texture(uShadowMap, projCoords.xy).r;  
    float currentDepth = projCoords.z;

    float bias = max(0.005 * (1.0 - dot(normal, lightDir)), 0.0005);
    float shadow = 0.0;
    vec2 texelSize = 1.0 / vec2(textureSize(uShadowMap, 0));

    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float pcfDepth = texture(uShadowMap, projCoords.xy + vec2(x, y) * texelSize).r; 
            shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0;        
        }    
    }
    shadow /= 9.0;

    if(projCoords.z > 1.0)
        shadow = 0.0;

    return shadow;
}

void main()
{
    vec3 resultLight = vec3(0.0);
    vec3 norm = normalize(Normal);
    vec3 viewDir = normalize(uViewPos - FragPos);

    for (int i = 0; i < 2; i++) {
        vec3 ambient = uAmbientStrength * uLightColor[i];

        vec3 lightDir = normalize(uLightPos[i] - FragPos);
        float diff = max(dot(norm, lightDir), 0.0);
        vec3 diffuse = diff * uLightColor[i];

        vec3 reflectDir = reflect(-lightDir, norm);
        float spec = pow(max(dot(viewDir, reflectDir), 0.0), float(uShininess));
        vec3 specular = uSpecularStrength * spec * uLightColor[i];

        float shadow = ShadowCalculation(FragPosLightSpace, norm, lightDir);

        resultLight += (ambient + (1.0 - shadow) * (diffuse + specular)) * uIntensity[i];
    }

    vec3 result;
    float alpha;

    if (uHasTexture == 1) {
        vec4 texColor = texture(uTexture, TexCoord);
        result = resultLight * texColor.rgb;
        alpha = texColor.a * uAlpha;
        FragColor = vec4(result, alpha);
    }
    else {
        result = resultLight * uModelColor;
        result += uEmissionColor;
        alpha = uAlpha;
        FragColor = vec4(result, alpha);
    }
}";

            uint vertexShader = CompileShader(ShaderType.VertexShader, vertSource);
            uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragSource);

            uint program = _gl.CreateProgram();
            _gl.AttachShader(program, vertexShader);
            _gl.AttachShader(program, fragmentShader);
            _gl.LinkProgram(program);

            string programLog = _gl.GetProgramInfoLog(program);
            if (!string.IsNullOrEmpty(programLog))
                Log.Information($"Shader program link log: {programLog}");

            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
            {
                Log.Information($"Shader program linking failed: {programLog}");
                throw new Exception("Shader program linking failed");
            }

            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            return program;
        }

        private uint CompileShader(ShaderType type, string source)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            string infoLog = _gl.GetShaderInfoLog(shader);
            if (!string.IsNullOrEmpty(infoLog))
            {
                Log.Information($"{type} compile log: {infoLog}");
            }

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
            {
                Log.Information($"{type} compilation failed: {infoLog}");
                throw new Exception($"{type} compilation failed");
            }

            return shader;
        }

        public static IShader3D Create(GL gL)
        {
            var sh = new GLShader();
            sh.InitializeShaders(gL);
            return sh;
        }

        public void Use()
        {
            _gl.UseProgram(_shaderProgram);
        }

        private void CacheUniformLocations()
        {
            _mvpLocation = _gl.GetUniformLocation(_shaderProgram, "uMVP");
            _modelLocation = _gl.GetUniformLocation(_shaderProgram, "uModel");
            _lightPosLocation = _gl.GetUniformLocation(_shaderProgram, "uLightPos");
            _viewPosLocation = _gl.GetUniformLocation(_shaderProgram, "uViewPos");
            _lightColorLocation = _gl.GetUniformLocation(_shaderProgram, "uLightColor");
            _hasTextureLocation = _gl.GetUniformLocation(_shaderProgram, "uHasTexture");
            _textureLocation = _gl.GetUniformLocation(_shaderProgram, "uTexture");
            _shadowMapLocation = _gl.GetUniformLocation(_shaderProgram, "uShadowMap");
            _lightSpaceMatrixLocation = _gl.GetUniformLocation(_shaderProgram, "uLightSpaceMatrix");
            _modelColorLocation = _gl.GetUniformLocation(_shaderProgram, "uModelColor");
            _modelEmissionColorLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissionColor");
            _ambientLocation = _gl.GetUniformLocation(_shaderProgram, "uAmbientStrength");
            _specularLocation = _gl.GetUniformLocation(_shaderProgram, "uSpecularStrength");
            _intensityLocation = _gl.GetUniformLocation(_shaderProgram, "uIntensity");
            _shininessLocation = _gl.GetUniformLocation(_shaderProgram, "uShininess");
            _alphaLocation = _gl.GetUniformLocation(_shaderProgram, "uAlpha");
        }

        public unsafe void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default)
        {
            var camera = renderContext.Scene.Camera;
            var modelMatrix = sceneObject.CreateModelMatrix();
            var mvpMatrix = modelMatrix * camera.View * camera.Projection;

            if (_mvpLocation != -1)
                _gl.UniformMatrix4(_mvpLocation, 1, false, (float*)&mvpMatrix);

            if (_modelLocation != -1)
                _gl.UniformMatrix4(_modelLocation, 1, false, (float*)&modelMatrix);

            if (_lightSpaceMatrixLocation != -1)
                _gl.UniformMatrix4(_lightSpaceMatrixLocation, 1, false, (float*)&lightSpaceMatrix);

            var lights = renderContext.Scene.Lights;

            if (_lightPosLocation != -1)
            {
                var lightPositions = new float[lights.Count * 3];
                for (int i = 0; i < lights.Count; i++)
                {
                    lightPositions[i * 3 + 0] = lights[i].Position.X;
                    lightPositions[i * 3 + 1] = lights[i].Position.Y;
                    lightPositions[i * 3 + 2] = lights[i].Position.Z;
                }
                fixed (float* p = &lightPositions[0])
                    _gl.Uniform3(_lightPosLocation, (uint)lights.Count, p);
            }

            if (_lightColorLocation != -1)
            {
                var lightColors = new float[lights.Count * 3];
                for (int i = 0; i < lights.Count; i++)
                {
                    lightColors[i * 3 + 0] = lights[i].Color.X;
                    lightColors[i * 3 + 1] = lights[i].Color.Y;
                    lightColors[i * 3 + 2] = lights[i].Color.Z;
                }
                fixed (float* p = &lightColors[0])
                    _gl.Uniform3(_lightColorLocation, (uint)lights.Count, p);
            }

            if (_intensityLocation != -1)
            {
                var intensities = new float[lights.Count];
                for (int i = 0; i < lights.Count; i++)
                    intensities[i] = lights[i].Intensity;
                _gl.Uniform1(_intensityLocation, (uint)lights.Count, intensities);
            }

            if (_viewPosLocation != -1)
                _gl.Uniform3(_viewPosLocation, camera.Position.X, camera.Position.Y, camera.Position.Z);

            if (_modelColorLocation != -1)
                _gl.Uniform3(_modelColorLocation, sceneObject.BaseColor.X, sceneObject.BaseColor.Y, sceneObject.BaseColor.Z);

            if (_modelEmissionColorLocation != -1)
                _gl.Uniform3(_modelEmissionColorLocation, sceneObject.EmissionColor.X, sceneObject.EmissionColor.Y, sceneObject.EmissionColor.Z);

            if (_alphaLocation != -1)
                _gl.Uniform1(_alphaLocation, sceneObject.Opacity);

            if (_ambientLocation != -1)
                _gl.Uniform1(_ambientLocation, lights[0].AmbientStrength);

            if (_specularLocation != -1)
                _gl.Uniform1(_specularLocation, lights[0].SpecularStrength);

            if (_shininessLocation != -1)
                _gl.Uniform1(_shininessLocation, lights[0].Shininess);
        }

        public void BindTexture(uint textureId, uint? shadowMapId)
        {
            if (textureId != 0)
            {
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, textureId);
                if (_textureLocation != -1)
                    _gl.Uniform1(_textureLocation, 0);
            }
            else
            {
                //Debug.WriteLine("No texture to bind (textureId = 0)");
            }

            if (_hasTextureLocation != -1)
                _gl.Uniform1(_hasTextureLocation, textureId != 0 ? 1 : 0);

            if (shadowMapId.HasValue)
            {
                _gl.ActiveTexture(TextureUnit.Texture1);
                _gl.BindTexture(TextureTarget.Texture2D, shadowMapId.Value);
                if (_shadowMapLocation != -1)
                    _gl.Uniform1(_shadowMapLocation, 1);
            }
        }
    }
}

using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Avalonia3D.Shaders
{
    public sealed class GLShader : IShader3D, IDisposable
    {
        private const int MaxLights = 2;
        // Кэшированные uniform-локации
        private int _mvpLocation = -1;
        private int _modelLocation = -1;
        private int _lightPosLocation = -1;
        private int _viewPosLocation = -1;
        private int _lightColorLocation = -1;
        private int _baseColorMapLocation = -1;
        private int _normalMapLocation = -1;
        private int _metallicRoughnessMapLocation = -1;
        private int _occlusionMapLocation = -1;
        private int _emissiveMapLocation = -1;
        private int _hasBaseColorMapLocation = -1;
        private int _hasNormalMapLocation = -1;
        private int _hasMetallicRoughnessMapLocation = -1;
        private int _hasOcclusionMapLocation = -1;
        private int _hasEmissiveMapLocation = -1;
        private int _shadowMapLocation = -1;
        private int _hasShadowMapLocation = -1;
        private int _lightCountLocation = -1;
        private int _lightSpaceMatrixLocation = -1;
        private int _modelColorLocation = -1;
        private int _ambientLocation = -1;
        private int _specularLocation = -1;
        private int _intensityLocation;
        private int _shininessLocation = -1;
        private int _alphaLocation = -1;
        private int _modelEmissionColorLocation = -1;
        private int _baseColorFactorLocation = -1;
        private int _metallicFactorLocation = -1;
        private int _roughnessFactorLocation = -1;
        private int _occlusionStrengthLocation = -1;
        private int _emissiveFactorLocation = -1;

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

uniform sampler2D uBaseColorMap;
uniform sampler2D uNormalMap;
uniform sampler2D uMetallicRoughnessMap;
uniform sampler2D uOcclusionMap;
uniform sampler2D uEmissiveMap;

uniform int uHasBaseColorMap;
uniform int uHasNormalMap;
uniform int uHasMetallicRoughnessMap;
uniform int uHasOcclusionMap;
uniform int uHasEmissiveMap;

uniform sampler2D uShadowMap;
uniform int uHasShadowMap;
uniform vec3 uLightPos[2];
uniform vec3 uLightColor[2];
uniform float uIntensity[2];
uniform int uLightCount;

uniform vec3 uViewPos;

uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform int uShininess;

uniform vec3 uModelColor;
uniform vec3 uEmissionColor;

uniform vec4 uBaseColorFactor;
uniform float uMetallicFactor;
uniform float uRoughnessFactor;
uniform float uOcclusionStrength;
uniform vec3 uEmissiveFactor;

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

vec3 GetNormal()
{
    vec3 norm = normalize(Normal);
    if (uHasNormalMap == 0)
    {
        return norm;
    }

    vec3 tangentNormal = texture(uNormalMap, TexCoord).xyz * 2.0 - 1.0;

    vec3 Q1 = dFdx(FragPos);
    vec3 Q2 = dFdy(FragPos);
    vec2 st1 = dFdx(TexCoord);
    vec2 st2 = dFdy(TexCoord);

    vec3 T = normalize(Q1 * st2.t - Q2 * st1.t);
    vec3 B = -normalize(cross(norm, T));
    mat3 TBN = mat3(T, B, norm);

    return normalize(TBN * tangentNormal);
}

void main()
{
    vec3 norm = GetNormal();
    vec3 viewDir = normalize(uViewPos - FragPos);

    vec4 baseColor = uBaseColorFactor;
    if (uHasBaseColorMap == 1)
    {
        baseColor *= texture(uBaseColorMap, TexCoord);
    }

    float metallic = uMetallicFactor;
    float roughness = uRoughnessFactor;
    if (uHasMetallicRoughnessMap == 1)
    {
        vec4 mrSample = texture(uMetallicRoughnessMap, TexCoord);
        metallic *= mrSample.b;
        roughness *= mrSample.g;
    }

    float ao = 1.0;
    if (uHasOcclusionMap == 1)
    {
        float aoSample = texture(uOcclusionMap, TexCoord).r;
        ao = mix(1.0, aoSample, uOcclusionStrength);
    }

    vec3 emissive = uEmissiveFactor;
    if (uHasEmissiveMap == 1)
    {
        emissive *= texture(uEmissiveMap, TexCoord).rgb;
    }

    vec3 albedo = baseColor.rgb;
    vec3 diffuseColor = albedo * (1.0 - metallic);
    vec3 specularColor = mix(vec3(0.04), albedo, metallic);

    vec3 resultLight = vec3(0.0);

    float smoothness = clamp(1.0 - roughness, 0.04, 1.0);
    float shininess = mix(2.0, float(uShininess), smoothness);

    for (int i = 0; i < 2; i++)
    {
        if (i >= uLightCount)
            break;

        vec3 ambient = uAmbientStrength * uLightColor[i];

        vec3 lightDir = normalize(uLightPos[i] - FragPos);
        float diff = max(dot(norm, lightDir), 0.0);
        vec3 diffuse = diff * diffuseColor * uLightColor[i];

        vec3 reflectDir = reflect(-lightDir, norm);
        float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
        vec3 specular = uSpecularStrength * spec * specularColor * uLightColor[i];

        float shadow = uHasShadowMap == 1 ? ShadowCalculation(FragPosLightSpace, norm, lightDir) : 0.0;
        resultLight += (ambient + (1.0 - shadow) * (diffuse + specular)) * uIntensity[i];
    }

    if (uLightCount == 0)
    {
        resultLight = albedo * 0.65;
    }

    vec3 result = resultLight * ao + emissive + uEmissionColor;
    float alpha = baseColor.a * uAlpha;
    FragColor = vec4(result, alpha);
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
            _lightPosLocation = _gl.GetUniformLocation(_shaderProgram, "uLightPos[0]");
            _viewPosLocation = _gl.GetUniformLocation(_shaderProgram, "uViewPos");
            _lightColorLocation = _gl.GetUniformLocation(_shaderProgram, "uLightColor[0]");
            _baseColorMapLocation = _gl.GetUniformLocation(_shaderProgram, "uBaseColorMap");
            _normalMapLocation = _gl.GetUniformLocation(_shaderProgram, "uNormalMap");
            _metallicRoughnessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uMetallicRoughnessMap");
            _occlusionMapLocation = _gl.GetUniformLocation(_shaderProgram, "uOcclusionMap");
            _emissiveMapLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissiveMap");
            _hasBaseColorMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasBaseColorMap");
            _hasNormalMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasNormalMap");
            _hasMetallicRoughnessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasMetallicRoughnessMap");
            _hasOcclusionMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasOcclusionMap");
            _hasEmissiveMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasEmissiveMap");
            _shadowMapLocation = _gl.GetUniformLocation(_shaderProgram, "uShadowMap");
            _hasShadowMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasShadowMap");
            _lightCountLocation = _gl.GetUniformLocation(_shaderProgram, "uLightCount");
            _lightSpaceMatrixLocation = _gl.GetUniformLocation(_shaderProgram, "uLightSpaceMatrix");
            _modelColorLocation = _gl.GetUniformLocation(_shaderProgram, "uModelColor");
            _modelEmissionColorLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissionColor");
            _baseColorFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uBaseColorFactor");
            _metallicFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uMetallicFactor");
            _roughnessFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uRoughnessFactor");
            _occlusionStrengthLocation = _gl.GetUniformLocation(_shaderProgram, "uOcclusionStrength");
            _emissiveFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissiveFactor");
            _ambientLocation = _gl.GetUniformLocation(_shaderProgram, "uAmbientStrength");
            _specularLocation = _gl.GetUniformLocation(_shaderProgram, "uSpecularStrength");
            _intensityLocation = _gl.GetUniformLocation(_shaderProgram, "uIntensity[0]");
            _shininessLocation = _gl.GetUniformLocation(_shaderProgram, "uShininess");
            _alphaLocation = _gl.GetUniformLocation(_shaderProgram, "uAlpha");
        }

        public unsafe void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default)
        {
            var camera = renderContext.Scene.Camera;
            var modelMatrix = sceneObject.CreateModelMatrix();
            var viewProjection = camera.View * camera.Projection;
            var mvpMatrix = modelMatrix * viewProjection;

            if (_mvpLocation != -1)
                _gl.UniformMatrix4(_mvpLocation, 1, false, (float*)&mvpMatrix);

            if (_modelLocation != -1)
                _gl.UniformMatrix4(_modelLocation, 1, false, (float*)&modelMatrix);

            if (_lightSpaceMatrixLocation != -1)
                _gl.UniformMatrix4(_lightSpaceMatrixLocation, 1, false, (float*)&lightSpaceMatrix);

            var lights = renderContext.Scene.Lights;
            var lightCount = Math.Min(lights.Count, MaxLights);

            if (_lightCountLocation != -1)
                _gl.Uniform1(_lightCountLocation, lightCount);

            if (_lightPosLocation != -1 && lightCount > 0)
            {
                var lightPositions = new float[MaxLights * 3];
                for (int i = 0; i < lightCount; i++)
                {
                    lightPositions[i * 3 + 0] = lights[i].Position.X;
                    lightPositions[i * 3 + 1] = lights[i].Position.Y;
                    lightPositions[i * 3 + 2] = lights[i].Position.Z;
                }
                fixed (float* p = &lightPositions[0])
                    _gl.Uniform3(_lightPosLocation, (uint)MaxLights, p);
            }

            if (_lightColorLocation != -1 && lightCount > 0)
            {
                var lightColors = new float[MaxLights * 3];
                for (int i = 0; i < lightCount; i++)
                {
                    lightColors[i * 3 + 0] = lights[i].Color.X;
                    lightColors[i * 3 + 1] = lights[i].Color.Y;
                    lightColors[i * 3 + 2] = lights[i].Color.Z;
                }
                fixed (float* p = &lightColors[0])
                    _gl.Uniform3(_lightColorLocation, (uint)MaxLights, p);
            }

            if (_intensityLocation != -1)
            {
                var intensities = new float[MaxLights];
                for (int i = 0; i < lightCount; i++)
                    intensities[i] = lights[i].Intensity;
                _gl.Uniform1(_intensityLocation, (uint)MaxLights, intensities);
            }

            if (_viewPosLocation != -1)
                _gl.Uniform3(_viewPosLocation, camera.Position.X, camera.Position.Y, camera.Position.Z);

            var material = (sceneObject as Interfaces.IMaterialProvider)?.Material;
            var baseColorFactor = material?.BaseColorFactor ?? new Vector4(sceneObject.BaseColor, 1f);
            var emissiveFactor = material?.EmissiveFactor ?? sceneObject.EmissionColor;
            var metallicFactor = material?.MetallicFactor ?? 0f;
            var roughnessFactor = material?.RoughnessFactor ?? 1f;
            var occlusionStrength = material?.OcclusionStrength ?? 1f;
            var alpha = material?.Opacity ?? sceneObject.Opacity;
            var emissionColor = material != null ? Vector3.Zero : sceneObject.EmissionColor;

            if (_modelColorLocation != -1)
                _gl.Uniform3(_modelColorLocation, sceneObject.BaseColor.X, sceneObject.BaseColor.Y, sceneObject.BaseColor.Z);

            if (_modelEmissionColorLocation != -1)
                _gl.Uniform3(_modelEmissionColorLocation, emissionColor.X, emissionColor.Y, emissionColor.Z);

            if (_baseColorFactorLocation != -1)
                _gl.Uniform4(_baseColorFactorLocation, baseColorFactor.X, baseColorFactor.Y, baseColorFactor.Z, baseColorFactor.W);

            if (_emissiveFactorLocation != -1)
                _gl.Uniform3(_emissiveFactorLocation, emissiveFactor.X, emissiveFactor.Y, emissiveFactor.Z);

            if (_metallicFactorLocation != -1)
                _gl.Uniform1(_metallicFactorLocation, metallicFactor);

            if (_roughnessFactorLocation != -1)
                _gl.Uniform1(_roughnessFactorLocation, roughnessFactor);

            if (_occlusionStrengthLocation != -1)
                _gl.Uniform1(_occlusionStrengthLocation, occlusionStrength);

            if (_alphaLocation != -1)
                _gl.Uniform1(_alphaLocation, alpha);

            var primaryLight = lightCount > 0 ? lights[0] : null;

            if (_ambientLocation != -1)
                _gl.Uniform1(_ambientLocation, primaryLight?.AmbientStrength ?? 0.25f);

            if (_specularLocation != -1)
                _gl.Uniform1(_specularLocation, primaryLight?.SpecularStrength ?? 0.35f);

            if (_shininessLocation != -1)
                _gl.Uniform1(_shininessLocation, primaryLight?.Shininess ?? 16);
        }

        public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId)
        {
            BindTextureUnit(resources.BaseColorTextureId, _baseColorMapLocation, _hasBaseColorMapLocation, 0);
            BindTextureUnit(resources.NormalTextureId, _normalMapLocation, _hasNormalMapLocation, 1);
            BindTextureUnit(resources.MetallicRoughnessTextureId, _metallicRoughnessMapLocation, _hasMetallicRoughnessMapLocation, 2);
            BindTextureUnit(resources.OcclusionTextureId, _occlusionMapLocation, _hasOcclusionMapLocation, 3);
            BindTextureUnit(resources.EmissiveTextureId, _emissiveMapLocation, _hasEmissiveMapLocation, 4);

            if (shadowMapId.HasValue)
            {
                _gl.ActiveTexture(TextureUnit.Texture5);
                _gl.BindTexture(TextureTarget.Texture2D, shadowMapId.Value);
                if (_shadowMapLocation != -1)
                    _gl.Uniform1(_shadowMapLocation, 5);

                if (_hasShadowMapLocation != -1)
                    _gl.Uniform1(_hasShadowMapLocation, 1);
            }
            else if (_hasShadowMapLocation != -1)
            {
                _gl.Uniform1(_hasShadowMapLocation, 0);
            }
        }

        private void BindTextureUnit(uint textureId, int samplerLocation, int hasTextureLocation, int unit)
        {
            if (textureId != 0)
            {
                _gl.ActiveTexture(TextureUnit.Texture0 + unit);
                _gl.BindTexture(TextureTarget.Texture2D, textureId);
                if (samplerLocation != -1)
                    _gl.Uniform1(samplerLocation, unit);
            }

            if (hasTextureLocation != -1)
                _gl.Uniform1(hasTextureLocation, textureId != 0 ? 1 : 0);
        }
    }
}

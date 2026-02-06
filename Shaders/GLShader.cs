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
        private readonly PbrFeatures _features;
        private readonly PbrShaderSourceBuilder _sourceBuilder;
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
        private int _environmentMapLocation = -1;
        private int _reflectionIntensityLocation = -1;
        private int _hasEnvironmentMapLocation = -1;

        public uint Handle => _shaderProgram;
        private GL _gl;
        private uint _shaderProgram;

        public GLShader() : this(PbrFeatures.None, null)
        {
        }

        public GLShader(PbrFeatures features, PbrShaderSourceBuilder? sourceBuilder = null)
        {
            _features = features;
            _sourceBuilder = sourceBuilder ?? new PbrShaderSourceBuilder();
        }

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
            var shaderSource = _sourceBuilder.Build(_features);
            string vertSource = shaderSource.VertexSource;
            string fragSource = shaderSource.FragmentSource;

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
            var sh = new GLShader(PbrFeatures.None);
            sh.InitializeShaders(gL);
            return sh;
        }

        public static IShader3D Create(GL gL, PbrFeatures features)
        {
            var sh = new GLShader(features);
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
            _environmentMapLocation = _gl.GetUniformLocation(_shaderProgram, "uEnvironmentMap");
            _reflectionIntensityLocation = _gl.GetUniformLocation(_shaderProgram, "uReflectionIntensity");
            _hasEnvironmentMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasEnvironmentMap");
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

            var frameState = renderContext.FrameState;
            if (_environmentMapLocation != -1)
                _gl.Uniform1(_environmentMapLocation, 6);

            if (_reflectionIntensityLocation != -1)
                _gl.Uniform1(_reflectionIntensityLocation, frameState.ReflectionsEnabled ? frameState.ReflectionIntensity : 0f);

            if (_hasEnvironmentMapLocation != -1)
                _gl.Uniform1(_hasEnvironmentMapLocation, frameState.ReflectionsEnabled && frameState.EnvironmentReflectionTextureId.HasValue ? 1 : 0);
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

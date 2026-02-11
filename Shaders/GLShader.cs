using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Avalonia3D.Rendering.Diagnostics;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Avalonia3D.Shaders
{
    public sealed class GLShader : IShader3D, IDisposable
    {
        private readonly PbrFeatures _features;
        private readonly PbrShaderSourceBuilder _sourceBuilder;
        private readonly int _maxLights;
        private readonly HashSet<string> _loggedEmissionOverrideMeshes = new(StringComparer.Ordinal);
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
        private int _forceWhiteEmissiveMapLocation = -1;
        private int _clearcoatMapLocation = -1;
        private int _clearcoatRoughnessMapLocation = -1;
        private int _clearcoatNormalMapLocation = -1;
        private int _sheenColorMapLocation = -1;
        private int _sheenRoughnessMapLocation = -1;
        private int _specularMapLocation = -1;
        private int _specularColorMapLocation = -1;
        private int _transmissionMapLocation = -1;
        private int _volumeThicknessMapLocation = -1;
        private int _hasClearcoatMapLocation = -1;
        private int _hasClearcoatRoughnessMapLocation = -1;
        private int _hasClearcoatNormalMapLocation = -1;
        private int _hasSheenColorMapLocation = -1;
        private int _hasSheenRoughnessMapLocation = -1;
        private int _hasSpecularMapLocation = -1;
        private int _hasSpecularColorMapLocation = -1;
        private int _hasTransmissionMapLocation = -1;
        private int _hasVolumeThicknessMapLocation = -1;
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
        private int _alphaCutoffLocation = -1;
        private int _alphaModeLocation = -1;
        private int _emissiveIntensityLocation = -1;
        private int _modelEmissionColorLocation = -1;
        private int _baseColorFactorLocation = -1;
        private int _metallicFactorLocation = -1;
        private int _roughnessFactorLocation = -1;
        private int _occlusionStrengthLocation = -1;
        private int _emissiveFactorLocation = -1;
        private int _baseColorUvOffsetLocation = -1;
        private int _baseColorUvScaleLocation = -1;
        private int _baseColorUvRotationLocation = -1;
        private int _emissiveUvOffsetLocation = -1;
        private int _emissiveUvScaleLocation = -1;
        private int _emissiveUvRotationLocation = -1;
        private int _baseColorTexCoordSetLocation = -1;
        private int _normalTexCoordSetLocation = -1;
        private int _metallicRoughnessTexCoordSetLocation = -1;
        private int _occlusionTexCoordSetLocation = -1;
        private int _emissiveTexCoordSetLocation = -1;
        private int _environmentMapLocation = -1;
        private int _reflectionIntensityLocation = -1;
        private int _hasEnvironmentMapLocation = -1;
        private int _transmissionFactorLocation = -1;
        private int _transmissionThicknessLocation = -1;
        private int _transmissionIorLocation = -1;
        private int _transmissionAttenuationDistanceLocation = -1;
        private int _transmissionAttenuationColorLocation = -1;
        private int _hasTransmissionLocation = -1;
        private int _clearcoatFactorLocation = -1;
        private int _clearcoatRoughnessLocation = -1;
        private int _sheenColorFactorLocation = -1;
        private int _sheenRoughnessFactorLocation = -1;
        private int _materialSpecularFactorLocation = -1;
        private int _materialSpecularColorFactorLocation = -1;
        private int _materialIorLocation = -1;
        private int _materialEmissiveStrengthLocation = -1;
        private int _manualBaseColorSrgbDecodeLocation = -1;
        private int _manualEmissiveSrgbDecodeLocation = -1;
        private int _pbrDebugViewModeLocation = -1;

        public uint Handle => _shaderProgram;
        private GL _gl;
        private uint _shaderProgram;

        public GLShader() : this(PbrFeatures.None, RenderQualitySettings.DefaultMaxLights, null)
        {
        }

        public GLShader(PbrFeatures features, int maxLights = RenderQualitySettings.DefaultMaxLights, PbrShaderSourceBuilder? sourceBuilder = null)
        {
            _features = features;
            _maxLights = Math.Clamp(maxLights, RenderQualitySettings.MinLights, RenderQualitySettings.MaxSupportedLights);
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
            var shaderSource = _sourceBuilder.Build(_features, _maxLights);
            string vertSource = shaderSource.VertexSource;
            string fragSource = shaderSource.FragmentSource;

            uint vertexShader = CompileShader(ShaderType.VertexShader, vertSource, fragSource);
            uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragSource, vertSource);

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

        private uint CompileShader(ShaderType type, string source, string otherSource)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            string infoLog = _gl.GetShaderInfoLog(shader);
            if (!string.IsNullOrEmpty(infoLog))
            {
                Log.Information("{ShaderType} compile log for features={Features}, maxLights={MaxLights}: {InfoLog}", type, _features, _maxLights, infoLog);
            }

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
            {
                var diagnostic = BuildCompileFailureDiagnostics(type, source, otherSource, infoLog);
                Log.Information("{ShaderType} compilation failed. {Diagnostic}", type, diagnostic);
                throw new Exception($"{type} compilation failed. {diagnostic}");
            }

            return shader;
        }

        private string BuildCompileFailureDiagnostics(ShaderType failedType, string failedSource, string otherSource, string compileLog)
        {
            var failedPreview = BuildSourcePreview(failedSource);
            var failedHash = ComputeStableHash(failedSource);
            var otherHash = ComputeStableHash(otherSource);
            var otherType = failedType == ShaderType.VertexShader ? ShaderType.FragmentShader : ShaderType.VertexShader;

            return $"features={_features}, maxLights={_maxLights}, failedShader={failedType}, failedShaderHash={failedHash}, {failedType}Log='{compileLog}', {failedType}SourcePreview='{failedPreview}', {otherType}Hash={otherHash}";
        }

        private static string BuildSourcePreview(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return "<empty>";
            }

            const int maxLength = 480;
            var normalized = source.Replace("\r", string.Empty).Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized[..maxLength] + " ... <truncated>";
        }

        private static string ComputeStableHash(string source)
        {
            var bytes = Encoding.UTF8.GetBytes(source ?? string.Empty);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        public static IShader3D Create(GL gL)
        {
            var sh = new GLShader(PbrFeatures.None, RenderQualitySettings.DefaultMaxLights);
            sh.InitializeShaders(gL);
            return sh;
        }

        public static IShader3D Create(GL gL, PbrFeatures features, int maxLights = RenderQualitySettings.DefaultMaxLights)
        {
            var sh = new GLShader(features, maxLights);
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
            _clearcoatMapLocation = _gl.GetUniformLocation(_shaderProgram, "uClearcoatMap");
            _clearcoatRoughnessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uClearcoatRoughnessMap");
            _clearcoatNormalMapLocation = _gl.GetUniformLocation(_shaderProgram, "uClearcoatNormalMap");
            _sheenColorMapLocation = _gl.GetUniformLocation(_shaderProgram, "uSheenColorMap");
            _sheenRoughnessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uSheenRoughnessMap");
            _specularMapLocation = _gl.GetUniformLocation(_shaderProgram, "uSpecularMap");
            _specularColorMapLocation = _gl.GetUniformLocation(_shaderProgram, "uSpecularColorMap");
            _transmissionMapLocation = _gl.GetUniformLocation(_shaderProgram, "uTransmissionMap");
            _volumeThicknessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uVolumeThicknessMap");
            _hasBaseColorMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasBaseColorMap");
            _hasNormalMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasNormalMap");
            _hasMetallicRoughnessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasMetallicRoughnessMap");
            _hasOcclusionMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasOcclusionMap");
            _hasEmissiveMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasEmissiveMap");
            _forceWhiteEmissiveMapLocation = _gl.GetUniformLocation(_shaderProgram, "uForceWhiteEmissiveMap");
            _hasClearcoatMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasClearcoatMap");
            _hasClearcoatRoughnessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasClearcoatRoughnessMap");
            _hasClearcoatNormalMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasClearcoatNormalMap");
            _hasSheenColorMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasSheenColorMap");
            _hasSheenRoughnessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasSheenRoughnessMap");
            _hasSpecularMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasSpecularMap");
            _hasSpecularColorMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasSpecularColorMap");
            _hasTransmissionMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasTransmissionMap");
            _hasVolumeThicknessMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasVolumeThicknessMap");
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
            _baseColorUvOffsetLocation = _gl.GetUniformLocation(_shaderProgram, "uBaseColorUvOffset");
            _baseColorUvScaleLocation = _gl.GetUniformLocation(_shaderProgram, "uBaseColorUvScale");
            _baseColorUvRotationLocation = _gl.GetUniformLocation(_shaderProgram, "uBaseColorUvRotation");
            _emissiveUvOffsetLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissiveUvOffset");
            _emissiveUvScaleLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissiveUvScale");
            _emissiveUvRotationLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissiveUvRotation");
            _baseColorTexCoordSetLocation = _gl.GetUniformLocation(_shaderProgram, "uBaseColorTexCoordSet");
            _normalTexCoordSetLocation = _gl.GetUniformLocation(_shaderProgram, "uNormalTexCoordSet");
            _metallicRoughnessTexCoordSetLocation = _gl.GetUniformLocation(_shaderProgram, "uMetallicRoughnessTexCoordSet");
            _occlusionTexCoordSetLocation = _gl.GetUniformLocation(_shaderProgram, "uOcclusionTexCoordSet");
            _emissiveTexCoordSetLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissiveTexCoordSet");
            _ambientLocation = _gl.GetUniformLocation(_shaderProgram, "uAmbientStrength");
            _specularLocation = _gl.GetUniformLocation(_shaderProgram, "uSpecularStrength");
            _intensityLocation = _gl.GetUniformLocation(_shaderProgram, "uIntensity[0]");
            _shininessLocation = _gl.GetUniformLocation(_shaderProgram, "uShininess");
            _alphaLocation = _gl.GetUniformLocation(_shaderProgram, "uAlpha");
            _alphaCutoffLocation = _gl.GetUniformLocation(_shaderProgram, "uAlphaCutoff");
            _alphaModeLocation = _gl.GetUniformLocation(_shaderProgram, "uAlphaMode");
            _emissiveIntensityLocation = _gl.GetUniformLocation(_shaderProgram, "uEmissiveIntensity");
            _environmentMapLocation = _gl.GetUniformLocation(_shaderProgram, "uEnvironmentMap");
            _reflectionIntensityLocation = _gl.GetUniformLocation(_shaderProgram, "uReflectionIntensity");
            _hasEnvironmentMapLocation = _gl.GetUniformLocation(_shaderProgram, "uHasEnvironmentMap");
            _transmissionFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uTransmissionFactor");
            _transmissionThicknessLocation = _gl.GetUniformLocation(_shaderProgram, "uTransmissionThickness");
            _transmissionIorLocation = _gl.GetUniformLocation(_shaderProgram, "uTransmissionIor");
            _transmissionAttenuationDistanceLocation = _gl.GetUniformLocation(_shaderProgram, "uTransmissionAttenuationDistance");
            _transmissionAttenuationColorLocation = _gl.GetUniformLocation(_shaderProgram, "uTransmissionAttenuationColor");
            _hasTransmissionLocation = _gl.GetUniformLocation(_shaderProgram, "uHasTransmission");
            _clearcoatFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uClearcoatFactor");
            _clearcoatRoughnessLocation = _gl.GetUniformLocation(_shaderProgram, "uClearcoatRoughness");
            _sheenColorFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uSheenColorFactor");
            _sheenRoughnessFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uSheenRoughnessFactor");
            _materialSpecularFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uSpecularFactor");
            _materialSpecularColorFactorLocation = _gl.GetUniformLocation(_shaderProgram, "uSpecularColorFactor");
            _materialIorLocation = _gl.GetUniformLocation(_shaderProgram, "uMaterialIor");
            _materialEmissiveStrengthLocation = _gl.GetUniformLocation(_shaderProgram, "uMaterialEmissiveStrength");
            _manualBaseColorSrgbDecodeLocation = _gl.GetUniformLocation(_shaderProgram, "uManualBaseColorSrgbDecode");
            _manualEmissiveSrgbDecodeLocation = _gl.GetUniformLocation(_shaderProgram, "uManualEmissiveSrgbDecode");
            _pbrDebugViewModeLocation = _gl.GetUniformLocation(_shaderProgram, "uPbrDebugViewMode");
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
            var lightCount = Math.Min(lights.Count, _maxLights);

            if (_lightCountLocation != -1)
                _gl.Uniform1(_lightCountLocation, lightCount);

            if (_lightPosLocation != -1 && lightCount > 0)
            {
                var lightPositions = new float[_maxLights * 3];
                for (int i = 0; i < lightCount; i++)
                {
                    lightPositions[i * 3 + 0] = lights[i].Position.X;
                    lightPositions[i * 3 + 1] = lights[i].Position.Y;
                    lightPositions[i * 3 + 2] = lights[i].Position.Z;
                }
                fixed (float* p = &lightPositions[0])
                    _gl.Uniform3(_lightPosLocation, (uint)_maxLights, p);
            }

            if (_lightColorLocation != -1 && lightCount > 0)
            {
                var lightColors = new float[_maxLights * 3];
                for (int i = 0; i < lightCount; i++)
                {
                    lightColors[i * 3 + 0] = lights[i].Color.X;
                    lightColors[i * 3 + 1] = lights[i].Color.Y;
                    lightColors[i * 3 + 2] = lights[i].Color.Z;
                }
                fixed (float* p = &lightColors[0])
                    _gl.Uniform3(_lightColorLocation, (uint)_maxLights, p);
            }

            if (_intensityLocation != -1)
            {
                var intensities = new float[_maxLights];
                for (int i = 0; i < lightCount; i++)
                    intensities[i] = lights[i].Intensity;
                _gl.Uniform1(_intensityLocation, (uint)_maxLights, intensities);
            }

            if (_viewPosLocation != -1)
                _gl.Uniform3(_viewPosLocation, camera.Position.X, camera.Position.Y, camera.Position.Z);

            if (_pbrDebugViewModeLocation != -1)
                _gl.Uniform1(_pbrDebugViewModeLocation, (int)renderContext.FrameState.PbrDebugViewMode);

            var material = (sceneObject as Interfaces.IMaterialProvider)?.Material;
            MaterialRenderDiagnostics.DumpIfEnabled(
                material,
                resources: (sceneObject as MeshObject)?.Resources,
                scene: renderContext.Scene,
                materialKey: sceneObject.Name ?? sceneObject.Node.Name ?? "$material");

            var baseColorFactor = material?.BaseColorFactor ?? new Vector4(sceneObject.BaseColor, 1f);
            var emissiveFactor = material?.EmissiveFactor ?? sceneObject.EmissionColor;
            var metallicFactor = material?.MetallicFactor ?? 0f;
            var roughnessFactor = material?.RoughnessFactor ?? 1f;
            var occlusionStrength = material?.OcclusionStrength ?? 1f;
            var alpha = sceneObject.Opacity;
            var alphaCutoff = material?.AlphaCutoff ?? 0.5f;
            var alphaMode = material?.AlphaMode ?? (alpha < 0.999f ? MaterialAlphaMode.Blend : MaterialAlphaMode.Opaque);
            var emissiveIntensity = material?.EmissiveIntensity ?? 1f;
            var emissionColor = EmissionUniformResolver.ResolveSceneEmissionColor(material, sceneObject);
            var forceWhiteEmissive = EmissionUniformResolver.ShouldForceWhiteEmissiveTexture();

            if (material != null && emissionColor.LengthSquared() > 0.0001f)
            {
                var key = (sceneObject.Name ?? sceneObject.Node.Name ?? "$mesh") + "|" + renderContext.Scene.RenderMode;
                if (!_loggedEmissionOverrideMeshes.Contains(key))
                {
                    _loggedEmissionOverrideMeshes.Add(key);
                    Log.Debug("GLShader emissive uniforms for mesh '{MeshId}': shaderProgram={Program}, renderMode={RenderMode}, emissiveFactor={EmissiveFactor}, emissiveIntensity={EmissiveIntensity}, sceneEmission={SceneEmission}, alphaMode={AlphaMode}, alpha={Alpha}, hasEmissiveTexture={HasEmissiveTexture}",
                        sceneObject.Name ?? sceneObject.Node.Name ?? "$mesh",
                        _shaderProgram,
                        renderContext.Scene.RenderMode,
                        emissiveFactor,
                        emissiveIntensity,
                        emissionColor,
                        alphaMode,
                        alpha,
                        material?.EmissiveTexture != null);
                }
            }

            if (_modelColorLocation != -1)
                _gl.Uniform3(_modelColorLocation, sceneObject.BaseColor.X, sceneObject.BaseColor.Y, sceneObject.BaseColor.Z);

            if (_modelEmissionColorLocation != -1)
                _gl.Uniform3(_modelEmissionColorLocation, emissionColor.X, emissionColor.Y, emissionColor.Z);

            if (_baseColorFactorLocation != -1)
                _gl.Uniform4(_baseColorFactorLocation, baseColorFactor.X, baseColorFactor.Y, baseColorFactor.Z, baseColorFactor.W);

            if (_emissiveFactorLocation != -1)
                _gl.Uniform3(_emissiveFactorLocation, emissiveFactor.X, emissiveFactor.Y, emissiveFactor.Z);

            var baseColorUv = material?.TextureRuntime.BaseColor ?? new MaterialTextureTransformRuntime();
            var normalUv = material?.TextureRuntime.Normal ?? new MaterialTextureTransformRuntime();
            var metallicRoughnessUv = material?.TextureRuntime.MetallicRoughness ?? new MaterialTextureTransformRuntime();
            var occlusionUv = material?.TextureRuntime.Occlusion ?? new MaterialTextureTransformRuntime();
            var emissiveUv = material?.TextureRuntime.Emissive ?? new MaterialTextureTransformRuntime();

            if (_baseColorUvOffsetLocation != -1)
                _gl.Uniform2(_baseColorUvOffsetLocation, baseColorUv.UvOffset.X, baseColorUv.UvOffset.Y);

            if (_baseColorUvScaleLocation != -1)
                _gl.Uniform2(_baseColorUvScaleLocation, baseColorUv.UvScale.X, baseColorUv.UvScale.Y);

            if (_baseColorUvRotationLocation != -1)
                _gl.Uniform1(_baseColorUvRotationLocation, baseColorUv.UvRotation);

            if (_emissiveUvOffsetLocation != -1)
                _gl.Uniform2(_emissiveUvOffsetLocation, emissiveUv.UvOffset.X, emissiveUv.UvOffset.Y);

            if (_emissiveUvScaleLocation != -1)
                _gl.Uniform2(_emissiveUvScaleLocation, emissiveUv.UvScale.X, emissiveUv.UvScale.Y);

            if (_emissiveUvRotationLocation != -1)
                _gl.Uniform1(_emissiveUvRotationLocation, emissiveUv.UvRotation);

            if (_baseColorTexCoordSetLocation != -1)
                _gl.Uniform1(_baseColorTexCoordSetLocation, baseColorUv.TexCoordSet);

            if (_normalTexCoordSetLocation != -1)
                _gl.Uniform1(_normalTexCoordSetLocation, normalUv.TexCoordSet);

            if (_metallicRoughnessTexCoordSetLocation != -1)
                _gl.Uniform1(_metallicRoughnessTexCoordSetLocation, metallicRoughnessUv.TexCoordSet);

            if (_occlusionTexCoordSetLocation != -1)
                _gl.Uniform1(_occlusionTexCoordSetLocation, occlusionUv.TexCoordSet);

            if (_emissiveTexCoordSetLocation != -1)
                _gl.Uniform1(_emissiveTexCoordSetLocation, emissiveUv.TexCoordSet);

            if (_metallicFactorLocation != -1)
                _gl.Uniform1(_metallicFactorLocation, metallicFactor);

            if (_roughnessFactorLocation != -1)
                _gl.Uniform1(_roughnessFactorLocation, roughnessFactor);

            if (_occlusionStrengthLocation != -1)
                _gl.Uniform1(_occlusionStrengthLocation, occlusionStrength);

            if (_alphaLocation != -1)
                _gl.Uniform1(_alphaLocation, alpha);

            if (_alphaCutoffLocation != -1)
                _gl.Uniform1(_alphaCutoffLocation, alphaCutoff);

            if (_alphaModeLocation != -1)
                _gl.Uniform1(_alphaModeLocation, (int)alphaMode);

            if (_emissiveIntensityLocation != -1)
                _gl.Uniform1(_emissiveIntensityLocation, emissiveIntensity);

            if (_forceWhiteEmissiveMapLocation != -1)
                _gl.Uniform1(_forceWhiteEmissiveMapLocation, forceWhiteEmissive ? 1 : 0);

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

            var hasTransmission = material?.HasTransmission == true && material.TransmissionFactor > 0.001f;
            var transmissionFactor = material?.TransmissionFactor ?? 0f;
            var transmissionThickness = material?.TransmissionThickness ?? 0f;
            var transmissionIor = material?.TransmissionIor ?? 1.5f;
            var transmissionAttenuationDistance = material?.TransmissionAttenuationDistance ?? float.PositiveInfinity;
            var transmissionAttenuationColor = material?.TransmissionAttenuationColor ?? Vector3.One;

            var clearcoatFactor = material?.ClearcoatFactor ?? 0f;
            var clearcoatRoughness = material?.ClearcoatRoughness ?? 0f;
            var sheenColorFactor = material?.SheenColorFactor ?? Vector3.Zero;
            var sheenRoughnessFactor = material?.SheenRoughnessFactor ?? 0f;
            var specularFactor = material?.SpecularFactor ?? 1f;
            var specularColorFactor = material?.SpecularColorFactor ?? Vector3.One;
            var materialIor = material?.Ior ?? 1.5f;
            var materialEmissiveStrength = material?.EmissiveStrength ?? 1f;

            if (_transmissionFactorLocation != -1)
                _gl.Uniform1(_transmissionFactorLocation, transmissionFactor);

            if (_transmissionThicknessLocation != -1)
                _gl.Uniform1(_transmissionThicknessLocation, transmissionThickness);

            if (_transmissionIorLocation != -1)
                _gl.Uniform1(_transmissionIorLocation, transmissionIor);

            if (_transmissionAttenuationDistanceLocation != -1)
                _gl.Uniform1(_transmissionAttenuationDistanceLocation, float.IsPositiveInfinity(transmissionAttenuationDistance) ? 1_000_000f : transmissionAttenuationDistance);

            if (_transmissionAttenuationColorLocation != -1)
                _gl.Uniform3(_transmissionAttenuationColorLocation, transmissionAttenuationColor.X, transmissionAttenuationColor.Y, transmissionAttenuationColor.Z);

            if (_clearcoatFactorLocation != -1)
                _gl.Uniform1(_clearcoatFactorLocation, clearcoatFactor);

            if (_clearcoatRoughnessLocation != -1)
                _gl.Uniform1(_clearcoatRoughnessLocation, clearcoatRoughness);

            if (_sheenColorFactorLocation != -1)
                _gl.Uniform3(_sheenColorFactorLocation, sheenColorFactor.X, sheenColorFactor.Y, sheenColorFactor.Z);

            if (_sheenRoughnessFactorLocation != -1)
                _gl.Uniform1(_sheenRoughnessFactorLocation, sheenRoughnessFactor);

            if (_materialSpecularFactorLocation != -1)
                _gl.Uniform1(_materialSpecularFactorLocation, specularFactor);

            if (_materialSpecularColorFactorLocation != -1)
                _gl.Uniform3(_materialSpecularColorFactorLocation, specularColorFactor.X, specularColorFactor.Y, specularColorFactor.Z);

            if (_materialIorLocation != -1)
                _gl.Uniform1(_materialIorLocation, materialIor);

            if (_materialEmissiveStrengthLocation != -1)
                _gl.Uniform1(_materialEmissiveStrengthLocation, materialEmissiveStrength);

            if (_hasTransmissionLocation != -1)
                _gl.Uniform1(_hasTransmissionLocation, hasTransmission ? 1 : 0);
        }

        public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId)
        {
            if (_manualBaseColorSrgbDecodeLocation != -1)
            {
                _gl.Uniform1(_manualBaseColorSrgbDecodeLocation,
                    TextureColorManagement.HasMissingSrgbDecode(resources.TextureColorFlags, TextureSemantic.BaseColor) ? 1 : 0);
            }

            if (_manualEmissiveSrgbDecodeLocation != -1)
            {
                _gl.Uniform1(_manualEmissiveSrgbDecodeLocation,
                    TextureColorManagement.HasMissingSrgbDecode(resources.TextureColorFlags, TextureSemantic.Emissive) ? 1 : 0);
            }

            BindTextureSlot(resources, TextureSemantic.BaseColor, resources.BaseColorTextureId, _baseColorMapLocation, _hasBaseColorMapLocation, 0);
            BindTextureSlot(resources, TextureSemantic.Normal, resources.NormalTextureId, _normalMapLocation, _hasNormalMapLocation, 1);
            BindTextureSlot(resources, TextureSemantic.MetallicRoughness, resources.MetallicRoughnessTextureId, _metallicRoughnessMapLocation, _hasMetallicRoughnessMapLocation, 2);
            BindTextureSlot(resources, TextureSemantic.Occlusion, resources.OcclusionTextureId, _occlusionMapLocation, _hasOcclusionMapLocation, 3);
            var emissiveTextureId = EmissionUniformResolver.ShouldSampleEmissiveTexture() ? resources.EmissiveTextureId : 0;
            BindTextureSlot(resources, TextureSemantic.Emissive, emissiveTextureId, _emissiveMapLocation, _hasEmissiveMapLocation, 4);
            BindTextureSlot(resources, TextureSemantic.Clearcoat, resources.ClearcoatTextureId, _clearcoatMapLocation, _hasClearcoatMapLocation, 7);
            BindTextureSlot(resources, TextureSemantic.ClearcoatRoughness, resources.ClearcoatRoughnessTextureId, _clearcoatRoughnessMapLocation, _hasClearcoatRoughnessMapLocation, 8);
            BindTextureSlot(resources, TextureSemantic.ClearcoatNormal, resources.ClearcoatNormalTextureId, _clearcoatNormalMapLocation, _hasClearcoatNormalMapLocation, 9);
            BindTextureSlot(resources, TextureSemantic.SheenColor, resources.SheenColorTextureId, _sheenColorMapLocation, _hasSheenColorMapLocation, 10);
            BindTextureSlot(resources, TextureSemantic.SheenRoughness, resources.SheenRoughnessTextureId, _sheenRoughnessMapLocation, _hasSheenRoughnessMapLocation, 11);
            BindTextureSlot(resources, TextureSemantic.Specular, resources.SpecularTextureId, _specularMapLocation, _hasSpecularMapLocation, 12);
            BindTextureSlot(resources, TextureSemantic.SpecularColor, resources.SpecularColorTextureId, _specularColorMapLocation, _hasSpecularColorMapLocation, 13);
            BindTextureSlot(resources, TextureSemantic.Transmission, resources.TransmissionTextureId, _transmissionMapLocation, _hasTransmissionMapLocation, 14);
            BindTextureSlot(resources, TextureSemantic.VolumeThickness, resources.VolumeThicknessTextureId, _volumeThicknessMapLocation, _hasVolumeThicknessMapLocation, 15);

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


        private void BindTextureSlot(RenderResources resources, TextureSemantic semantic, uint textureId, int samplerLocation, int hasTextureLocation, int unit)
        {
            if (MaterialRenderDiagnostics.Enabled)
            {
                Log.Debug("GL texture bind slot: semantic={Semantic}, unit={TextureUnit}, texture={TextureId}, willBind={WillBind}", semantic, unit, textureId, textureId != 0);
            }

            resources.MarkTextureGpuBinding(semantic, unit, textureId != 0);
            BindTextureUnit(textureId, samplerLocation, hasTextureLocation, unit);
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

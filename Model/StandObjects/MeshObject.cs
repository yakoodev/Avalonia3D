using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public class MeshObject : SceneObject, IMaterialProvider
    {
        private RenderResources? _resources;
        private RenderResourceManager? _resourceManager;
        private GL? _gl;
        private Model? _model;
        private Vertex[]? _baseVertices;
        private Vertex[]? _morphedVertices;
        private float[] _lastAppliedMorphWeights = [];
        private float[] _currentMorphWeights = [];
        private Vector3 _baseEmissiveFactor = Vector3.Zero;
        private float _baseEmissiveIntensity = 1f;

        public Vector3 LocalBoundsMin { get; private set; } = Vector3.Zero;
        public Vector3 LocalBoundsMax { get; private set; } = Vector3.Zero;
        public bool HasGeometryBounds { get; private set; }

        public void AssignModel(Model model)
        {
            _model = model;
            _baseVertices = model?.Vertices == null ? null : (Vertex[])model.Vertices.Clone();
            _morphedVertices = _baseVertices == null ? null : (Vertex[])_baseVertices.Clone();
            _lastAppliedMorphWeights = [];
            _currentMorphWeights = [];

            if (model?.Vertices == null || model.Vertices.Length == 0)
            {
                Gravity = Vector3.Zero;
                HasGeometryBounds = false;
                LocalBoundsMin = Vector3.Zero;
                LocalBoundsMax = Vector3.Zero;
                return;
            }

            Gravity = GetCenterOfGravity(model.Vertices);
            (LocalBoundsMin, LocalBoundsMax) = GetLocalBounds(model.Vertices);
            HasGeometryBounds = true;

            if (model.Material != null)
            {
                BaseColor = new Vector3(model.Material.BaseColorFactor.X, model.Material.BaseColorFactor.Y, model.Material.BaseColorFactor.Z);
                EmissionColor = model.Material.EmissiveFactor;
                _baseEmissiveFactor = model.Material.EmissiveFactor;
                _baseEmissiveIntensity = model.Material.EmissiveIntensity;
                // Opacity сцены — это runtime override; alpha материала учитывается в shader через baseColor.a
                Opacity = 1f;
            }
        }

        public void BuildRenderResources(RenderResourceManager resourceManager)
        {
            Setup(resourceManager);
        }

        public void Setup(RenderResourceManager resourceManager)
        {
            if (resourceManager == null || _model == null)
            {
                return;
            }

            if (_resources != null)
            {
                return;
            }

            _resourceManager = resourceManager;
            _gl = resourceManager.Gl;
            _resources = resourceManager.Acquire(_model);
        }

        public Material? Material => _model?.Material;
        public bool SupportsMorphTargets => _model?.HasMorphTargets == true;

        public void SetMorphWeights(float[] weights)
        {
            _currentMorphWeights = weights == null ? [] : (float[])weights.Clone();
            ApplyMorphTargetsIfNeeded();
        }

        private static Vector3 GetCenterOfGravity(Vertex[] vertices)
        {
            if (vertices == null || vertices.Length == 0)
            {
                return Vector3.Zero;
            }

            Vector3 sum = Vector3.Zero;

            foreach (var v in vertices)
            {
                sum += v.Position;
            }

            return sum / vertices.Length;
        }

        private static (Vector3 Min, Vector3 Max) GetLocalBounds(Vertex[] vertices)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex.Position);
                max = Vector3.Max(max, vertex.Position);
            }

            return (min, max);
        }

        public override unsafe void Render(IRenderContext renderContext)
        {
            if (_gl == null || _resources == null)
            {
                return;
            }

            ApplyMorphTargetsIfNeeded();

            var shader = renderContext.Scene.ShaderSelectionPolicy.Select(Material, renderContext.Scene, _gl);
            if (shader == null)
            {
                return;
            }

            ApplyMaterialState(Material, renderContext.Scene.RenderMode);

            shader.Use();
            shader.BindMaterial(_resources, Material, renderContext.FrameState.ShadowMapId);
            shader.SetUniforms(renderContext, this, renderContext.FrameState.LightSpaceMatrix);
            RenderModel(renderContext);

            ResetMaterialState(Material, renderContext.Scene.RenderMode);
        }

        public unsafe void RenderModel(IRenderContext? renderContext = null)
        {
            if (_gl == null || _resources == null)
            {
                return;
            }

            _gl.BindVertexArray(_resources.Vao);

            if (_resources.IndexCount > 0)
            {
                var drawType = _resources.IndicesUShort ? DrawElementsType.UnsignedShort : DrawElementsType.UnsignedInt;
                _gl.DrawElements(PrimitiveType.Triangles,
                    (uint)_resources.IndexCount,
                    drawType,
                    null);
            }
            else if (_resources.VertexCount > 0)
            {
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_resources.VertexCount);
            }

            _gl.BindVertexArray(0);
            if (renderContext != null)
            {
                renderContext.FrameState.Metrics.DrawCalls += 1;
            }
        }

        public override void Dispose()
        {
            if (_resources == null)
            {
                return;
            }

            if (_resourceManager != null)
            {
                _resourceManager.Release(_resources);
            }

            _resources = null;
            _resourceManager = null;
            _model = null;
            _baseVertices = null;
            _morphedVertices = null;
            _lastAppliedMorphWeights = [];
            _currentMorphWeights = [];
            _baseEmissiveFactor = Vector3.Zero;
            _baseEmissiveIntensity = 1f;
            _gl = null;
        }



        private void ApplyMorphTargetsIfNeeded()
        {
            if (_model == null || _resources == null || _resourceManager == null)
            {
                return;
            }

            if (!_model.HasMorphTargets || _baseVertices == null || _morphedVertices == null)
            {
                return;
            }

            var weights = _currentMorphWeights;
            if (weights == null || weights.Length == 0)
            {
                if (_lastAppliedMorphWeights.Length == 0)
                {
                    return;
                }

                Array.Copy(_baseVertices, _morphedVertices, _baseVertices.Length);
                _resourceManager.UpdateVertexBuffer(_resources, _morphedVertices);
                _lastAppliedMorphWeights = [];
                RestoreMorphDrivenEmissiveFallback();
                return;
            }

            ApplyMorphDrivenEmissiveFallback(weights);

            if (AreWeightsEqual(weights, _lastAppliedMorphWeights))
            {
                return;
            }

            var morphCount = Math.Min(_model.MorphTargets.Length, weights.Length);
            for (var i = 0; i < _baseVertices.Length; i++)
            {
                var baseVertex = _baseVertices[i];
                var position = baseVertex.Position;
                var normal = baseVertex.Normal;

                for (var m = 0; m < morphCount; m++)
                {
                    var w = weights[m];
                    if (MathF.Abs(w) < 0.000001f)
                    {
                        continue;
                    }

                    var target = _model.MorphTargets[m];
                    if (i < target.PositionDeltas.Length)
                    {
                        position += target.PositionDeltas[i] * w;
                    }

                    if (i < target.NormalDeltas.Length)
                    {
                        normal += target.NormalDeltas[i] * w;
                    }
                }

                if (normal != Vector3.Zero)
                {
                    normal = Vector3.Normalize(normal);
                }

                _morphedVertices[i] = new Vertex
                {
                    Position = position,
                    Normal = normal,
                    TexCoord = baseVertex.TexCoord
                };
            }

            _resourceManager.UpdateVertexBuffer(_resources, _morphedVertices);
            _lastAppliedMorphWeights = (float[])weights.Clone();
        }


        private void ApplyMorphDrivenEmissiveFallback(float[] weights)
        {
            if (_model?.Material == null || _model.Material.EmissiveTexture == null || weights == null || weights.Length == 0)
            {
                return;
            }

            var redSignal = Math.Clamp(weights[0], 0f, 1f);
            var dim = 1f - redSignal;
            _model.Material.EmissiveFactor = new Vector3(
                MathF.Max(_baseEmissiveFactor.X, redSignal),
                _baseEmissiveFactor.Y * dim,
                _baseEmissiveFactor.Z * dim);
            _model.Material.EmissiveIntensity = MathF.Max(_baseEmissiveIntensity, _baseEmissiveIntensity + (redSignal * 3f));
        }

        private void RestoreMorphDrivenEmissiveFallback()
        {
            if (_model?.Material == null)
            {
                return;
            }

            _model.Material.EmissiveFactor = _baseEmissiveFactor;
            _model.Material.EmissiveIntensity = _baseEmissiveIntensity;
        }

        private static bool AreWeightsEqual(float[] left, float[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (MathF.Abs(left[i] - right[i]) > 0.000001f)
                {
                    return false;
                }
            }

            return true;
        }

        public static MaterialAlphaMode ResolveAlphaMode(Material? material, float opacity, ShaderRenderMode renderMode)
        {
            if (renderMode == ShaderRenderMode.NormalsDebug)
            {
                return MaterialAlphaMode.Opaque;
            }

            if (renderMode == ShaderRenderMode.Unlit && material?.AlphaMode == MaterialAlphaMode.Blend)
            {
                var hasFactorTransparency = material.Opacity < 0.999f;
                if (!hasFactorTransparency && !material.HasTextureTransparency)
                {
                    return MaterialAlphaMode.Opaque;
                }
            }

            return ResolveAlphaMode(material, opacity);
        }

        public static MaterialAlphaMode ResolveAlphaMode(Material? material, float opacity)
        {
            if (material != null)
            {
                return material.AlphaMode;
            }

            return opacity < 0.999f ? MaterialAlphaMode.Blend : MaterialAlphaMode.Opaque;
        }

        private void ApplyMaterialState(Material? material, ShaderRenderMode renderMode)
        {
            if (_gl == null)
            {
                return;
            }

            var alphaMode = ResolveAlphaMode(material, Opacity, renderMode);
            if (alphaMode == MaterialAlphaMode.Blend)
            {
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                _gl.DepthMask(false);
            }
            else
            {
                _gl.Disable(EnableCap.Blend);
                _gl.DepthMask(true);
            }

            if (material?.DoubleSided == true)
            {
                _gl.Disable(EnableCap.CullFace);
            }
            else
            {
                _gl.Enable(EnableCap.CullFace);
            }
        }

        private void ResetMaterialState(Material? material, ShaderRenderMode renderMode)
        {
            if (_gl == null)
            {
                return;
            }

            if (ResolveAlphaMode(material, Opacity, renderMode) == MaterialAlphaMode.Blend)
            {
                _gl.DepthMask(true);
            }

            if (material?.DoubleSided == true)
            {
                _gl.Enable(EnableCap.CullFace);
            }
        }
    }
}

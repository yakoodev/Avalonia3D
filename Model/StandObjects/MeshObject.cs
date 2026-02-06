using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public class MeshObject : SceneObject, IMaterialProvider
    {
        private RenderResources? _resources;
        private RenderResourceManager? _resourceManager;
        private GL? _gl;
        private Model? _model;

        public Vector3 LocalBoundsMin { get; private set; } = Vector3.Zero;
        public Vector3 LocalBoundsMax { get; private set; } = Vector3.Zero;
        public bool HasGeometryBounds { get; private set; }

        public void AssignModel(Model model)
        {
            _model = model;

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
                Opacity = model.Material.Opacity;
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

            var shader = renderContext.Scene.ShaderSelectionPolicy.Select(Material, renderContext.Scene, _gl);
            if (shader == null)
            {
                return;
            }

            ApplyMaterialState(Material);

            shader.Use();
            shader.BindMaterial(_resources, Material, renderContext.FrameState.ShadowMapId);
            shader.SetUniforms(renderContext, this, renderContext.FrameState.LightSpaceMatrix);
            RenderModel(renderContext);

            ResetMaterialState(Material);
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
            _gl = null;
        }

        public static MaterialAlphaMode ResolveAlphaMode(Material? material, float opacity)
        {
            if (material != null)
            {
                return material.AlphaMode;
            }

            return opacity < 0.999f ? MaterialAlphaMode.Blend : MaterialAlphaMode.Opaque;
        }

        private void ApplyMaterialState(Material? material)
        {
            if (_gl == null)
            {
                return;
            }

            var alphaMode = ResolveAlphaMode(material, Opacity);
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

        private void ResetMaterialState(Material? material)
        {
            if (_gl == null)
            {
                return;
            }

            if (ResolveAlphaMode(material, Opacity) == MaterialAlphaMode.Blend)
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

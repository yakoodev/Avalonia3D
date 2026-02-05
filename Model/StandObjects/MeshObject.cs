using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System.Linq;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public class MeshObject : SceneObject, IMaterialProvider
    {
        private RenderResources? _resources;
        private RenderResourceManager? _resourceManager;
        private GL? _gl;
        private Model? _model;

        public void AssignModel(Model model)
        {
            _model = model;

            if (model?.Vertices == null || model.Vertices.Length == 0)
            {
                Gravity = Vector3.Zero;
                return;
            }

            Gravity = GetCenterOfGravity(model.Vertices);

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

        public override unsafe void Render(IRenderContext renderContext)
        {
            if (_gl == null || _resources == null)
            {
                return;
            }

            var shader = Material?.Shader as IShader3D ?? renderContext.Scene.Shaders.FirstOrDefault();
            if (shader == null)
            {
                return;
            }

            ApplyMaterialState(Material);

            shader.Use();
            shader.BindMaterial(_resources, Material, renderContext.FrameState.ShadowMapId);
            shader.SetUniforms(renderContext, this, renderContext.FrameState.LightSpaceMatrix);
            RenderModel();

            ResetMaterialState(Material);
        }

        public unsafe void RenderModel()
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

        private void ApplyMaterialState(Material? material)
        {
            if (_gl == null)
            {
                return;
            }

            bool transparent = material?.IsTransparent == true || Opacity < 1f;
            if (transparent)
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
        }

        private void ResetMaterialState(Material? material)
        {
            if (_gl == null)
            {
                return;
            }

            if (material?.IsTransparent == true || Opacity < 1f)
            {
                _gl.DepthMask(true);
            }
        }
    }
}

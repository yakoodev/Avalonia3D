using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public class MeshObject : SceneObject
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
        }

        public void BuildRenderResources(RenderResourceManager resourceManager)
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

            foreach (var shader in renderContext.Scene.Shaders)
            {
                shader.BindTexture(_resources.TextureId);
                shader.SetUniforms(renderContext, this);
                RenderModel();
            }
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
    }
}

using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Rendering
{
    public interface IRenderPass
    {
        string Name { get; }
        void Execute(RenderPipelineContext context);
    }

    public sealed class RenderPipelineContext
    {
        public RenderPipelineContext(
            IRenderContext renderContext,
            int width,
            int height,
            IReadOnlyList<MeshObject> opaqueObjects,
            IReadOnlyList<MeshObject> transparentObjects,
            IReadOnlyList<MeshObject> allObjects)
        {
            RenderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            Width = width;
            Height = height;
            OpaqueObjects = opaqueObjects ?? throw new ArgumentNullException(nameof(opaqueObjects));
            TransparentObjects = transparentObjects ?? throw new ArgumentNullException(nameof(transparentObjects));
            AllObjects = allObjects ?? throw new ArgumentNullException(nameof(allObjects));
        }

        public IRenderContext RenderContext { get; }
        public GL Gl => RenderContext.GL ?? throw new InvalidOperationException("OpenGL context is not initialized.");
        public Scene3D Scene => RenderContext.Scene;
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MeshObject> OpaqueObjects { get; }
        public IReadOnlyList<MeshObject> TransparentObjects { get; }
        public IReadOnlyList<MeshObject> AllObjects { get; }
    }

    public sealed class RenderPipeline
    {
        private readonly RenderPipelineFactory _factory;
        private List<IRenderPass> _passes;

        public RenderPipeline(GraphicsProfile? profile = null, RenderPipelineFactory? factory = null)
        {
            _factory = factory ?? new RenderPipelineFactory();
            Profile = (profile ?? GraphicsProfile.Medium).Validate();
            _passes = new List<IRenderPass>(_factory.CreatePasses(Profile));
        }

        public GraphicsProfile Profile { get; private set; }
        public RenderQualitySettings Settings => RenderQualitySettings.FromProfile(Profile);
        public IReadOnlyList<IRenderPass> Passes => _passes;

        public void ApplyProfile(GraphicsProfile profile)
        {
            Profile = profile.Validate();
            _passes = new List<IRenderPass>(_factory.CreatePasses(Profile));
        }

        public void Execute(IRenderContext renderContext, int width, int height)
        {
            if (renderContext?.GL == null)
            {
                return;
            }

            renderContext.Scene.UpdateFrame();

            var allObjects = CollectMeshObjects(renderContext.Scene.SceneGraph.RootObjects);
            var opaqueObjects = new List<MeshObject>();
            var transparentObjects = new List<MeshObject>();

            foreach (var obj in allObjects)
            {
                if (IsTransparent(obj))
                {
                    transparentObjects.Add(obj);
                }
                else
                {
                    opaqueObjects.Add(obj);
                }
            }

            SortTransparentObjects(renderContext.Scene.Camera, transparentObjects);

            var context = new RenderPipelineContext(
                renderContext,
                width,
                height,
                opaqueObjects,
                transparentObjects,
                allObjects);

            foreach (var pass in _passes)
            {
                pass.Execute(context);
            }
        }

        private static List<MeshObject> CollectMeshObjects(IReadOnlyList<SceneObject> roots)
        {
            var result = new List<MeshObject>();
            foreach (var obj in roots)
            {
                CollectMeshObjects(obj, result);
            }

            return result;
        }

        private static void CollectMeshObjects(SceneObject obj, List<MeshObject> result)
        {
            if (obj == null || !obj.IsVisible)
            {
                return;
            }

            if (obj is MeshGroup group)
            {
                foreach (var child in group)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    child.BaseColor = group.BaseColor;
                    child.EmissionColor = group.EmissionColor;
                    CollectMeshObjects(child, result);
                }

                return;
            }

            if (obj is MeshObject mesh)
            {
                result.Add(mesh);
            }
        }

        private static bool IsTransparent(MeshObject obj)
        {
            var material = obj.Material;
            return material?.IsTransparent == true || obj.Opacity < 1f;
        }

        private static void SortTransparentObjects(Camera camera, List<MeshObject> transparentObjects)
        {
            if (transparentObjects.Count <= 1)
            {
                return;
            }

            var cameraPos = camera.Position;
            transparentObjects.Sort((a, b) =>
            {
                var distA = GetSquaredDistance(cameraPos, a.CreateModelMatrix());
                var distB = GetSquaredDistance(cameraPos, b.CreateModelMatrix());
                return distB.CompareTo(distA);
            });
        }

        private static float GetSquaredDistance(Vector3 cameraPos, Matrix4x4 modelMatrix)
        {
            var position = new Vector3(modelMatrix.M41, modelMatrix.M42, modelMatrix.M43);
            var delta = position - cameraPos;
            return delta.LengthSquared();
        }
    }
}

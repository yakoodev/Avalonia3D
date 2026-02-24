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
            IReadOnlyList<MeshObject> allObjects,
            int culledObjects)
        {
            RenderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            Width = width;
            Height = height;
            OpaqueObjects = opaqueObjects ?? throw new ArgumentNullException(nameof(opaqueObjects));
            TransparentObjects = transparentObjects ?? throw new ArgumentNullException(nameof(transparentObjects));
            AllObjects = allObjects ?? throw new ArgumentNullException(nameof(allObjects));
            CulledObjects = culledObjects;
        }

        public IRenderContext RenderContext { get; }
        public GL Gl => RenderContext.GL ?? throw new InvalidOperationException("OpenGL context is not initialized.");
        public Scene3D Scene => RenderContext.Scene;
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MeshObject> OpaqueObjects { get; }
        public IReadOnlyList<MeshObject> TransparentObjects { get; }
        public IReadOnlyList<MeshObject> AllObjects { get; }
        public int CulledObjects { get; }
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
        public bool EnableFrustumCulling { get; set; } = true;

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
            renderContext.FrameState.Metrics.Reset();

            var allObjects = CollectMeshObjects(renderContext.Scene.SceneGraph.RootObjects);
            var culledObjects = 0;
            var visibleObjects = EnableFrustumCulling
                ? FrustumCullMeshObjects(renderContext.Scene.Camera, allObjects, out culledObjects)
                : new List<MeshObject>(allObjects);
            var opaqueObjects = new List<MeshObject>();
            var transparentObjects = new List<MeshObject>();

            foreach (var obj in visibleObjects)
            {
                if (IsTransparent(obj, renderContext.Scene.RenderMode))
                {
                    transparentObjects.Add(obj);
                }
                else
                {
                    opaqueObjects.Add(obj);
                }
            }

            renderContext.FrameState.Metrics.CulledObjects = culledObjects;

            SortTransparentObjects(renderContext.Scene.Camera, transparentObjects);

            var context = new RenderPipelineContext(
                renderContext,
                width,
                height,
                opaqueObjects,
                transparentObjects,
                visibleObjects,
                culledObjects);

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

        private static List<MeshObject> FrustumCullMeshObjects(Camera camera, IReadOnlyList<MeshObject> objects, out int culledObjects)
        {
            var result = new List<MeshObject>(objects.Count);
            var frustum = BuildFrustumPlanes(camera.View * camera.Projection);

            int culled = 0;
            foreach (var mesh in objects)
            {
                if (mesh.HasGeometryBounds && !IsVisibleInFrustum(mesh, frustum))
                {
                    culled++;
                    continue;
                }

                result.Add(mesh);
            }

            culledObjects = culled;
            return result;
        }

        private static bool IsVisibleInFrustum(MeshObject mesh, Plane[] frustum)
        {
            var modelMatrix = mesh.CreateModelMatrix();
            var (worldMin, worldMax) = TransformAabb(mesh.LocalBoundsMin, mesh.LocalBoundsMax, modelMatrix);

            for (int i = 0; i < frustum.Length; i++)
            {
                if (!IntersectsAabb(frustum[i], worldMin, worldMax))
                {
                    return false;
                }
            }

            return true;
        }

        private static (Vector3 Min, Vector3 Max) TransformAabb(Vector3 localMin, Vector3 localMax, Matrix4x4 transform)
        {
            Vector3[] corners =
            {
                new(localMin.X, localMin.Y, localMin.Z),
                new(localMax.X, localMin.Y, localMin.Z),
                new(localMin.X, localMax.Y, localMin.Z),
                new(localMax.X, localMax.Y, localMin.Z),
                new(localMin.X, localMin.Y, localMax.Z),
                new(localMax.X, localMin.Y, localMax.Z),
                new(localMin.X, localMax.Y, localMax.Z),
                new(localMax.X, localMax.Y, localMax.Z)
            };

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (var corner in corners)
            {
                var world = Vector3.Transform(corner, transform);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }

            return (min, max);
        }

        private static bool IntersectsAabb(Plane plane, Vector3 min, Vector3 max)
        {
            var positiveVertex = new Vector3(
                plane.Normal.X >= 0 ? max.X : min.X,
                plane.Normal.Y >= 0 ? max.Y : min.Y,
                plane.Normal.Z >= 0 ? max.Z : min.Z);

            return Plane.DotCoordinate(plane, positiveVertex) >= 0;
        }

        private static Plane[] BuildFrustumPlanes(Matrix4x4 matrix)
        {
            return new[]
            {
                Plane.Normalize(new Plane(matrix.M14 + matrix.M11, matrix.M24 + matrix.M21, matrix.M34 + matrix.M31, matrix.M44 + matrix.M41)),
                Plane.Normalize(new Plane(matrix.M14 - matrix.M11, matrix.M24 - matrix.M21, matrix.M34 - matrix.M31, matrix.M44 - matrix.M41)),
                Plane.Normalize(new Plane(matrix.M14 + matrix.M12, matrix.M24 + matrix.M22, matrix.M34 + matrix.M32, matrix.M44 + matrix.M42)),
                Plane.Normalize(new Plane(matrix.M14 - matrix.M12, matrix.M24 - matrix.M22, matrix.M34 - matrix.M32, matrix.M44 - matrix.M42)),
                Plane.Normalize(new Plane(matrix.M13, matrix.M23, matrix.M33, matrix.M43)),
                Plane.Normalize(new Plane(matrix.M14 - matrix.M13, matrix.M24 - matrix.M23, matrix.M34 - matrix.M33, matrix.M44 - matrix.M43))
            };
        }

        private static bool IsTransparent(MeshObject obj, ShaderRenderMode renderMode)
        {
            var material = obj.Material;
            var resolvedAlpha = MeshObject.ResolveAlphaMode(material, obj.Opacity, renderMode);
            return resolvedAlpha == MaterialAlphaMode.Blend;
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

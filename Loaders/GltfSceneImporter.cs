using Avalonia3D.Helpers;
using Avalonia3D.Memory;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Serilog;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Loaders
{
    public class GltfSceneImporter
    {
        private readonly Dictionary<string, List<Model.Model>> _modelCache = new(StringComparer.OrdinalIgnoreCase);

        public SceneGraph Import(string gltfPath)
        {
            if (string.IsNullOrWhiteSpace(gltfPath))
            {
                throw new ArgumentException("GLTF path is empty.", nameof(gltfPath));
            }

            if (!File.Exists(gltfPath))
            {
                throw new FileNotFoundException($"Model file not found: {gltfPath}");
            }

            Log.Information($"Loading GLTF scene: {gltfPath}");
            MemoryManager.LogMemoryState("Before GLTF load");

            var fileSize = new FileInfo(gltfPath).Length;
            if (fileSize > 5 * 1024 * 1024)
            {
                MemoryManager.OptimizeForLargeModel();
            }

            try
            {
                var gltf = ModelRoot.Load(gltfPath);
                return Import(gltf);
            }
            finally
            {
                if (fileSize > 5 * 1024 * 1024)
                {
                    MemoryManager.RestoreNormalSettings();
                }
            }
        }

        public SceneGraph Import(ModelRoot gltf)
        {
            var graph = new SceneGraph();
            if (gltf == null)
            {
                return graph;
            }

            var scenes = gltf.LogicalScenes;
            if (scenes.Count > 0)
            {
                foreach (var scene in scenes)
                {
                    foreach (var root in scene.VisualChildren)
                    {
                        BuildNode(graph, null, root);
                    }
                }
            }
            else
            {
                foreach (var node in gltf.LogicalNodes)
                {
                    BuildNode(graph, null, node);
                }
            }

            ModelLoader.ClearAllCaches();
            MemoryManager.PerformAggressiveCleanup();
            MemoryManager.LogMemoryState("After GLTF load");
            return graph;
        }

        public void ImportInto(string gltfPath, MeshGroup target)
        {
            if (string.IsNullOrWhiteSpace(gltfPath))
            {
                throw new ArgumentException("GLTF path is empty.", nameof(gltfPath));
            }

            if (target == null)
            {
                return;
            }

            if (!File.Exists(gltfPath))
            {
                throw new FileNotFoundException($"Model file not found: {gltfPath}");
            }

            var models = LoadModelsForPath(gltfPath);
            foreach (var model in models)
            {
                var meshObject = CreateMeshObject(model, applyLocalMatrix: true);
                target.Add(meshObject);
            }

            ModelLoader.ClearAllCaches();
            MemoryManager.PerformAggressiveCleanup();
        }

        private MeshGroup BuildNode(SceneGraph graph, MeshGroup? parent, Node node)
        {
            var group = new MeshGroup
            {
                Name = string.IsNullOrWhiteSpace(node.Name) ? $"Node_{node.GetHashCode()}" : node.Name
            };

            ApplyTransform(group, node.LocalMatrix);

            if (parent == null)
            {
                graph.AddRoot(group);
            }
            else
            {
                parent.Add(group);
            }

            if (node.Mesh != null)
            {
                foreach (var model in ModelLoader.LoadModelsForNode(node))
                {
                    var meshObject = CreateMeshObject(model, applyLocalMatrix: false);
                    group.Add(meshObject);
                }
            }

            foreach (var child in node.VisualChildren)
            {
                BuildNode(graph, group, child);
            }

            return group;
        }

        private List<Model.Model> LoadModelsForPath(string gltfPath)
        {
            if (!_modelCache.TryGetValue(gltfPath, out var models))
            {
                var gltf = ModelRoot.Load(gltfPath);
                models = ModelLoader.LoadModels(gltf);
                _modelCache[gltfPath] = models;
            }

            return models;
        }

        private static void ApplyTransform(SceneObject obj, Matrix4x4 matrix)
        {
            obj.Position = matrix.Translation;
            obj.Scale = matrix.GetScale();
            obj.Rotation = matrix.GetRotation();
        }

        private static MeshObject CreateMeshObject(Model.Model model, bool applyLocalMatrix)
        {
            var meshObject = new MeshObject
            {
                Name = model.Name
            };

            if (applyLocalMatrix)
            {
                ApplyTransform(meshObject, model.LocalMatrix);
            }

            meshObject.AssignModel(model);
            return meshObject;
        }
    }
}

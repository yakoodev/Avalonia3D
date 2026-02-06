using Avalonia3D.Animation;
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
    public sealed class SceneImportResult
    {
        public SceneImportResult(SceneGraph graph, IReadOnlyList<AnimationClip> clips)
        {
            Graph = graph;
            Clips = clips;
        }

        public SceneGraph Graph { get; }
        public IReadOnlyList<AnimationClip> Clips { get; }
    }

    public class GltfSceneImporter
    {
        private readonly Dictionary<string, List<Model.Model>> _modelCache = new(StringComparer.OrdinalIgnoreCase);

        public SceneGraph Import(string gltfPath)
        {
            return ImportWithAnimations(gltfPath).Graph;
        }

        public SceneImportResult ImportWithAnimations(string gltfPath)
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
                return ImportWithAnimations(gltf);
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
            return ImportWithAnimations(gltf).Graph;
        }

        public SceneImportResult ImportWithAnimations(ModelRoot gltf)
        {
            var graph = new SceneGraph();
            if (gltf == null)
            {
                return new SceneImportResult(graph, []);
            }

            var nodeKeys = new Dictionary<Node, string>();
            var scenes = gltf.LogicalScenes;
            if (scenes.Count > 0)
            {
                foreach (var scene in scenes)
                {
                    foreach (var root in scene.VisualChildren)
                    {
                        BuildNode(graph, null, root, nodeKeys);
                    }
                }
            }
            else
            {
                foreach (var node in gltf.LogicalNodes)
                {
                    BuildNode(graph, null, node, nodeKeys);
                }
            }

            var clips = ExtractAnimationClips(gltf, nodeKeys);

            ModelLoader.ClearAllCaches();
            MemoryManager.PerformAggressiveCleanup();
            MemoryManager.LogMemoryState("After GLTF load");
            return new SceneImportResult(graph, clips);
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

        private MeshGroup BuildNode(SceneGraph graph, MeshGroup? parent, Node node, Dictionary<Node, string> nodeKeys)
        {
            var group = new MeshGroup
            {
                Name = string.IsNullOrWhiteSpace(node.Name) ? $"Node_{node.LogicalIndex}" : node.Name
            };

            var stableId = $"node:{node.LogicalIndex}";
            group.Node.StableId = stableId;

            ApplyTransform(group, node.LocalMatrix);

            if (parent == null)
            {
                graph.AddRoot(group);
            }
            else
            {
                parent.Add(group);
            }

            nodeKeys[node] = stableId;

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
                BuildNode(graph, group, child, nodeKeys);
            }

            return group;
        }

        private static List<AnimationClip> ExtractAnimationClips(ModelRoot gltf, IReadOnlyDictionary<Node, string> nodeKeys)
        {
            var clips = new List<AnimationClip>();
            foreach (var animation in gltf.LogicalAnimations)
            {
                var clipName = string.IsNullOrWhiteSpace(animation.Name)
                    ? $"Animation_{animation.LogicalIndex}"
                    : animation.Name;

                var clip = new AnimationClip(clipName);

                foreach (var channel in animation.Channels)
                {
                    if (channel.TargetNode == null)
                    {
                        continue;
                    }

                    if (!nodeKeys.TryGetValue(channel.TargetNode, out var targetNodeKey))
                    {
                        continue;
                    }

                    var targetProperty = MapTargetProperty(channel.TargetNodePath);
                    if (targetProperty == null)
                    {
                        continue;
                    }

                    var animChannel = new Avalonia3D.Animation.AnimationChannel(targetNodeKey, targetProperty.Value);

                    switch (targetProperty.Value)
                    {
                        case AnimationTargetProperty.Position:
                            foreach (var (time, value) in channel.GetTranslationSampler().GetLinearKeys())
                            {
                                animChannel.AddKeyframe(time, value);
                            }
                            break;
                        case AnimationTargetProperty.Scale:
                            foreach (var (time, value) in channel.GetScaleSampler().GetLinearKeys())
                            {
                                animChannel.AddKeyframe(time, value);
                            }
                            break;
                        case AnimationTargetProperty.Rotation:
                            foreach (var (time, value) in channel.GetRotationSampler().GetLinearKeys())
                            {
                                animChannel.AddKeyframe(time, value);
                            }
                            break;
                    }

                    if (animChannel.HasData)
                    {
                        clip.Channels.Add(animChannel);
                    }
                }

                if (clip.Channels.Count > 0)
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        private static AnimationTargetProperty? MapTargetProperty(PropertyPath path)
        {
            return path switch
            {
                PropertyPath.translation => AnimationTargetProperty.Position,
                PropertyPath.rotation => AnimationTargetProperty.Rotation,
                PropertyPath.scale => AnimationTargetProperty.Scale,
                _ => null
            };
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

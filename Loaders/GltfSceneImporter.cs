using Avalonia3D.Animation;
using Avalonia3D.Helpers;
using Avalonia3D.Memory;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Serilog;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json.Nodes;

namespace Avalonia3D.Loaders
{
    public sealed class SceneImportResult
    {
        public SceneImportResult(SceneGraph graph, IReadOnlyList<AnimationClip> clips, SceneImportStatus status, IReadOnlyList<string>? issues = null)
        {
            Graph = graph;
            Clips = clips;
            Status = status;
            Issues = issues ?? [];
        }

        public SceneGraph Graph { get; }
        public IReadOnlyList<AnimationClip> Clips { get; }
        public SceneImportStatus Status { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool IsDegraded => Status == SceneImportStatus.Degraded;
    }

    public enum SceneImportStatus
    {
        Success,
        Degraded
    }

    public class GltfSceneImporter
    {
        private readonly Dictionary<string, List<Model.Model>> _modelCache = new(StringComparer.OrdinalIgnoreCase);
        public ImportValidationPolicy ValidationPolicy { get; set; } = ImportValidationPolicy.RelaxedWithWarnings;

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
                var loaded = LoadModelRoot(gltfPath, ValidationPolicy);
                var importResult = loaded.Model != null
                    ? ImportWithAnimations(loaded.Model)
                    : new SceneImportResult(new SceneGraph(), [], SceneImportStatus.Success);

                return new SceneImportResult(importResult.Graph, importResult.Clips, loaded.Status, loaded.Issues);
            }
            finally
            {
                if (fileSize > 5 * 1024 * 1024)
                {
                    MemoryManager.RestoreNormalSettings();
                }
            }
        }

        private static ModelLoadOutcome LoadModelRoot(string gltfPath, ImportValidationPolicy validationPolicy)
        {
            var missingDependencies = GltfDependencyInspector.GetMissingDependencies(gltfPath);

            try
            {
                var model = ModelRoot.Load(gltfPath);
                return new ModelLoadOutcome(model, SceneImportStatus.Success, []);
            }
            catch (Exception strictException)
            {
                var issues = BuildIssues(strictException, missingDependencies);

                if (validationPolicy == ImportValidationPolicy.Strict)
                {
                    throw CreateStrictImportException(gltfPath, issues, strictException);
                }

                Log.Warning("Strict glTF validation failed for {Path}. Running in relaxed mode. Issues: {Issues}", gltfPath, string.Join(" | ", issues));

                var relaxedSettings = new ReadSettings
                {
                    Validation = ValidationMode.Skip
                };

                try
                {
                    var model = ModelRoot.Load(gltfPath, relaxedSettings);
                    Log.Warning("GLTF loaded with degraded quality for {Path}.", gltfPath);
                    return new ModelLoadOutcome(model, SceneImportStatus.Degraded, issues);
                }
                catch (Exception relaxedException)
                {
                    issues.Add($"Relaxed import also failed: {relaxedException.Message}");
                    Log.Error(relaxedException, "Relaxed import failed for {Path}. Returning degraded empty scene.", gltfPath);
                    return new ModelLoadOutcome(null, SceneImportStatus.Degraded, issues);
                }
            }
        }

        private static InvalidDataException CreateStrictImportException(string gltfPath, IReadOnlyList<string> issues, Exception innerException)
        {
            var message = $"GLTF import failed in strict mode for '{gltfPath}'. Problematic assets/issues:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", issues)}";
            return new InvalidDataException(message, innerException);
        }

        private static List<string> BuildIssues(Exception exception, IReadOnlyList<string> missingDependencies)
        {
            var issues = new List<string>
            {
                $"Validation/load: {exception.Message}"
            };

            foreach (var dependency in missingDependencies)
            {
                issues.Add($"Missing dependency: {dependency}");
            }

            return issues;
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
                return new SceneImportResult(graph, [], SceneImportStatus.Success);
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
            return new SceneImportResult(graph, clips, SceneImportStatus.Success);
        }

        private sealed record ModelLoadOutcome(ModelRoot? Model, SceneImportStatus Status, IReadOnlyList<string> Issues);

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

            var externalId = TryGetExternalId(node);
            var stableId = !string.IsNullOrWhiteSpace(externalId)
                ? externalId!
                : $"node:{node.LogicalIndex}";

            group.Node.StableId = stableId;
            group.Node.ExternalId = externalId;
            group.Node.SemanticId = TryGetSemanticId(node);

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

        private static string? TryGetExternalId(Node node)
        {
            return TryGetFirstStringFromExtras(node, "externalId", "external_id", "stableId", "stable_id", "id", "guid", "uuid");
        }

        private static string? TryGetSemanticId(Node node)
        {
            return TryGetFirstStringFromExtras(node, "semanticId", "semantic_id", "path", "nodePath", "node_path", "key");
        }

        private static string? TryGetFirstStringFromExtras(Node node, params string[] keys)
        {
            if (node.Extras is not JsonObject extras)
            {
                return null;
            }

            foreach (var key in keys)
            {
                if (!extras.TryGetPropertyValue(key, out var valueNode) || valueNode is null)
                {
                    continue;
                }

                if (valueNode is JsonValue value && value.TryGetValue<string>(out var stringValue) && !string.IsNullOrWhiteSpace(stringValue))
                {
                    return stringValue;
                }
            }

            return null;
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

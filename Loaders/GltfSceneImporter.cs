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
                    var onlyBenignIssues = HasOnlyBenignValidationIssues(issues);
                    if (onlyBenignIssues)
                    {
                        Log.Warning("GLTF loaded in compatibility mode for {Path}. Non-critical validation issues were ignored.", gltfPath);
                        return new ModelLoadOutcome(model, SceneImportStatus.Success, issues);
                    }

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


        private static bool HasOnlyBenignValidationIssues(IReadOnlyList<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return false;
            }

            foreach (var issue in issues)
            {
                if (string.IsNullOrWhiteSpace(issue))
                {
                    continue;
                }

                if (issue.StartsWith("Missing dependency:", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var normalized = issue.Replace("\r", string.Empty).Replace("\n", " ");
                if (normalized.Contains("AnimationSampler", StringComparison.OrdinalIgnoreCase) &&
                    normalized.Contains("_byteStride: must NOT be defined", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return false;
            }

            return true;
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

            var materialTargetMap = BuildMaterialTargetMap(gltf, nodeKeys);
            var clips = ExtractAnimationClips(gltf, nodeKeys, materialTargetMap);

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

        private static IReadOnlyDictionary<int, IReadOnlyList<string>> BuildMaterialTargetMap(ModelRoot gltf, IReadOnlyDictionary<Node, string> nodeKeys)
        {
            var map = new Dictionary<int, HashSet<string>>();

            foreach (var node in gltf.LogicalNodes)
            {
                if (node.Mesh == null)
                {
                    continue;
                }

                if (!nodeKeys.TryGetValue(node, out var nodeKey))
                {
                    continue;
                }

                foreach (var primitive in node.Mesh.Primitives)
                {
                    var materialIndex = primitive.Material?.LogicalIndex;
                    if (materialIndex == null)
                    {
                        continue;
                    }

                    if (!map.TryGetValue(materialIndex.Value, out var targets))
                    {
                        targets = new HashSet<string>(StringComparer.Ordinal);
                        map[materialIndex.Value] = targets;
                    }

                    targets.Add(nodeKey);
                }
            }

            var readonlyMap = new Dictionary<int, IReadOnlyList<string>>();
            foreach (var (materialIndex, targets) in map)
            {
                readonlyMap[materialIndex] = [.. targets];
            }

            return readonlyMap;
        }

        private static List<AnimationClip> ExtractAnimationClips(ModelRoot gltf, IReadOnlyDictionary<Node, string> nodeKeys, IReadOnlyDictionary<int, IReadOnlyList<string>> materialTargetMap)
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
                    var descriptor = ParseChannelTarget(channel);
                    if (descriptor == null)
                    {
                        Log.Debug("Skipping unsupported GLTF animation channel '{PointerPath}' in clip '{Clip}'. NodePath='{NodePath}' PointerPath='{TargetPointerPath}'",
                            channel.TargetPointerPath,
                            clipName,
                            channel.TargetNodePath,
                            channel.TargetPointerPath);
                        continue;
                    }

                    var resolved = descriptor.Value;
                    var bindingSpecs = ResolveTargetBindings(channel, resolved, nodeKeys, materialTargetMap);
                    if (bindingSpecs.Count == 0)
                    {
                        continue;
                    }

                    foreach (var spec in bindingSpecs)
                    {
                        var animChannel = new Avalonia3D.Animation.AnimationChannel(spec.ChannelTargetKey, resolved.TargetProperty)
                        {
                            Binding = spec.Binding
                        };

                        ExtractChannelKeys(channel, resolved.TargetProperty, animChannel);
                        if (animChannel.HasData)
                        {
                            clip.Channels.Add(animChannel);
                        }
                    }
                }

                if (clip.Channels.Count > 0)
                {
                    clips.Add(clip);
                }
                else if (animation.Channels.Count > 0)
                {
                    Log.Warning("GLTF animation '{AnimationName}' has {ChannelCount} channels, but none could be extracted into Avalonia3D clip channels.",
                        clipName,
                        animation.Channels.Count);
                }
            }

            return clips;
        }

        private static IReadOnlyList<ResolvedBindingSpec> ResolveTargetBindings(
            SharpGLTF.Schema2.AnimationChannel channel,
            ChannelTargetDescriptor descriptor,
            IReadOnlyDictionary<Node, string> nodeKeys,
            IReadOnlyDictionary<int, IReadOnlyList<string>> materialTargetMap)
        {
            switch (descriptor.Kind)
            {
                case AnimationTargetKind.NodeTransform:
                    if (channel.TargetNode != null && nodeKeys.TryGetValue(channel.TargetNode, out var nodeKey))
                    {
                        return [new ResolvedBindingSpec(nodeKey, new NodeTransformBinding(nodeKey, descriptor.TargetProperty))];
                    }
                    return [];

                case AnimationTargetKind.NodeMorph:
                    if (channel.TargetNode != null && nodeKeys.TryGetValue(channel.TargetNode, out var morphNodeKey))
                    {
                        return [new ResolvedBindingSpec(morphNodeKey, new NodeMorphBinding(morphNodeKey))];
                    }
                    return [];

                case AnimationTargetKind.MaterialProperty:
                case AnimationTargetKind.TextureProperty:
                    var materialIndex = TryParseMaterialIndex(channel.TargetPointerPath);
                    if (materialIndex == null)
                    {
                        return [];
                    }

                    if (!materialTargetMap.TryGetValue(materialIndex.Value, out var mappedNodeKeys) || mappedNodeKeys.Count == 0)
                    {
                        return [];
                    }

                    var materialKey = $"material:{materialIndex.Value}";
                    var specs = new List<ResolvedBindingSpec>(mappedNodeKeys.Count);
                    foreach (var mappedNodeKey in mappedNodeKeys)
                    {
                        IAnimationTargetBinding binding = descriptor.Kind == AnimationTargetKind.MaterialProperty
                            ? new MaterialPropertyBinding(materialKey, descriptor.TargetProperty)
                            : new TexturePropertyBinding(materialKey, descriptor.TextureSlot!.Value, descriptor.TargetProperty);
                        specs.Add(new ResolvedBindingSpec(mappedNodeKey, binding));
                    }

                    return specs;

                default:
                    return [];
            }
        }

        private static int? TryParseMaterialIndex(string? pointerPath)
        {
            if (string.IsNullOrWhiteSpace(pointerPath))
            {
                return null;
            }

            const string prefix = "/materials/";
            var start = pointerPath.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            var indexStart = start + prefix.Length;
            var indexEnd = pointerPath.IndexOf('/', indexStart);
            var token = indexEnd > indexStart
                ? pointerPath[indexStart..indexEnd]
                : pointerPath[indexStart..];

            return int.TryParse(token, out var materialIndex)
                ? materialIndex
                : null;
        }

        private static void ExtractChannelKeys(SharpGLTF.Schema2.AnimationChannel sourceChannel, AnimationTargetProperty targetProperty, Avalonia3D.Animation.AnimationChannel targetChannel)
        {
            switch (targetProperty)
            {
                case AnimationTargetProperty.Position:
                    foreach (var (time, value) in sourceChannel.GetTranslationSampler().GetLinearKeys())
                    {
                        targetChannel.AddKeyframe(time, value);
                    }
                    break;
                case AnimationTargetProperty.Scale:
                    foreach (var (time, value) in sourceChannel.GetScaleSampler().GetLinearKeys())
                    {
                        targetChannel.AddKeyframe(time, value);
                    }
                    break;
                case AnimationTargetProperty.Rotation:
                    foreach (var (time, value) in sourceChannel.GetRotationSampler().GetLinearKeys())
                    {
                        targetChannel.AddKeyframe(time, value);
                    }
                    break;
                case AnimationTargetProperty.EmissiveColor:
                case AnimationTargetProperty.BaseColorFactor:
                case AnimationTargetProperty.TextureTransformOffset:
                case AnimationTargetProperty.TextureTransformScale:
                    if (TryExtractVector3PointerKeys(sourceChannel, out var colorKeys))
                    {
                        foreach (var (time, value) in colorKeys)
                        {
                            targetChannel.AddKeyframe(time, value);
                        }
                    }
                    break;
                case AnimationTargetProperty.EmissiveIntensity:
                case AnimationTargetProperty.TextureTransformRotation:
                case AnimationTargetProperty.TextureTransformTexCoord:
                    if (TryExtractFloatPointerKeys(sourceChannel, out var intensityKeys))
                    {
                        foreach (var (time, value) in intensityKeys)
                        {
                            targetChannel.AddKeyframe(time, value);
                        }
                    }
                    break;
                case AnimationTargetProperty.MorphWeights:
                    foreach (var (time, value) in sourceChannel.GetMorphSampler().GetLinearKeys())
                    {
                        targetChannel.AddKeyframe(time, value);
                    }
                    break;
            }
        }

        private static ChannelTargetDescriptor? ParseChannelTarget(SharpGLTF.Schema2.AnimationChannel channel)
        {
            var nodePathTarget = MapNodeTargetProperty(channel.TargetNodePath);
            if (nodePathTarget != null)
            {
                return nodePathTarget;
            }

            return MapPointerTargetProperty(channel.TargetPointerPath);
        }

        private static ChannelTargetDescriptor? MapNodeTargetProperty(PropertyPath path)
        {
            return path switch
            {
                PropertyPath.translation => new ChannelTargetDescriptor(AnimationTargetKind.NodeTransform, AnimationTargetProperty.Position),
                PropertyPath.rotation => new ChannelTargetDescriptor(AnimationTargetKind.NodeTransform, AnimationTargetProperty.Rotation),
                PropertyPath.scale => new ChannelTargetDescriptor(AnimationTargetKind.NodeTransform, AnimationTargetProperty.Scale),
                PropertyPath.weights => new ChannelTargetDescriptor(AnimationTargetKind.NodeMorph, AnimationTargetProperty.MorphWeights),
                _ => null
            };
        }

        private static readonly (string PathToken, TextureSlot Slot)[] TextureSlotPathRegistry =
        [
            ("/pbrMetallicRoughness/baseColorTexture", TextureSlot.BaseColor),
            ("/emissiveTexture", TextureSlot.Emissive),
            ("/normalTexture", TextureSlot.Normal),
            ("/occlusionTexture", TextureSlot.Occlusion),
            ("/pbrMetallicRoughness/metallicRoughnessTexture", TextureSlot.MetallicRoughness)
        ];

        private static readonly (string Suffix, AnimationTargetProperty Property)[] TextureTransformPropertyRegistry =
        [
            ("/offset", AnimationTargetProperty.TextureTransformOffset),
            ("/scale", AnimationTargetProperty.TextureTransformScale),
            ("/rotation", AnimationTargetProperty.TextureTransformRotation),
            ("/texCoord", AnimationTargetProperty.TextureTransformTexCoord)
        ];

        private static ChannelTargetDescriptor? MapPointerTargetProperty(string? pointerPath)
        {
            if (string.IsNullOrWhiteSpace(pointerPath) || !pointerPath.Contains("/materials/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (pointerPath.EndsWith("/emissiveFactor", StringComparison.OrdinalIgnoreCase))
            {
                return new ChannelTargetDescriptor(AnimationTargetKind.MaterialProperty, AnimationTargetProperty.EmissiveColor);
            }

            if (pointerPath.EndsWith("/extensions/KHR_materials_emissive_strength/emissiveStrength", StringComparison.OrdinalIgnoreCase)
                || pointerPath.EndsWith("/emissiveStrength", StringComparison.OrdinalIgnoreCase)
                || pointerPath.EndsWith("/emissiveIntensity", StringComparison.OrdinalIgnoreCase))
            {
                return new ChannelTargetDescriptor(AnimationTargetKind.MaterialProperty, AnimationTargetProperty.EmissiveIntensity);
            }

            if (pointerPath.EndsWith("/pbrMetallicRoughness/baseColorFactor", StringComparison.OrdinalIgnoreCase)
                || pointerPath.EndsWith("/baseColorFactor", StringComparison.OrdinalIgnoreCase))
            {
                return new ChannelTargetDescriptor(AnimationTargetKind.MaterialProperty, AnimationTargetProperty.BaseColorFactor);
            }

            if (TryResolveTextureTransformPath(pointerPath, out var textureSlot, out var textureProperty))
            {
                return new ChannelTargetDescriptor(AnimationTargetKind.TextureProperty, textureProperty, textureSlot);
            }

            return null;
        }

        private static bool TryResolveTextureTransformPath(string pointerPath, out TextureSlot textureSlot, out AnimationTargetProperty textureProperty)
        {
            textureSlot = default;
            textureProperty = default;

            if (!pointerPath.Contains("KHR_texture_transform", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var slotResolved = false;
            foreach (var entry in TextureSlotPathRegistry)
            {
                if (!pointerPath.Contains(entry.PathToken, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                textureSlot = entry.Slot;
                slotResolved = true;
                break;
            }

            if (!slotResolved)
            {
                return false;
            }

            foreach (var entry in TextureTransformPropertyRegistry)
            {
                if (!pointerPath.EndsWith(entry.Suffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                textureProperty = entry.Property;
                return true;
            }

            return false;
        }

        private static bool TryExtractVector3PointerKeys(SharpGLTF.Schema2.AnimationChannel channel, out List<(float Time, Vector3 Value)> keys)
        {
            keys = [];
            var sampler = channel.GetSamplerOrNull<Vector3>();
            if (sampler == null)
            {
                return false;
            }

            foreach (var (time, value) in sampler.GetLinearKeys())
            {
                keys.Add((time, value));
            }

            return keys.Count > 0;
        }

        private static bool TryExtractFloatPointerKeys(SharpGLTF.Schema2.AnimationChannel channel, out List<(float Time, float Value)> keys)
        {
            keys = [];
            var sampler = channel.GetSamplerOrNull<float>();
            if (sampler != null)
            {
                foreach (var (time, value) in sampler.GetLinearKeys())
                {
                    keys.Add((time, value));
                }

                if (keys.Count > 0)
                {
                    return true;
                }
            }

            var scalarArraySampler = channel.GetSamplerOrNull<float[]>();
            if (scalarArraySampler == null)
            {
                return false;
            }

            foreach (var (time, value) in scalarArraySampler.GetLinearKeys())
            {
                if (value == null || value.Length == 0)
                {
                    continue;
                }

                keys.Add((time, value[0]));
            }

            return keys.Count > 0;
        }

        private enum AnimationTargetKind
        {
            NodeTransform,
            NodeMorph,
            MaterialProperty,
            TextureProperty
        }

        private readonly record struct ChannelTargetDescriptor(AnimationTargetKind Kind, AnimationTargetProperty TargetProperty, TextureSlot? TextureSlot = null);
        private readonly record struct ResolvedBindingSpec(string ChannelTargetKey, IAnimationTargetBinding Binding);
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

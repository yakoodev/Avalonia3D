// File: ModelLoader.cs - Optimized Version
using Avalonia3D.Model;
using Avalonia3D.Loaders.Policies;
using Avalonia3D.Rendering;
using Serilog;
using SharpGLTF.Schema2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Numerics;
using System.Runtime;

namespace Avalonia3D.Loaders
{
    public static class ModelLoader
    {
        private static readonly GltfMaterialExtensionsReader _materialExtensionsReader = new();
        private static readonly IMaterialImportPolicy _materialImportPolicy = new DefaultMaterialImportPolicy();

        private static readonly Dictionary<string, Dictionary<(int MeshIndex, int PrimitiveIndex), int>> _materialIndexMapCache = new(StringComparer.OrdinalIgnoreCase);
        private const uint GlbMagic = 0x46546C67;
        private const uint JsonChunkType = 0x4E4F534A;

        private unsafe static long EstimateModelMemory(Model.Model m)
        {
            long v = (m.Vertices?.LongLength ?? 0) * sizeof(Vertex);
            long i = (m.Indices?.LongLength ?? 0) * sizeof(uint);
            long t = (m.TextureData?.Data?.LongLength ?? 0);
            return v + i + t;
        }

        private static TextureData LoadTextureFromImage(byte[] imageData, int maxDimension = 1024) // Уменьшен размер по умолчанию
        {
            if (imageData == null || imageData.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(imageData);
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(ms);

                // Более агрессивный ресайз
                int w = image.Width;
                int h = image.Height;
                int maxDim = Math.Max(w, h);

                if (maxDim > maxDimension)
                {
                    var ratio = (float)maxDimension / maxDim;
                    int newW = Math.Max(4, (int)(w * ratio)); // Минимум 4x4
                    int newH = Math.Max(4, (int)(h * ratio));

                    // Используем более качественный алгоритм ресайза
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(newW, newH),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3 // Лучшее качество
                    }));
                    w = newW;
                    h = newH;
                }

                int size = w * h * 4;
                byte[] data = new byte[size];

                var span = data.AsSpan();
                image.CopyPixelDataTo(span);

                return new TextureData
                {
                    Width = w,
                    Height = h,
                    Data = data,
                    DataIsPooled = false
                };
            }
            catch (Exception ex)
            {
                Log.Information($"Texture load/resize error: {ex}");
                return null;
            }
        }

        public static List<Model.Model> LoadModels(ModelRoot gltf, MaterialImportPolicyContext? policyContext = null)
        {
            var models = new List<Model.Model>();
            if (gltf == null) return models;

            policyContext ??= new MaterialImportPolicyContext
            {
                AlphaProfile = MaterialAlphaImportConfiguration.CurrentProfile
            };

            try
            {
                foreach (var node in gltf.LogicalNodes)
                {
                    models.AddRange(LoadModelsForNode(node, policyContext));
                }

                // Принудительная сборка мусора после загрузки
                GC.Collect(2, GCCollectionMode.Aggressive);
                GC.WaitForPendingFinalizers();

                return models;
            }
            finally
            {
                // Очистка кеша текстур от мертвых ссылок
                CleanupTextureCache();
            }
        }

        public static List<Model.Model> LoadModelsForNode(Node node, MaterialImportPolicyContext? policyContext = null)
        {
            var models = new List<Model.Model>();
            if (node?.Mesh == null)
            {
                return models;
            }

            policyContext ??= new MaterialImportPolicyContext
            {
                AlphaProfile = MaterialAlphaImportConfiguration.CurrentProfile
            };

            foreach (var prim in node.Mesh.Primitives)
            {
                var model = LoadPrimitive(prim, node, policyContext);
                if (model != null)
                {
                    models.Add(model);

                    var mem = EstimateModelMemory(model);
                    Log.Information($"Loaded model '{model.Name}' CPU memory: {mem:N0} bytes");
                }
            }

            return models;
        }

        internal static Model.Model LoadPrimitive(MeshPrimitive prim, Node node, MaterialImportPolicyContext? policyContext = null)
        {
            var posAccessor = prim.GetVertexAccessor("POSITION");
            if (posAccessor == null) return null;

            var positions = posAccessor.AsVector3Array();
            var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
            var texcoords = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
            var texcoords1 = prim.GetVertexAccessor("TEXCOORD_1")?.AsVector2Array();
            var indicesAccessor = prim.GetIndexAccessor();

            // Создаем уникальный ключ для кеширования на GPU
            var primitiveKey = GeneratePrimitiveKey(prim, positions.Count, indicesAccessor?.Count ?? 0);       

            // Создаем массивы вершин - используем сразу правильный размер
            var vertices = new Vertex[positions.Count];

            // Обрабатываем вершины пакетами для лучшей производительности
            var batchSize = Math.Min(1024, positions.Count);
            for (int batch = 0; batch < positions.Count; batch += batchSize)
            {
                int end = Math.Min(batch + batchSize, positions.Count);

                for (int i = batch; i < end; i++)
                {                    
                    var normal = (i < (normals?.Count ?? 0)) ? normals[i] : Vector3.UnitY;

                    if (normal != Vector3.Zero)
                        normal = Vector3.Normalize(normal);

                    vertices[i] = new Vertex
                    {
                        Position = positions[i],
                        Normal = normal,
                        TexCoord = (i < (texcoords?.Count ?? 0)) ? texcoords[i] : Vector2.Zero,
                        TexCoord1 = (i < (texcoords1?.Count ?? 0)) ? texcoords1[i] : Vector2.Zero
                    };
                }
            }

            // Индексы
            uint[] indices = indicesAccessor?.AsIndicesArray()
                .Select(idx => (uint)idx)
                .ToArray() ?? Array.Empty<uint>();

            var resolvedMaterial = ResolvePrimitiveMaterial(prim, policyContext?.AssetPath);

            var model = new Model.Model
            {
                Name = $"{node.Name}_{resolvedMaterial?.Name ?? "mat"}",
                PrimitiveKey = primitiveKey,
                MaterialKey = resolvedMaterial != null ? $"material:{resolvedMaterial.LogicalIndex}" : string.Empty,
                Vertices = vertices,
                Indices = indices,
                LocalMatrix = node.LocalMatrix,
                MorphTargets = ReadMorphTargets(prim, positions.Count)
            };

            if (model.HasMorphTargets)
            {
                model.PrimitiveKey = string.Empty;
            }

            // Загрузка материала и текстур с кешированием
            LoadMaterialForModel(model, resolvedMaterial, prim, node, policyContext);

            return model;
        }



        private static SharpGLTF.Schema2.Material? ResolvePrimitiveMaterial(MeshPrimitive prim, string? assetPath)
        {
            if (prim == null)
            {
                return null;
            }

            if (prim.Material != null)
            {
                return prim.Material;
            }

            var reflectionMaterial = TryResolveMaterialViaReflection(prim);
            if (reflectionMaterial != null)
            {
                return reflectionMaterial;
            }

            var documentMaterial = TryResolveMaterialFromSourceDocument(prim, assetPath);
            if (documentMaterial != null)
            {
                return documentMaterial;
            }

            try
            {
                var mesh = prim.LogicalParent;
                var modelRoot = mesh?.LogicalParent;
                if (modelRoot == null)
                {
                    return null;
                }

                var materials = modelRoot.LogicalMaterials;
                if (materials == null || materials.Count != 1)
                {
                    return null;
                }

                var fallback = materials[0];
                Log.Warning("GLTF primitive has no explicit material binding. Applying single-material fallback: materialIndex={MaterialIndex}, materialName={MaterialName}", fallback.LogicalIndex, fallback.Name ?? "<unnamed>");
                return fallback;
            }
            catch
            {
                return null;
            }
        }

        private static SharpGLTF.Schema2.Material? TryResolveMaterialViaReflection(MeshPrimitive prim)
        {
            try
            {
                var type = prim.GetType();

                var logicalMaterialProperty = type.GetProperty("LogicalMaterial");
                if (logicalMaterialProperty?.GetValue(prim) is SharpGLTF.Schema2.Material logicalMaterial)
                {
                    Log.Warning("GLTF primitive material resolved via reflection property LogicalMaterial. materialIndex={MaterialIndex}, materialName={MaterialName}", logicalMaterial.LogicalIndex, logicalMaterial.Name ?? "<unnamed>");
                    return logicalMaterial;
                }

                var materialProperty = type.GetProperty("Material");
                if (materialProperty?.GetValue(prim) is SharpGLTF.Schema2.Material reflectedMaterial)
                {
                    Log.Warning("GLTF primitive material resolved via reflection property Material. materialIndex={MaterialIndex}, materialName={MaterialName}", reflectedMaterial.LogicalIndex, reflectedMaterial.Name ?? "<unnamed>");
                    return reflectedMaterial;
                }

                var materialIndex = TryReadMaterialIndex(type, prim);
                if (materialIndex.HasValue)
                {
                    var materials = prim.LogicalParent?.LogicalParent?.LogicalMaterials;
                    if (materials != null && materialIndex.Value >= 0 && materialIndex.Value < materials.Count)
                    {
                        var indexedMaterial = materials[materialIndex.Value];
                        Log.Warning("GLTF primitive material resolved via reflection index. materialIndex={MaterialIndex}, materialName={MaterialName}", indexedMaterial.LogicalIndex, indexedMaterial.Name ?? "<unnamed>");
                        return indexedMaterial;
                    }
                }
            }
            catch
            {
                // Ignore reflection failures.
            }

            return null;
        }

        private static SharpGLTF.Schema2.Material? TryResolveMaterialFromSourceDocument(MeshPrimitive prim, string? assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                return null;
            }

            var meshIndex = prim.LogicalParent?.LogicalIndex ?? -1;
            var primitiveIndex = ResolvePrimitiveIndexWithinMesh(prim);
            if (meshIndex < 0 || primitiveIndex < 0)
            {
                return null;
            }

            if (!TryGetMaterialIndexMap(assetPath, out var map))
            {
                return null;
            }

            if (!map.TryGetValue((meshIndex, primitiveIndex), out var materialIndex))
            {
                return null;
            }

            var materials = prim.LogicalParent?.LogicalParent?.LogicalMaterials;
            if (materials == null || materialIndex < 0 || materialIndex >= materials.Count)
            {
                return null;
            }

            var material = materials[materialIndex];
            Log.Warning("GLTF primitive material resolved from source JSON mapping. meshIndex={MeshIndex}, primitiveIndex={PrimitiveIndex}, materialIndex={MaterialIndex}, materialName={MaterialName}", meshIndex, primitiveIndex, material.LogicalIndex, material.Name ?? "<unnamed>");
            return material;
        }

        private static int ResolvePrimitiveIndexWithinMesh(MeshPrimitive prim)
        {
            var primitives = prim.LogicalParent?.Primitives;
            if (primitives == null)
            {
                return -1;
            }

            for (var i = 0; i < primitives.Count; i++)
            {
                if (ReferenceEquals(primitives[i], prim))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryGetMaterialIndexMap(string assetPath, out Dictionary<(int MeshIndex, int PrimitiveIndex), int> map)
        {
            if (_materialIndexMapCache.TryGetValue(assetPath, out map!))
            {
                return true;
            }

            try
            {
                var root = ReadGltfRootFromFile(assetPath);
                var parsed = ParseMaterialIndexMap(root);
                _materialIndexMapCache[assetPath] = parsed;
                map = parsed;
                return true;
            }
            catch
            {
                map = new Dictionary<(int MeshIndex, int PrimitiveIndex), int>();
                return false;
            }
        }

        private static JsonElement ReadGltfRootFromFile(string assetPath)
        {
            if (Path.GetExtension(assetPath).Equals(".glb", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(assetPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
                if (TryReadGlbJsonChunk(reader, out var glbRoot))
                {
                    return glbRoot;
                }

                throw new InvalidDataException("GLB JSON chunk was not found.");
            }

            using var jsonStream = File.OpenRead(assetPath);
            using var doc = JsonDocument.Parse(jsonStream);
            return doc.RootElement.Clone();
        }

        private static Dictionary<(int MeshIndex, int PrimitiveIndex), int> ParseMaterialIndexMap(JsonElement root)
        {
            var map = new Dictionary<(int MeshIndex, int PrimitiveIndex), int>();
            if (!root.TryGetProperty("meshes", out var meshes) || meshes.ValueKind != JsonValueKind.Array)
            {
                return map;
            }

            var meshIndex = 0;
            foreach (var mesh in meshes.EnumerateArray())
            {
                if (!mesh.TryGetProperty("primitives", out var primitives) || primitives.ValueKind != JsonValueKind.Array)
                {
                    meshIndex++;
                    continue;
                }

                var primitiveIndex = 0;
                foreach (var primitive in primitives.EnumerateArray())
                {
                    if (primitive.TryGetProperty("material", out var materialProperty) && materialProperty.ValueKind == JsonValueKind.Number && materialProperty.TryGetInt32(out var materialIndex))
                    {
                        map[(meshIndex, primitiveIndex)] = materialIndex;
                    }

                    primitiveIndex++;
                }

                meshIndex++;
            }

            return map;
        }

        private static bool TryReadGlbJsonChunk(BinaryReader reader, out JsonElement root)
        {
            root = default;
            if (reader.BaseStream.Length < 12)
            {
                return false;
            }

            var magic = reader.ReadUInt32();
            var version = reader.ReadUInt32();
            var fileLength = reader.ReadUInt32();

            if (magic != GlbMagic || version < 2 || fileLength > reader.BaseStream.Length)
            {
                return false;
            }

            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                var chunkLength = reader.ReadUInt32();
                var chunkType = reader.ReadUInt32();
                if (chunkLength > int.MaxValue || reader.BaseStream.Position + chunkLength > reader.BaseStream.Length)
                {
                    return false;
                }

                var chunkBytes = reader.ReadBytes((int)chunkLength);
                if (chunkType != JsonChunkType)
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(chunkBytes);
                root = doc.RootElement.Clone();
                return true;
            }

            return false;
        }

        private static int? TryReadMaterialIndex(Type primitiveType, MeshPrimitive primitive)
        {
            var materialIndexProperty = primitiveType.GetProperty("MaterialIndex") ?? primitiveType.GetProperty("LogicalMaterialIndex");
            if (materialIndexProperty?.GetValue(primitive) is int materialIndex)
            {
                return materialIndex;
            }

            var rawValue = materialIndexProperty?.GetValue(primitive);
            if (rawValue != null && int.TryParse(rawValue.ToString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static MorphTarget[] ReadMorphTargets(MeshPrimitive prim, int vertexCount)
        {
            if (prim == null || prim.MorphTargetsCount <= 0 || vertexCount <= 0)
            {
                return [];
            }

            var targets = new MorphTarget[prim.MorphTargetsCount];
            for (var i = 0; i < prim.MorphTargetsCount; i++)
            {
                var accessors = prim.GetMorphTargetAccessors(i);
                var positionDeltas = new Vector3[vertexCount];
                var normalDeltas = new Vector3[vertexCount];

                if (accessors.TryGetValue("POSITION", out var positionAccessor) && positionAccessor != null)
                {
                    var src = positionAccessor.AsVector3Array();
                    var count = Math.Min(vertexCount, src.Count);
                    for (var v = 0; v < count; v++)
                    {
                        positionDeltas[v] = src[v];
                    }
                }

                if (accessors.TryGetValue("NORMAL", out var normalAccessor) && normalAccessor != null)
                {
                    var src = normalAccessor.AsVector3Array();
                    var count = Math.Min(vertexCount, src.Count);
                    for (var v = 0; v < count; v++)
                    {
                        normalDeltas[v] = src[v];
                    }
                }

                targets[i] = new MorphTarget
                {
                    PositionDeltas = positionDeltas,
                    NormalDeltas = normalDeltas
                };
            }

            return targets;
        }

        private static void LoadMaterialForModel(Model.Model model, SharpGLTF.Schema2.Material? material, MeshPrimitive prim, Node node, MaterialImportPolicyContext? policyContext)
        {
            if (model == null)
            {
                return;
            }

            if (material == null)
            {
                return;
            }

            policyContext ??= new MaterialImportPolicyContext
            {
                AlphaProfile = MaterialAlphaImportConfiguration.CurrentProfile
            };

            var result = new Avalonia3D.Model.Material();
            ApplyPbrFactors(material, result);
            var extensionPayload = _materialExtensionsReader.Read(material);
            ApplySurfaceSettings(material, result, extensionPayload);
            result.Opacity = result.BaseColorFactor.W;

            var baseColorChannel = material.FindChannel("BaseColor");
            result.BaseColorTexture = LoadTextureFromChannel(baseColorChannel, out var baseColorTexCoord);
            AssignTextureTexCoord(result.BaseColorTexture, baseColorTexCoord);
            SyncTextureRuntimeTransform(result, TextureSemantic.BaseColor, result.BaseColorTexture);
            if (baseColorChannel != null)
            {
                var baseColor = GetChannelColor(baseColorChannel, result.BaseColorFactor);
                result.BaseColorFactor = baseColor;
                result.Opacity = baseColor.W;
            }

            var normalChannel = material.FindChannel("Normal");
            result.NormalTexture = LoadTextureFromChannel(normalChannel, out var normalTexCoord);
            AssignTextureTexCoord(result.NormalTexture, normalTexCoord);
            SyncTextureRuntimeTransform(result, TextureSemantic.Normal, result.NormalTexture);

            var metallicRoughnessChannel = material.FindChannel("MetallicRoughness");
            result.MetallicRoughnessTexture = LoadTextureFromChannel(metallicRoughnessChannel, out var metallicRoughnessTexCoord);
            AssignTextureTexCoord(result.MetallicRoughnessTexture, metallicRoughnessTexCoord);
            SyncTextureRuntimeTransform(result, TextureSemantic.MetallicRoughness, result.MetallicRoughnessTexture);

            var occlusionChannel = material.FindChannel("Occlusion");
            result.OcclusionTexture = LoadTextureFromChannel(occlusionChannel, out var occlusionTexCoord);
            AssignTextureTexCoord(result.OcclusionTexture, occlusionTexCoord);
            SyncTextureRuntimeTransform(result, TextureSemantic.Occlusion, result.OcclusionTexture);
            if (occlusionChannel != null)
            {
                result.OcclusionStrength = GetChannelStrength(occlusionChannel, result.OcclusionStrength);
            }

            result.EmissiveFactor = ReadEmissiveFactor(material, result.EmissiveFactor);

            var emissiveChannel = material.FindChannel("Emissive");
            result.EmissiveTexture = LoadTextureFromChannel(emissiveChannel, out var emissiveTexCoord);
            AssignTextureTexCoord(result.EmissiveTexture, emissiveTexCoord);
            SyncTextureRuntimeTransform(result, TextureSemantic.Emissive, result.EmissiveTexture);
            if (emissiveChannel != null)
            {
                var emissiveColor = GetChannelColor(emissiveChannel, new Vector4(result.EmissiveFactor, 1f));
                result.EmissiveFactor = new Vector3(emissiveColor.X, emissiveColor.Y, emissiveColor.Z);
            }


            var extensionChannels = extensionPayload.TextureChannels;
            result.ExtensionTextures.ClearcoatTexture = LoadTextureFromChannel(extensionChannels.Clearcoat, out var clearcoatTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.ClearcoatTexture, clearcoatTexCoord);
            result.ExtensionTextures.ClearcoatRoughnessTexture = LoadTextureFromChannel(extensionChannels.ClearcoatRoughness, out var clearcoatRoughnessTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.ClearcoatRoughnessTexture, clearcoatRoughnessTexCoord);
            result.ExtensionTextures.ClearcoatNormalTexture = LoadTextureFromChannel(extensionChannels.ClearcoatNormal, out var clearcoatNormalTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.ClearcoatNormalTexture, clearcoatNormalTexCoord);
            result.ExtensionTextures.SheenColorTexture = LoadTextureFromChannel(extensionChannels.SheenColor, out var sheenColorTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.SheenColorTexture, sheenColorTexCoord);
            result.ExtensionTextures.SheenRoughnessTexture = LoadTextureFromChannel(extensionChannels.SheenRoughness, out var sheenRoughnessTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.SheenRoughnessTexture, sheenRoughnessTexCoord);
            result.ExtensionTextures.SpecularTexture = LoadTextureFromChannel(extensionChannels.Specular, out var specularTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.SpecularTexture, specularTexCoord);
            result.ExtensionTextures.SpecularColorTexture = LoadTextureFromChannel(extensionChannels.SpecularColor, out var specularColorTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.SpecularColorTexture, specularColorTexCoord);
            result.ExtensionTextures.TransmissionTexture = LoadTextureFromChannel(extensionChannels.Transmission, out var transmissionTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.TransmissionTexture, transmissionTexCoord);
            result.ExtensionTextures.VolumeThicknessTexture = LoadTextureFromChannel(extensionChannels.VolumeThickness, out var volumeThicknessTexCoord);
            AssignTextureTexCoord(result.ExtensionTextures.VolumeThicknessTexture, volumeThicknessTexCoord);

            var sourceAlphaMode = result.SourceAlphaMode;
            var resolvedSceneOverride = MaterialImportOverrideConfiguration.ResolveForMaterial(policyContext.AssetPath, material.Name)
                ?? policyContext.SceneOverride;

            var effectivePolicyContext = new MaterialImportPolicyContext
            {
                AssetPath = policyContext.AssetPath,
                AlphaProfile = policyContext.AlphaProfile,
                SceneOverride = resolvedSceneOverride,
                SourceAlphaMode = sourceAlphaMode,
                MaterialName = material.Name,
                MeshName = prim.LogicalParent?.Name,
                NodeName = node?.Name,
                NodeStableId = node != null ? $"node:{node.LogicalIndex}" : null,
                IsAnimatedMaterial = model.HasMorphTargets ||
                    (node?.MorphWeights?.Count ?? 0) > 0 ||
                    (!string.IsNullOrWhiteSpace(policyContext.AssetPath) && policyContext.AssetPath.Contains("anim", StringComparison.OrdinalIgnoreCase))
            };

            _ = _materialImportPolicy.ResolveColorSpaceHandling(result, TextureSemantic.BaseColor, effectivePolicyContext);
            _ = _materialImportPolicy.ResolveColorSpaceHandling(result, TextureSemantic.Emissive, effectivePolicyContext);
            result.AlphaMode = _materialImportPolicy.ResolveAlphaMode(result, effectivePolicyContext);

            result.IsTransparent = result.AlphaMode == MaterialAlphaMode.Blend;

            model.Material = result;
            model.TextureData = result.BaseColorTexture;
        }


        private static void ApplySurfaceSettings(SharpGLTF.Schema2.Material material, Avalonia3D.Model.Material target, MaterialExtensionData extensionPayload)
        {
            if (material == null || target == null)
            {
                return;
            }

            target.AlphaMode = ParseAlphaMode(material.Alpha);
            target.SourceAlphaMode = target.AlphaMode;
            target.AlphaCutoff = material.AlphaCutoff;
            target.DoubleSided = material.DoubleSided;
            target.EmissiveStrength = extensionPayload.EmissiveStrength.Value;
            target.TransmissionFactor = extensionPayload.Transmission.Factor;
            target.TransmissionThickness = extensionPayload.Transmission.Thickness;
            target.TransmissionIor = extensionPayload.Transmission.Ior;
            target.TransmissionAttenuationDistance = extensionPayload.Transmission.AttenuationDistance;
            target.TransmissionAttenuationColor = extensionPayload.Transmission.AttenuationColor;
            target.ClearcoatFactor = extensionPayload.Clearcoat.Factor;
            target.ClearcoatRoughness = extensionPayload.Clearcoat.Roughness;
            target.SheenColorFactor = extensionPayload.Sheen.ColorFactor;
            target.SheenRoughnessFactor = extensionPayload.Sheen.RoughnessFactor;
            target.SpecularFactor = extensionPayload.Specular.Factor;
            target.SpecularColorFactor = extensionPayload.Specular.ColorFactor;
            target.Ior = extensionPayload.Ior.Value;
            target.HasTransmission = target.TransmissionFactor > 0.001f;
        }

        private static MaterialAlphaMode ParseAlphaMode(AlphaMode alphaMode)
        {
            return alphaMode switch
            {
                AlphaMode.MASK => MaterialAlphaMode.Mask,
                AlphaMode.BLEND => MaterialAlphaMode.Blend,
                _ => MaterialAlphaMode.Opaque
            };
        }

        private static void SyncTextureRuntimeTransform(Avalonia3D.Model.Material material, TextureSemantic semantic, TextureData? texture)
        {
            if (material == null)
            {
                return;
            }

            var runtime = material.TextureRuntime.GetOrCreate(semantic);
            if (texture == null)
            {
                runtime.UvOffset = Vector2.Zero;
                runtime.UvScale = Vector2.One;
                runtime.UvRotation = 0f;
                runtime.TexCoordSet = 0;
                return;
            }

            runtime.UvOffset = texture.Transform.Offset;
            runtime.UvScale = texture.Transform.Scale;
            runtime.UvRotation = texture.Transform.Rotation;
            runtime.TexCoordSet = texture.Transform.TexCoord;
        }

        private static TextureData? LoadTextureFromChannel(MaterialChannel? channel, out int texCoord)
        {
            texCoord = GetChannelTexCoord(channel);
            var image = channel?.Texture?.PrimaryImage;
            if (image == null)
            {
                return null;
            }

            return LoadTextureFromImage(image);
        }

        private static TextureData? LoadTextureFromChannel(object? channel, out int texCoord)
        {
            if (channel is MaterialChannel materialChannel)
            {
                return LoadTextureFromChannel(materialChannel, out texCoord);
            }

            texCoord = GetChannelTexCoord(channel);
            if (channel == null)
            {
                return null;
            }

            var textureProperty = channel.GetType().GetProperty("Texture");
            var texture = textureProperty?.GetValue(channel);
            var primaryImageProperty = texture?.GetType().GetProperty("PrimaryImage");
            if (primaryImageProperty?.GetValue(texture) is SharpGLTF.Schema2.Image primaryImage)
            {
                return LoadTextureFromImage(primaryImage);
            }

            return null;
        }


        private static void AssignTextureTexCoord(TextureData? texture, int texCoord)
        {
            if (texture == null)
            {
                return;
            }

            texture.Transform.TexCoord = texCoord;
        }

        private static int GetChannelTexCoord(object? channel)
        {
            if (channel == null)
            {
                return 0;
            }

            var texCoordProperty = channel.GetType().GetProperty("TextureCoordinate")
                ?? channel.GetType().GetProperty("TexCoord")
                ?? channel.GetType().GetProperty("TextureCoord");

            if (texCoordProperty?.GetValue(channel) is int texCoord)
            {
                return texCoord;
            }

            return 0;
        }

        private static TextureData? LoadTextureFromImage(SharpGLTF.Schema2.Image image)
        {
            var texBytes = image.Content.Content.ToArray();
            return LoadTextureFromImage(texBytes);
        }

        private static Vector4 GetChannelColor(MaterialChannel? channel, Vector4 fallback)
        {
            if (channel == null)
            {
                return fallback;
            }

            var colorProperty = channel.GetType().GetProperty("Color");
            if (colorProperty?.GetValue(channel) is Vector4 color)
            {
                return color;
            }

            return fallback;
        }

        private static float GetChannelStrength(MaterialChannel? channel, float fallback)
        {
            if (channel == null)
            {
                return fallback;
            }

            var strengthProperty = channel.GetType().GetProperty("Strength");
            if (strengthProperty?.GetValue(channel) is float strength)
            {
                return strength;
            }

            return fallback;
        }

        private static Vector3 ReadEmissiveFactor(SharpGLTF.Schema2.Material material, Vector3 fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            try
            {
                var emissiveFactorProperty = material.GetType().GetProperty("EmissiveFactor");
                if (emissiveFactorProperty?.GetValue(material) is Vector3 emissiveFactor)
                {
                    return emissiveFactor;
                }
            }
            catch
            {
                // ignore reflection errors and keep fallback
            }

            return fallback;
        }


        private static void ApplyPbrFactors(SharpGLTF.Schema2.Material material, Avalonia3D.Model.Material target)
        {
            if (material == null || target == null)
            {
                return;
            }

            var pbrProp = material.GetType().GetProperty("PbrMetallicRoughness");
            if (pbrProp?.GetValue(material) is not null)
            {
                var pbr = pbrProp.GetValue(material);
                if (pbr == null)
                {
                    return;
                }

                var baseColorProp = pbr.GetType().GetProperty("BaseColorFactor");
                if (baseColorProp?.GetValue(pbr) is Vector4 baseColor)
                {
                    target.BaseColorFactor = baseColor;
                }

                var metallicProp = pbr.GetType().GetProperty("MetallicFactor");
                if (metallicProp?.GetValue(pbr) is float metallic)
                {
                    target.MetallicFactor = metallic;
                }

                var roughnessProp = pbr.GetType().GetProperty("RoughnessFactor");
                if (roughnessProp?.GetValue(pbr) is float roughness)
                {
                    target.RoughnessFactor = roughness;
                }
            }
        }

        private static string GeneratePrimitiveKey(MeshPrimitive prim, int vertexCount, int indexCount)
        {
            // Более точный ключ для кеширования
            var material = prim.Material;
            var materialKey = material != null ?
                $"{material.Name}_{material.GetHashCode()}" : "default";

            return $"prim_{vertexCount}_{indexCount}_{materialKey}";
        }

        private static void CleanupTextureCache()
        {
            // no-op: CPU texture cache removed to avoid excessive RAM usage
        }

        public static void ClearAllCaches()
        {
            // no-op: CPU texture cache removed to avoid excessive RAM usage
            GC.Collect(2, GCCollectionMode.Aggressive);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive);

            Log.Information("All ModelLoader caches cleared");
        }
    }
}

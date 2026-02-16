// File: ModelLoader.cs - Optimized Version
using Avalonia3D.Model;
using Avalonia3D.Loaders.Policies;
using Avalonia3D.Memory;
using Avalonia3D.Rendering;
using Serilog;
using SharpGLTF.Schema2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Numerics;
using System.Runtime;
using System.Security.Cryptography;

namespace Avalonia3D.Loaders
{
    public static class ModelLoader
    {
        private static readonly GltfMaterialExtensionsReader _materialExtensionsReader = new();
        private static readonly IMaterialImportPolicy _materialImportPolicy = new DefaultMaterialImportPolicy();
        private static IMemoryPressurePolicy _memoryPressurePolicy = DefaultMemoryPressurePolicy.Instance;

        private static readonly ConcurrentDictionary<string, MaterialIndexMapCacheEntry> _materialIndexMapCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<PrimitiveMaterialCacheKey, int?> _reflectionMaterialFallbackCache = new();
        private static readonly TimeSpan MaterialIndexMapCacheTtl = TimeSpan.FromMinutes(10);
        private const int MaterialIndexMapCacheSizeLimit = 64;
        private static readonly LruTextureDecodeCache _textureDecodeCache = new();
        private static ITextureDecodePolicy _textureDecodePolicy = TextureDecodePolicies.Balanced;
        private const uint GlbMagic = 0x46546C67;
        private const uint JsonChunkType = 0x4E4F534A;

        private readonly record struct PrimitiveMaterialCacheKey(string AssetPath, int MeshIndex, int PrimitiveIndex);

        private sealed class MaterialIndexMapCacheEntry
        {
            public required IReadOnlyDictionary<(int MeshIndex, int PrimitiveIndex), int> Map { get; init; }
            public required DateTimeOffset ExpiresAtUtc { get; init; }
            public required DateTimeOffset LastAccessUtc { get; init; }

            public MaterialIndexMapCacheEntry WithAccess(DateTimeOffset now) => new()
            {
                Map = Map,
                ExpiresAtUtc = ExpiresAtUtc,
                LastAccessUtc = now
            };
        }

        private unsafe static long EstimateModelMemory(Model.Model m)
        {
            long v = (m.Vertices?.LongLength ?? 0) * sizeof(Vertex);
            long i = (m.Indices?.LongLength ?? 0) * sizeof(uint);
            long t = (m.TextureData?.Data?.LongLength ?? 0);
            return v + i + t;
        }

        private static TextureData? LoadTextureFromImage(ReadOnlyMemory<byte> imageData, int maxDimension, TextureDecodeMode samplerMode)
        {
            if (imageData.IsEmpty) return null;

            try
            {
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageData.Span);

                // Более агрессивный ресайз
                int w = image.Width;
                int h = image.Height;
                int maxDim = Math.Max(w, h);

                if (maxDim > maxDimension)
                {
                    var ratio = (float)maxDimension / maxDim;
                    int newW = Math.Max(4, (int)(w * ratio)); // Минимум 4x4
                    int newH = Math.Max(4, (int)(h * ratio));

                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(newW, newH),
                        Mode = ResizeMode.Max,
                        Sampler = _textureDecodePolicy.GetResamplerFor(samplerMode)
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

        public static void ConfigureMemoryPressurePolicy(IMemoryPressurePolicy? policy)
        {
            _memoryPressurePolicy = policy ?? DefaultMemoryPressurePolicy.Instance;
        }

        public static void ConfigureTextureDecodePolicy(ITextureDecodePolicy? policy)
        {
            _textureDecodePolicy = policy ?? TextureDecodePolicies.Balanced;
        }

        public static List<Model.Model> LoadModels(ModelRoot gltf, MaterialImportPolicyContext? policyContext = null, IMemoryPressurePolicy? memoryPressurePolicy = null)
        {
            var models = new List<Model.Model>();
            if (gltf == null) return models;

            var pressurePolicy = memoryPressurePolicy ?? _memoryPressurePolicy;
            pressurePolicy.NotifyActivity("ModelLoader.LoadModels:start");

            policyContext ??= new MaterialImportPolicyContext
            {
                AlphaProfile = MaterialAlphaImportConfiguration.CurrentProfile
            };

            var precomputedMaterialIndexMap = policyContext.PrecomputedMaterialIndexMap
                ?? BuildPrecomputedMaterialIndexMap(gltf, policyContext.AssetPath);

            var effectivePolicyContext = CreatePolicyContextWithMaterialMap(policyContext, precomputedMaterialIndexMap);

            try
            {
                foreach (var node in gltf.LogicalNodes)
                {
                    models.AddRange(LoadModelsForNode(node, effectivePolicyContext));
                }

                pressurePolicy.OnImportCompleted("ModelLoader.LoadModels");
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

            if (policyContext.PrecomputedMaterialIndexMap == null)
            {
                var precomputedMap = BuildPrecomputedMaterialIndexMap(node.LogicalParent, policyContext.AssetPath);
                policyContext = CreatePolicyContextWithMaterialMap(policyContext, precomputedMap);
            }

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
            uint[] indices;
            if (indicesAccessor == null)
            {
                indices = Array.Empty<uint>();
            }
            else
            {
                var sourceIndices = indicesAccessor.AsIndicesArray();
                indices = new uint[sourceIndices.Count];
                for (var i = 0; i < sourceIndices.Count; i++)
                {
                    indices[i] = (uint)sourceIndices[i];
                }
            }

            var resolvedMaterial = ResolvePrimitiveMaterial(prim, policyContext);

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



        private static SharpGLTF.Schema2.Material? ResolvePrimitiveMaterial(MeshPrimitive prim, MaterialImportPolicyContext? policyContext)
        {
            if (prim == null)
            {
                return null;
            }

            if (prim.Material != null)
            {
                return prim.Material;
            }

            var documentMaterial = TryResolveMaterialFromSourceDocument(prim, policyContext);
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

        private static SharpGLTF.Schema2.Material? TryResolveMaterialFromSourceDocument(MeshPrimitive prim, MaterialImportPolicyContext? policyContext)
        {
            var meshIndex = prim.LogicalParent?.LogicalIndex ?? -1;
            var primitiveIndex = ResolvePrimitiveIndexWithinMesh(prim);
            if (meshIndex < 0 || primitiveIndex < 0)
            {
                return null;
            }

            var map = policyContext?.PrecomputedMaterialIndexMap;
            if (map == null && !string.IsNullOrWhiteSpace(policyContext?.AssetPath) && TryGetMaterialIndexMap(policyContext.AssetPath, out var fileMap))
            {
                map = fileMap;
            }

            if (map != null && map.TryGetValue((meshIndex, primitiveIndex), out var materialIndexFromMap))
            {
                var mappedMaterial = ResolveMaterialByIndex(prim, materialIndexFromMap);
                if (mappedMaterial != null)
                {
                    Log.Warning("GLTF primitive material resolved from material index map. meshIndex={MeshIndex}, primitiveIndex={PrimitiveIndex}, materialIndex={MaterialIndex}, materialName={MaterialName}", meshIndex, primitiveIndex, mappedMaterial.LogicalIndex, mappedMaterial.Name ?? "<unnamed>");
                    return mappedMaterial;
                }
            }

            var fallbackKey = new PrimitiveMaterialCacheKey(policyContext?.AssetPath ?? string.Empty, meshIndex, primitiveIndex);
            if (_reflectionMaterialFallbackCache.TryGetValue(fallbackKey, out var cachedMaterialIndex))
            {
                return cachedMaterialIndex.HasValue ? ResolveMaterialByIndex(prim, cachedMaterialIndex.Value) : null;
            }

            var reflectionMaterial = TryResolveMaterialViaReflection(prim);
            var resolvedIndex = reflectionMaterial?.LogicalIndex;
            _reflectionMaterialFallbackCache[fallbackKey] = resolvedIndex;
            return reflectionMaterial;
        }

        private static SharpGLTF.Schema2.Material? ResolveMaterialByIndex(MeshPrimitive prim, int materialIndex)
        {
            var materials = prim.LogicalParent?.LogicalParent?.LogicalMaterials;
            if (materials == null || materialIndex < 0 || materialIndex >= materials.Count)
            {
                return null;
            }

            return materials[materialIndex];
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

        private static bool TryGetMaterialIndexMap(string assetPath, out IReadOnlyDictionary<(int MeshIndex, int PrimitiveIndex), int> map)
        {
            map = new Dictionary<(int MeshIndex, int PrimitiveIndex), int>();
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            if (_materialIndexMapCache.TryGetValue(assetPath, out var cachedEntry) && cachedEntry.ExpiresAtUtc > now)
            {
                _materialIndexMapCache[assetPath] = cachedEntry.WithAccess(now);
                map = cachedEntry.Map;
                return true;
            }

            try
            {
                var root = ReadGltfRootFromFile(assetPath);
                var parsed = ParseMaterialIndexMap(root);
                _materialIndexMapCache[assetPath] = new MaterialIndexMapCacheEntry
                {
                    Map = parsed,
                    ExpiresAtUtc = now.Add(MaterialIndexMapCacheTtl),
                    LastAccessUtc = now
                };

                TrimMaterialIndexMapCacheIfNeeded(now);
                map = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TrimMaterialIndexMapCacheIfNeeded(DateTimeOffset now)
        {
            foreach (var pair in _materialIndexMapCache)
            {
                if (pair.Value.ExpiresAtUtc <= now)
                {
                    _materialIndexMapCache.TryRemove(pair.Key, out _);
                }
            }

            while (_materialIndexMapCache.Count > MaterialIndexMapCacheSizeLimit)
            {
                var oldest = _materialIndexMapCache.OrderBy(pair => pair.Value.LastAccessUtc).FirstOrDefault();
                if (string.IsNullOrEmpty(oldest.Key))
                {
                    break;
                }

                _materialIndexMapCache.TryRemove(oldest.Key, out _);
            }
        }

        private static IReadOnlyDictionary<(int MeshIndex, int PrimitiveIndex), int> BuildPrecomputedMaterialIndexMap(ModelRoot? gltf, string? assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath) && TryGetMaterialIndexMap(assetPath, out var sourceDocumentMap))
            {
                return sourceDocumentMap;
            }

            var map = new Dictionary<(int MeshIndex, int PrimitiveIndex), int>();
            if (gltf == null)
            {
                return map;
            }

            foreach (var mesh in gltf.LogicalMeshes)
            {
                foreach (var primitive in mesh.Primitives)
                {
                    var materialIndex = primitive.Material?.LogicalIndex;
                    if (!materialIndex.HasValue)
                    {
                        continue;
                    }

                    map[(mesh.LogicalIndex, primitive.LogicalIndex)] = materialIndex.Value;
                }
            }

            return map;
        }

        private static MaterialImportPolicyContext CreatePolicyContextWithMaterialMap(MaterialImportPolicyContext baseContext, IReadOnlyDictionary<(int MeshIndex, int PrimitiveIndex), int> precomputedMaterialIndexMap)
        {
            return new MaterialImportPolicyContext
            {
                AssetPath = baseContext.AssetPath,
                AlphaProfile = baseContext.AlphaProfile,
                SceneOverride = baseContext.SceneOverride,
                SourceAlphaMode = baseContext.SourceAlphaMode,
                MaterialName = baseContext.MaterialName,
                MeshName = baseContext.MeshName,
                NodeName = baseContext.NodeName,
                NodeStableId = baseContext.NodeStableId,
                IsAnimatedMaterial = baseContext.IsAnimatedMaterial,
                PrecomputedMaterialIndexMap = precomputedMaterialIndexMap
            };
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
            if (metallicRoughnessChannel != null)
            {
                result.MetallicFactor = GetChannelScalarByName(metallicRoughnessChannel, result.MetallicFactor, "MetallicFactor", "MetalnessFactor", "Metallic", "metallicFactor");
                result.RoughnessFactor = GetChannelScalarByName(metallicRoughnessChannel, result.RoughnessFactor, "RoughnessFactor", "roughnessFactor");
            }

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
                    (!string.IsNullOrWhiteSpace(policyContext.AssetPath) && policyContext.AssetPath.Contains("anim", StringComparison.OrdinalIgnoreCase)),
                PrecomputedMaterialIndexMap = policyContext.PrecomputedMaterialIndexMap
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
            var maxDimension = Math.Max(4, MemoryManager.Settings.MaxTextureDimension);
            var samplerMode = _textureDecodePolicy.Mode;
            var cacheKey = BuildTextureCacheKey(image, maxDimension, samplerMode);

            if (_textureDecodeCache.TryGet(cacheKey, out var cached))
            {
                return CloneTextureData(cached);
            }

            var content = image.Content.Content;
            if (content.IsEmpty)
            {
                return null;
            }

            PersistOriginalTextureBytes(cacheKey.SourceIdentity, content);

            var decoded = LoadTextureFromImage(content, maxDimension, samplerMode);
            if (decoded == null)
            {
                return null;
            }

            _textureDecodeCache.Set(cacheKey, decoded, MemoryManager.Settings.MaxDecodedTextureCacheMemoryMB * 1024L * 1024L);
            return CloneTextureData(decoded);
        }

        private static TextureData CloneTextureData(TextureData source)
        {
            var dataCopy = new byte[source.Data.Length];
            source.Data.AsSpan().CopyTo(dataCopy);

            return new TextureData
            {
                Width = source.Width,
                Height = source.Height,
                Data = dataCopy,
                DataIsPooled = false
            };
        }

        private static DecodedTextureCacheKey BuildTextureCacheKey(SharpGLTF.Schema2.Image image, int targetDimension, TextureDecodeMode samplerMode)
        {
            var uri = TryGetImageIdentity(image);
            if (!string.IsNullOrWhiteSpace(uri))
            {
                return new DecodedTextureCacheKey(uri, targetDimension, samplerMode);
            }

            var hash = Convert.ToHexString(SHA256.HashData(image.Content.Content.Span));
            return new DecodedTextureCacheKey(hash, targetDimension, samplerMode);
        }


        private static string? TryGetImageIdentity(SharpGLTF.Schema2.Image image)
        {
            var uriProperty = image.GetType().GetProperty("Uri")
                ?? image.GetType().GetProperty("Name")
                ?? image.GetType().GetProperty("LogicalIndex");

            return uriProperty?.GetValue(image)?.ToString();
        }

        private static void PersistOriginalTextureBytes(string sourceIdentity, ReadOnlyMemory<byte> sourceData)
        {
            if (!MemoryManager.Settings.PersistOriginalTextureBytes || sourceData.IsEmpty)
            {
                return;
            }

            var cacheRoot = Path.Combine(Path.GetTempPath(), "Avalonia3D", "asset-cache", "textures");
            var maxSizeBytes = Math.Max(1L, MemoryManager.Settings.MaxPersistedTextureCacheMemoryMB * 1024L * 1024L);
            var maxAge = MemoryManager.Settings.PersistedTextureCacheMaxAge <= TimeSpan.Zero
                ? TimeSpan.FromHours(1)
                : MemoryManager.Settings.PersistedTextureCacheMaxAge;

            try
            {
                Directory.CreateDirectory(cacheRoot);
                CleanupPersistedTextureCache(cacheRoot, maxSizeBytes, maxAge);

                var fileName = $"{SanitizeFileName(sourceIdentity)}.bin";
                var fullPath = Path.Combine(cacheRoot, fileName);
                if (File.Exists(fullPath))
                {
                    return;
                }

                long currentSizeBytes = GetDirectorySize(cacheRoot);
                if (currentSizeBytes + sourceData.Length > maxSizeBytes)
                {
                    CleanupPersistedTextureCache(cacheRoot, maxSizeBytes - sourceData.Length, maxAge);
                    currentSizeBytes = GetDirectorySize(cacheRoot);
                    if (currentSizeBytes + sourceData.Length > maxSizeBytes)
                    {
                        Log.Debug("Skipping persisted texture cache write for {SourceIdentity}: cache budget exceeded ({CurrentSize}+{IncomingSize}>{Budget})",
                            sourceIdentity, currentSizeBytes, sourceData.Length, maxSizeBytes);
                        return;
                    }
                }

                File.WriteAllBytes(fullPath, sourceData.ToArray());
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Persisting original texture bytes failed for {SourceIdentity}", sourceIdentity);
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            var candidate = sb.ToString();
            return candidate.Length <= 80 ? candidate : candidate.Substring(0, 80);
        }

        private static void CleanupPersistedTextureCache(string cacheRoot, long maxSizeBytes, TimeSpan maxAge)
        {
            var directory = new DirectoryInfo(cacheRoot);
            if (!directory.Exists)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            foreach (var file in directory.GetFiles("*.bin"))
            {
                if (nowUtc - file.LastWriteTimeUtc > maxAge)
                {
                    TryDeleteFile(file);
                }
            }

            maxSizeBytes = Math.Max(0, maxSizeBytes);
            var files = directory.GetFiles("*.bin");
            long totalSizeBytes = 0;
            foreach (var file in files)
            {
                totalSizeBytes += file.Length;
            }

            if (totalSizeBytes <= maxSizeBytes)
            {
                return;
            }

            Array.Sort(files, static (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            foreach (var file in files)
            {
                TryDeleteFile(file);
                totalSizeBytes -= file.Length;
                if (totalSizeBytes <= maxSizeBytes)
                {
                    break;
                }
            }
        }

        private static long GetDirectorySize(string cacheRoot)
        {
            var directory = new DirectoryInfo(cacheRoot);
            if (!directory.Exists)
            {
                return 0;
            }

            long totalSize = 0;
            foreach (var file in directory.GetFiles("*.bin"))
            {
                totalSize += file.Length;
            }

            return totalSize;
        }

        private static void TryDeleteFile(FileInfo file)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Best-effort cleanup only.
            }
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

        private static float GetChannelScalarByName(MaterialChannel? channel, float fallback, params string[] parameterNames)
        {
            if (channel == null || parameterNames == null || parameterNames.Length == 0)
            {
                return fallback;
            }

            var parametersProperty = channel.GetType().GetProperty("Parameters");
            if (parametersProperty?.GetValue(channel) is not System.Collections.IEnumerable parameters)
            {
                return fallback;
            }

            foreach (var parameter in parameters)
            {
                var name = parameter?.GetType().GetProperty("Name")?.GetValue(parameter) as string;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                foreach (var parameterName in parameterNames)
                {
                    if (!string.Equals(name, parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = parameter?.GetType().GetProperty("Value")?.GetValue(parameter);
                    if (TryConvertSingle(value, out var scalar))
                    {
                        return scalar;
                    }
                }
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

            var pbrProp = material.GetType().GetProperty("PbrMetallicRoughness")
                ?? material.GetType().GetProperty("PBRMetallicRoughness");
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
                if (TryConvertSingle(metallicProp?.GetValue(pbr), out var metallic))
                {
                    target.MetallicFactor = metallic;
                }

                var roughnessProp = pbr.GetType().GetProperty("RoughnessFactor");
                if (TryConvertSingle(roughnessProp?.GetValue(pbr), out var roughness))
                {
                    target.RoughnessFactor = roughness;
                }
            }
        }

        private static bool TryConvertSingle(object? value, out float result)
        {
            switch (value)
            {
                case float f:
                    result = f;
                    return true;
                case double d:
                    result = (float)d;
                    return true;
                case decimal m:
                    result = (float)m;
                    return true;
                case int i:
                    result = i;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static string GeneratePrimitiveKey(MeshPrimitive prim, int vertexCount, int indexCount)
        {
            // Ключ обязан быть детерминированным между повторными импортами одной и той же модели.
            // Нельзя использовать GetHashCode(), иначе RenderResourceManager geometry cache будет расти бесконечно.
            var material = prim.Material;
            var materialLogicalIndex = material?.LogicalIndex ?? -1;
            var materialName = material?.Name ?? "default";
            var meshLogicalIndex = prim.LogicalParent?.LogicalIndex ?? -1;
            var primitiveLogicalIndex = prim.LogicalIndex;

            return $"prim_{meshLogicalIndex}_{primitiveLogicalIndex}_{vertexCount}_{indexCount}_{materialLogicalIndex}_{materialName}";
        }

        private static void CleanupTextureCache()
        {
            _textureDecodeCache.TrimToSize(MemoryManager.Settings.MaxDecodedTextureCacheMemoryMB * 1024L * 1024L);
        }

        internal static int MaterialIndexMapCacheCount
        {
            get
            {
                lock (_materialIndexMapCache)
                {
                    return _materialIndexMapCache.Count;
                }
            }
        }

        public static void ClearAllCaches()
        {
            lock (_materialIndexMapCache)
            {
                _materialIndexMapCache.Clear();
            }

            _textureDecodeCache.Clear();

            _memoryPressurePolicy.OnMemoryPressure("ModelLoader.ClearAllCaches");

            Log.Information("All ModelLoader caches cleared");
        }

        private readonly record struct DecodedTextureCacheKey(string SourceIdentity, int TargetDimension, TextureDecodeMode SamplerMode);

        private sealed class LruTextureDecodeCache
        {
            private sealed class Entry
            {
                public required DecodedTextureCacheKey Key;
                public required TextureData Texture;
                public required long SizeBytes;
            }

            private readonly Dictionary<DecodedTextureCacheKey, LinkedListNode<Entry>> _map = new();
            private readonly LinkedList<Entry> _lru = new();
            private readonly object _sync = new();
            private long _currentSizeBytes;

            public bool TryGet(DecodedTextureCacheKey key, out TextureData texture)
            {
                lock (_sync)
                {
                    if (!_map.TryGetValue(key, out var node))
                    {
                        texture = null!;
                        return false;
                    }

                    _lru.Remove(node);
                    _lru.AddFirst(node);
                    texture = node.Value.Texture;
                    return true;
                }
            }

            public void Set(DecodedTextureCacheKey key, TextureData texture, long maxSizeBytes)
            {
                lock (_sync)
                {
                    if (_map.TryGetValue(key, out var existing))
                    {
                        _currentSizeBytes -= existing.Value.SizeBytes;
                        _lru.Remove(existing);
                        _map.Remove(key);
                    }

                    var size = texture.Data?.LongLength ?? 0;
                    var entry = new Entry { Key = key, Texture = texture, SizeBytes = size };
                    var node = new LinkedListNode<Entry>(entry);
                    _lru.AddFirst(node);
                    _map[key] = node;
                    _currentSizeBytes += size;

                    TrimInternal(maxSizeBytes);
                }
            }

            public void TrimToSize(long maxSizeBytes)
            {
                lock (_sync)
                {
                    TrimInternal(maxSizeBytes);
                }
            }

            public void Clear()
            {
                lock (_sync)
                {
                    _map.Clear();
                    _lru.Clear();
                    _currentSizeBytes = 0;
                }
            }

            private void TrimInternal(long maxSizeBytes)
            {
                while (_currentSizeBytes > maxSizeBytes && _lru.Last is { } tail)
                {
                    _lru.RemoveLast();
                    _map.Remove(tail.Value.Key);
                    _currentSizeBytes -= tail.Value.SizeBytes;
                }
            }
        }
    }
}

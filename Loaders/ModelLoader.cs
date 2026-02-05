// File: ModelLoader.cs - Optimized Version
using Avalonia3D.Model;
using Serilog;
using SharpGLTF.Schema2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime;

namespace Avalonia3D.Loaders
{
    public static class ModelLoader
    {
        // Убираем глобальный кеш - будем использовать только GPU-кеш из MeshObject
        private static readonly Dictionary<string, WeakReference<byte[]>> TextureCache = new();
        private static readonly object TextureCacheLock = new();

        private unsafe static long EstimateModelMemory(Model.Model m)
        {
            long v = (m.Vertices?.LongLength ?? 0) * sizeof(Vertex);
            long i = (m.Indices?.LongLength ?? 0) * sizeof(uint);
            long t = (m.TextureData?.Data?.LongLength ?? 0);
            return v + i + t;
        }

        private static TextureData LoadTextureFromImage(byte[] imageData, int maxDimension = 512) // Уменьшен размер по умолчанию
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
                var pool = ArrayPool<byte>.Shared;
                byte[] rented = pool.Rent(size);

                var span = rented.AsSpan(0, size);
                image.CopyPixelDataTo(span);

                return new TextureData
                {
                    Width = w,
                    Height = h,
                    Data = rented,
                    DataIsPooled = true
                };
            }
            catch (Exception ex)
            {
                Log.Information($"Texture load/resize error: {ex}");
                return null;
            }
        }

        public static List<Model.Model> LoadModels(ModelRoot gltf)
        {
            var models = new List<Model.Model>();
            if (gltf == null) return models;

            try
            {
                foreach (var node in gltf.LogicalNodes)
                {
                    models.AddRange(LoadModelsForNode(node));
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

        public static List<Model.Model> LoadModelsForNode(Node node)
        {
            var models = new List<Model.Model>();
            if (node?.Mesh == null)
            {
                return models;
            }

            foreach (var prim in node.Mesh.Primitives)
            {
                var model = LoadPrimitive(prim, node);
                if (model != null)
                {
                    models.Add(model);

                    var mem = EstimateModelMemory(model);
                    Log.Information($"Loaded model '{model.Name}' CPU memory: {mem:N0} bytes");
                }
            }

            return models;
        }

        internal static Model.Model LoadPrimitive(MeshPrimitive prim, Node node)
        {
            var posAccessor = prim.GetVertexAccessor("POSITION");
            if (posAccessor == null) return null;

            var positions = posAccessor.AsVector3Array();
            var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
            var texcoords = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
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
                        TexCoord = (i < (texcoords?.Count ?? 0)) ? texcoords[i] : Vector2.Zero
                    };
                }
            }

            // Индексы
            uint[] indices = indicesAccessor?.AsIndicesArray()
                .Select(idx => (uint)idx)
                .ToArray() ?? Array.Empty<uint>();

            var model = new Model.Model
            {
                Name = $"{node.Name}_{prim.Material?.Name ?? "mat"}",
                PrimitiveKey = primitiveKey,
                Vertices = vertices,
                Indices = indices,
                LocalMatrix = node.LocalMatrix
            };

            // Загрузка материала и текстур с кешированием
            LoadMaterialForModel(model, prim);

            return model;
        }

        private static void LoadMaterialForModel(Model.Model model, MeshPrimitive prim)
        {
            if (model == null)
            {
                return;
            }

            var material = prim.Material;
            if (material == null)
            {
                return;
            }

            var result = new Avalonia3D.Model.Material();
            ApplyPbrFactors(material, result);
            result.Opacity = result.BaseColorFactor.W;

            var baseColorChannel = material.FindChannel("BaseColor");
            result.BaseColorTexture = LoadTextureFromChannel(baseColorChannel);
            if (baseColorChannel != null)
            {
                var baseColor = GetChannelColor(baseColorChannel, result.BaseColorFactor);
                result.BaseColorFactor = baseColor;
                result.Opacity = baseColor.W;
            }

            var normalChannel = material.FindChannel("Normal");
            result.NormalTexture = LoadTextureFromChannel(normalChannel);

            var metallicRoughnessChannel = material.FindChannel("MetallicRoughness");
            result.MetallicRoughnessTexture = LoadTextureFromChannel(metallicRoughnessChannel);

            var occlusionChannel = material.FindChannel("Occlusion");
            result.OcclusionTexture = LoadTextureFromChannel(occlusionChannel);
            if (occlusionChannel != null)
            {
                result.OcclusionStrength = GetChannelStrength(occlusionChannel, result.OcclusionStrength);
            }

            var emissiveChannel = material.FindChannel("Emissive");
            result.EmissiveTexture = LoadTextureFromChannel(emissiveChannel);
            if (emissiveChannel != null)
            {
                var emissiveColor = GetChannelColor(emissiveChannel, new Vector4(result.EmissiveFactor, 1f));
                result.EmissiveFactor = new Vector3(emissiveColor.X, emissiveColor.Y, emissiveColor.Z);
            }

            if (result.Opacity < 0.999f)
            {
                result.IsTransparent = true;
            }

            model.Material = result;
            model.TextureData = result.BaseColorTexture;
        }

        private static TextureData? LoadTextureFromChannel(MaterialChannel? channel)
        {
            var image = channel?.Texture?.PrimaryImage;
            if (image == null)
            {
                return null;
            }

            return LoadTextureFromImage(image);
        }

        private static TextureData? LoadTextureFromImage(SharpGLTF.Schema2.Image image)
        {
            var textureKey = image.Content.GetHashCode().ToString();

            lock (TextureCacheLock)
            {
                if (TextureCache.TryGetValue(textureKey, out var weakRef) &&
                    weakRef.TryGetTarget(out var cachedBytes))
                {
                    var copyBytes = new byte[cachedBytes.Length];
                    Array.Copy(cachedBytes, copyBytes, cachedBytes.Length);
                    return LoadTextureFromImage(copyBytes);
                }

                var texBytes = image.Content.Content.ToArray();
                var textureData = LoadTextureFromImage(texBytes);
                TextureCache[textureKey] = new WeakReference<byte[]>(texBytes);
                return textureData;
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
            lock (TextureCacheLock)
            {
                var keysToRemove = new List<string>();
                foreach (var kvp in TextureCache)
                {
                    if (!kvp.Value.TryGetTarget(out _))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    TextureCache.Remove(key);
                }
            }
        }

        public static void ClearAllCaches()
        {
            lock (TextureCacheLock)
            {
                TextureCache.Clear();
            }

            // Принудительная сборка мусора
            GC.Collect(2, GCCollectionMode.Aggressive);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive);

            Log.Information("All ModelLoader caches cleared");
        }
    }
}

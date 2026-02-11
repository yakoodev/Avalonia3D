// File: ModelLoader.cs - Optimized Version
using Avalonia3D.Model;
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
using System.Numerics;
using System.Runtime;

namespace Avalonia3D.Loaders
{
    public static class ModelLoader
    {
        private enum TextureAlphaHeuristicProfile
        {
            Strict,
            Balanced,
            Permissive
        }

        private static class TextureAlphaHeuristics
        {
            public const byte SoftTransparentAlphaThreshold = 253;
            public const byte RegularTransparentAlphaThreshold = 245;
            public const byte DeepTransparentAlphaThreshold = 64;
            public const byte OpaqueAlphaThreshold = 254;

            public const int MaxSamples = 8192;
            public const float MinOpaqueRatio = 0.05f;
            public const float MinDeepTransparentRatio = 0.001f;
            public const float MinRegularTransparentRatio = 0.01f;
            public const float MinSoftTransparentRatio = 0.15f;

            public const float DenseDeepMaskOpaqueRatio = 0.35f;
            public const float StrictDenseDeepMaskRatio = 0.15f;
            public const float BalancedDenseDeepMaskRatio = 0.20f;
            public const float PermissiveDenseDeepMaskRatio = 0.35f;

            public static TextureAlphaHeuristicProfile ActiveProfile { get; set; } = TextureAlphaHeuristicProfile.Balanced;

            public static float GetDenseDeepMaskRatioThreshold()
            {
                return ActiveProfile switch
                {
                    TextureAlphaHeuristicProfile.Strict => StrictDenseDeepMaskRatio,
                    TextureAlphaHeuristicProfile.Permissive => PermissiveDenseDeepMaskRatio,
                    _ => BalancedDenseDeepMaskRatio,
                };
            }
        }

        private static readonly GltfMaterialExtensionsReader _materialExtensionsReader = new();

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
                MaterialKey = prim.Material != null ? $"material:{prim.Material.LogicalIndex}" : string.Empty,
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
            LoadMaterialForModel(model, prim);

            return model;
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
            var extensionPayload = _materialExtensionsReader.Read(material);
            ApplySurfaceSettings(material, result, extensionPayload);
            result.Opacity = result.BaseColorFactor.W;

            var baseColorChannel = material.FindChannel("BaseColor");
            result.BaseColorTexture = LoadTextureFromChannel(baseColorChannel);
            SyncTextureRuntimeTransform(result, TextureSemantic.BaseColor, result.BaseColorTexture);
            if (baseColorChannel != null)
            {
                var baseColor = GetChannelColor(baseColorChannel, result.BaseColorFactor);
                result.BaseColorFactor = baseColor;
                result.Opacity = baseColor.W;
            }

            var normalChannel = material.FindChannel("Normal");
            result.NormalTexture = LoadTextureFromChannel(normalChannel);
            SyncTextureRuntimeTransform(result, TextureSemantic.Normal, result.NormalTexture);

            var metallicRoughnessChannel = material.FindChannel("MetallicRoughness");
            result.MetallicRoughnessTexture = LoadTextureFromChannel(metallicRoughnessChannel);
            SyncTextureRuntimeTransform(result, TextureSemantic.MetallicRoughness, result.MetallicRoughnessTexture);

            var occlusionChannel = material.FindChannel("Occlusion");
            result.OcclusionTexture = LoadTextureFromChannel(occlusionChannel);
            SyncTextureRuntimeTransform(result, TextureSemantic.Occlusion, result.OcclusionTexture);
            if (occlusionChannel != null)
            {
                result.OcclusionStrength = GetChannelStrength(occlusionChannel, result.OcclusionStrength);
            }

            result.EmissiveFactor = ReadEmissiveFactor(material, result.EmissiveFactor);

            var emissiveChannel = material.FindChannel("Emissive");
            result.EmissiveTexture = LoadTextureFromChannel(emissiveChannel);
            SyncTextureRuntimeTransform(result, TextureSemantic.Emissive, result.EmissiveTexture);
            if (emissiveChannel != null)
            {
                var emissiveColor = GetChannelColor(emissiveChannel, new Vector4(result.EmissiveFactor, 1f));
                result.EmissiveFactor = new Vector3(emissiveColor.X, emissiveColor.Y, emissiveColor.Z);
            }


            var extensionChannels = extensionPayload.TextureChannels;
            result.ExtensionTextures.ClearcoatTexture = LoadTextureFromChannel(extensionChannels.Clearcoat);
            result.ExtensionTextures.ClearcoatRoughnessTexture = LoadTextureFromChannel(extensionChannels.ClearcoatRoughness);
            result.ExtensionTextures.ClearcoatNormalTexture = LoadTextureFromChannel(extensionChannels.ClearcoatNormal);
            result.ExtensionTextures.SheenColorTexture = LoadTextureFromChannel(extensionChannels.SheenColor);
            result.ExtensionTextures.SheenRoughnessTexture = LoadTextureFromChannel(extensionChannels.SheenRoughness);
            result.ExtensionTextures.SpecularTexture = LoadTextureFromChannel(extensionChannels.Specular);
            result.ExtensionTextures.SpecularColorTexture = LoadTextureFromChannel(extensionChannels.SpecularColor);
            result.ExtensionTextures.TransmissionTexture = LoadTextureFromChannel(extensionChannels.Transmission);
            result.ExtensionTextures.VolumeThicknessTexture = LoadTextureFromChannel(extensionChannels.VolumeThickness);

            result.HasTextureTransparency = HasMeaningfulTextureTransparency(result.BaseColorTexture);
            ApplyAlphaFallbackForUnsupportedBlend(result);

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

        private static void ApplyAlphaFallbackForUnsupportedBlend(Avalonia3D.Model.Material material)
        {
            if (material == null || material.AlphaMode != MaterialAlphaMode.Blend)
            {
                return;
            }

            var hasFactorTransparency = material.BaseColorFactor.W < 0.999f;
            if (!hasFactorTransparency && !material.HasTextureTransparency)
            {
                // Оставляем BLEND только если есть явный alpha-сигнал (factor либо значимая texture-alpha).
                material.AlphaMode = MaterialAlphaMode.Opaque;
            }
        }

        private static bool HasMeaningfulTextureTransparency(TextureData? texture)
        {
            if (texture?.Data == null || texture.Data.Length < 4)
            {
                return false;
            }

            var data = texture.Data;
            var pixelCount = data.Length / 4;
            if (pixelCount <= 0)
            {
                return false;
            }

            int sampled = 0;
            int softTransparent = 0;
            int regularTransparent = 0;
            int deepTransparent = 0;
            int opaque = 0;

            int stepPixels = Math.Max(1, pixelCount / TextureAlphaHeuristics.MaxSamples);
            var step = stepPixels * 4;

            for (int i = 3; i < data.Length; i += step)
            {
                sampled++;
                var alpha = data[i];

                if (alpha <= TextureAlphaHeuristics.SoftTransparentAlphaThreshold)
                {
                    softTransparent++;
                }

                if (alpha <= TextureAlphaHeuristics.RegularTransparentAlphaThreshold)
                {
                    regularTransparent++;
                }

                if (alpha <= TextureAlphaHeuristics.DeepTransparentAlphaThreshold)
                {
                    deepTransparent++;
                }

                if (alpha >= TextureAlphaHeuristics.OpaqueAlphaThreshold)
                {
                    opaque++;
                }
            }

            if (sampled == 0)
            {
                return false;
            }

            var opaqueRatio = opaque / (float)sampled;
            if (opaqueRatio < TextureAlphaHeuristics.MinOpaqueRatio)
            {
                // Если texture alpha почти полностью прозрачная, это обычно служебный канал,
                // а не осмысленная геометрическая прозрачность.
                return false;
            }

            var deepTransparentRatio = deepTransparent / (float)sampled;
            var denseDeepMaskThreshold = TextureAlphaHeuristics.GetDenseDeepMaskRatioThreshold();
            if (deepTransparentRatio > denseDeepMaskThreshold &&
                opaqueRatio >= TextureAlphaHeuristics.DenseDeepMaskOpaqueRatio)
            {
                // Плотная глубокая alpha вместе с заметной долей полностью непрозрачных пикселей
                // обычно означает маску экспорта/вырез, а не полупрозрачную поверхность.
                return false;
            }

            if (deepTransparentRatio >= TextureAlphaHeuristics.MinDeepTransparentRatio)
            {
                return true;
            }

            var regularTransparentRatio = regularTransparent / (float)sampled;
            if (regularTransparentRatio >= TextureAlphaHeuristics.MinRegularTransparentRatio)
            {
                return true;
            }

            var softTransparentRatio = softTransparent / (float)sampled;
            return softTransparentRatio >= TextureAlphaHeuristics.MinSoftTransparentRatio;
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
                return;
            }

            runtime.UvOffset = texture.Transform.Offset;
            runtime.UvScale = texture.Transform.Scale;
            runtime.UvRotation = texture.Transform.Rotation;
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

        private static TextureData? LoadTextureFromChannel(object? channel)
        {
            if (channel is MaterialChannel materialChannel)
            {
                return LoadTextureFromChannel(materialChannel);
            }

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

// File: ModelLoader.cs - Optimized Version
using Avalonia3D.Model;
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
using System.Text.Json;

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

        private sealed record MaterialExtensionPayload(
            float EmissiveStrength,
            float TransmissionFactor,
            float TransmissionThickness,
            float TransmissionIor,
            float TransmissionAttenuationDistance,
            Vector3 TransmissionAttenuationColor)
        {
            public static MaterialExtensionPayload Default { get; } = new(
                EmissiveStrength: 1f,
                TransmissionFactor: 0f,
                TransmissionThickness: 0f,
                TransmissionIor: 1.5f,
                TransmissionAttenuationDistance: float.PositiveInfinity,
                TransmissionAttenuationColor: Vector3.One);
        }


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
            ApplySurfaceSettings(material, result);
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

            result.HasTextureTransparency = HasMeaningfulTextureTransparency(result.BaseColorTexture);
            ApplyAlphaFallbackForUnsupportedBlend(result);

            result.IsTransparent = result.AlphaMode == MaterialAlphaMode.Blend;

            model.Material = result;
            model.TextureData = result.BaseColorTexture;
        }


        private static void ApplySurfaceSettings(SharpGLTF.Schema2.Material material, Avalonia3D.Model.Material target)
        {
            if (material == null || target == null)
            {
                return;
            }

            var extensionPayload = ReadMaterialExtensionPayload(material);

            target.AlphaMode = ParseAlphaMode(material.Alpha);
            target.AlphaCutoff = material.AlphaCutoff;
            target.DoubleSided = material.DoubleSided;
            target.EmissiveIntensity = extensionPayload.EmissiveStrength;
            target.TransmissionFactor = extensionPayload.TransmissionFactor;
            target.TransmissionThickness = extensionPayload.TransmissionThickness;
            target.TransmissionIor = extensionPayload.TransmissionIor;
            target.TransmissionAttenuationDistance = extensionPayload.TransmissionAttenuationDistance;
            target.TransmissionAttenuationColor = extensionPayload.TransmissionAttenuationColor;
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

        private static MaterialExtensionPayload ReadMaterialExtensionPayload(SharpGLTF.Schema2.Material material)
        {
            if (material == null)
            {
                return MaterialExtensionPayload.Default;
            }

            var payload = MaterialExtensionPayload.Default;
            var context = new ExtensionReadContext();
            TryReadExtensionValues(material, context, depth: 0);
            payload = context.ToPayload(payload);

            try
            {
                var extrasJson = material.Extras?.ToString();
                if (!string.IsNullOrWhiteSpace(extrasJson))
                {
                    using var doc = JsonDocument.Parse(extrasJson);
                    payload = MergePayload(payload, ParseMaterialExtensions(doc.RootElement));
                }
            }
            catch
            {
                // ignored, fall through to defaults
            }

            return payload;
        }

        private sealed class ExtensionReadContext
        {
            public float? EmissiveStrength { get; set; }
            public float? TransmissionFactor { get; set; }
            public float? TransmissionThickness { get; set; }
            public float? TransmissionIor { get; set; }
            public float? TransmissionAttenuationDistance { get; set; }
            public Vector3? TransmissionAttenuationColor { get; set; }

            public MaterialExtensionPayload ToPayload(MaterialExtensionPayload defaults)
            {
                return defaults with
                {
                    EmissiveStrength = Math.Max(0f, EmissiveStrength ?? defaults.EmissiveStrength),
                    TransmissionFactor = Math.Clamp(TransmissionFactor ?? defaults.TransmissionFactor, 0f, 1f),
                    TransmissionThickness = Math.Max(0f, TransmissionThickness ?? defaults.TransmissionThickness),
                    TransmissionIor = Math.Clamp(TransmissionIor ?? defaults.TransmissionIor, 1f, 3f),
                    TransmissionAttenuationDistance = (TransmissionAttenuationDistance ?? defaults.TransmissionAttenuationDistance) > 0f
                        ? (TransmissionAttenuationDistance ?? defaults.TransmissionAttenuationDistance)
                        : float.PositiveInfinity,
                    TransmissionAttenuationColor = TransmissionAttenuationColor ?? defaults.TransmissionAttenuationColor
                };
            }
        }

        private static void TryReadExtensionValues(object source, ExtensionReadContext context, int depth)
        {
            if (source == null || depth > 6)
            {
                return;
            }

            if (source is JsonElement jsonElement)
            {
                MergeExtensionContext(context, ParseMaterialExtensions(jsonElement));
                return;
            }

            if (source is string)
            {
                return;
            }

            if (source is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        TryReadExtensionValues(item, context, depth + 1);
                    }
                }

                return;
            }

            foreach (var property in source.GetType().GetProperties())
            {
                object value;
                try
                {
                    value = property.GetValue(source);
                }
                catch
                {
                    continue;
                }

                if (value == null)
                {
                    continue;
                }

                var name = property.Name;
                if (TryConvertToFloat(value, out var scalar))
                {
                    if (name.Contains("EmissiveStrength", StringComparison.OrdinalIgnoreCase))
                    {
                        context.EmissiveStrength = scalar;
                    }
                    else if (name.Contains("TransmissionFactor", StringComparison.OrdinalIgnoreCase))
                    {
                        context.TransmissionFactor = scalar;
                    }
                    else if (name.Contains("ThicknessFactor", StringComparison.OrdinalIgnoreCase) || name.Equals("Thickness", StringComparison.OrdinalIgnoreCase))
                    {
                        context.TransmissionThickness = scalar;
                    }
                    else if (name.Equals("Ior", StringComparison.OrdinalIgnoreCase) || name.Contains("Ior", StringComparison.OrdinalIgnoreCase) || name.Contains("Refraction", StringComparison.OrdinalIgnoreCase))
                    {
                        context.TransmissionIor = scalar;
                    }
                    else if (name.Contains("AttenuationDistance", StringComparison.OrdinalIgnoreCase))
                    {
                        context.TransmissionAttenuationDistance = scalar;
                    }
                }
                else if ((name.Contains("AttenuationColor", StringComparison.OrdinalIgnoreCase) || name.Equals("Color", StringComparison.OrdinalIgnoreCase)) &&
                         TryConvertToVector3(value, out var attenuationColor))
                {
                    context.TransmissionAttenuationColor = attenuationColor;
                }

                if (name.Contains("Extension", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Transmission", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Volume", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Ior", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Emissive", StringComparison.OrdinalIgnoreCase))
                {
                    TryReadExtensionValues(value, context, depth + 1);
                }
            }
        }

        private static bool TryConvertToFloat(object value, out float result)
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
                case uint ui:
                    result = ui;
                    return true;
                default:
                    result = 0f;
                    return false;
            }
        }

        private static bool TryConvertToVector3(object value, out Vector3 result)
        {
            result = default;

            if (value is Vector3 v3)
            {
                result = v3;
                return true;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                var values = new float[3];
                var index = 0;
                foreach (var item in enumerable)
                {
                    if (index >= 3 || item == null || !TryConvertToFloat(item, out values[index]))
                    {
                        break;
                    }

                    index++;
                }

                if (index == 3)
                {
                    result = new Vector3(values[0], values[1], values[2]);
                    return true;
                }
            }

            return false;
        }

        private static void MergeExtensionContext(ExtensionReadContext context, MaterialExtensionPayload payload)
        {
            context.EmissiveStrength = payload.EmissiveStrength;
            context.TransmissionFactor = payload.TransmissionFactor;
            context.TransmissionThickness = payload.TransmissionThickness;
            context.TransmissionIor = payload.TransmissionIor;
            context.TransmissionAttenuationDistance = payload.TransmissionAttenuationDistance;
            context.TransmissionAttenuationColor = payload.TransmissionAttenuationColor;
        }

        private static MaterialExtensionPayload ParseMaterialExtensions(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return MaterialExtensionPayload.Default;
            }

            var payload = MaterialExtensionPayload.Default;
            var extensionsNode = root;
            if (root.TryGetProperty("extensions", out var explicitExtensions) && explicitExtensions.ValueKind == JsonValueKind.Object)
            {
                extensionsNode = explicitExtensions;
            }

            if (extensionsNode.TryGetProperty("KHR_materials_emissive_strength", out var emissiveStrengthExtension) &&
                emissiveStrengthExtension.ValueKind == JsonValueKind.Object &&
                emissiveStrengthExtension.TryGetProperty("emissiveStrength", out var emissiveStrengthNode) &&
                emissiveStrengthNode.ValueKind == JsonValueKind.Number &&
                emissiveStrengthNode.TryGetSingle(out var emissiveStrength))
            {
                payload = payload with { EmissiveStrength = Math.Max(0f, emissiveStrength) };
            }

            if (extensionsNode.TryGetProperty("KHR_materials_transmission", out var transmissionExtension) &&
                transmissionExtension.ValueKind == JsonValueKind.Object)
            {
                var transmissionFactor = payload.TransmissionFactor;
                if (transmissionExtension.TryGetProperty("transmissionFactor", out var transmissionFactorNode) &&
                    transmissionFactorNode.ValueKind == JsonValueKind.Number &&
                    transmissionFactorNode.TryGetSingle(out var parsedTransmissionFactor))
                {
                    transmissionFactor = Math.Clamp(parsedTransmissionFactor, 0f, 1f);
                }

                payload = payload with { TransmissionFactor = transmissionFactor };
            }

            if (extensionsNode.TryGetProperty("KHR_materials_volume", out var volumeExtension) &&
                volumeExtension.ValueKind == JsonValueKind.Object)
            {
                var thickness = payload.TransmissionThickness;
                if (volumeExtension.TryGetProperty("thicknessFactor", out var thicknessNode) &&
                    thicknessNode.ValueKind == JsonValueKind.Number &&
                    thicknessNode.TryGetSingle(out var parsedThickness))
                {
                    thickness = Math.Max(0f, parsedThickness);
                }

                var attenuationDistance = payload.TransmissionAttenuationDistance;
                if (volumeExtension.TryGetProperty("attenuationDistance", out var attenuationDistanceNode) &&
                    attenuationDistanceNode.ValueKind == JsonValueKind.Number &&
                    attenuationDistanceNode.TryGetSingle(out var parsedDistance))
                {
                    attenuationDistance = parsedDistance > 0f ? parsedDistance : float.PositiveInfinity;
                }

                var attenuationColor = payload.TransmissionAttenuationColor;
                if (volumeExtension.TryGetProperty("attenuationColor", out var attenuationColorNode) &&
                    attenuationColorNode.ValueKind == JsonValueKind.Array)
                {
                    attenuationColor = ReadVector3(attenuationColorNode, attenuationColor);
                }

                payload = payload with
                {
                    TransmissionThickness = thickness,
                    TransmissionAttenuationDistance = attenuationDistance,
                    TransmissionAttenuationColor = attenuationColor
                };
            }

            if (extensionsNode.TryGetProperty("KHR_materials_ior", out var iorExtension) &&
                iorExtension.ValueKind == JsonValueKind.Object &&
                iorExtension.TryGetProperty("ior", out var iorNode) &&
                iorNode.ValueKind == JsonValueKind.Number &&
                iorNode.TryGetSingle(out var ior))
            {
                payload = payload with { TransmissionIor = Math.Clamp(ior, 1f, 3f) };
            }

            return payload;
        }

        private static MaterialExtensionPayload MergePayload(MaterialExtensionPayload baseline, MaterialExtensionPayload parsed)
        {
            return baseline with
            {
                EmissiveStrength = parsed.EmissiveStrength,
                TransmissionFactor = parsed.TransmissionFactor,
                TransmissionThickness = parsed.TransmissionThickness,
                TransmissionIor = parsed.TransmissionIor,
                TransmissionAttenuationDistance = parsed.TransmissionAttenuationDistance,
                TransmissionAttenuationColor = parsed.TransmissionAttenuationColor
            };
        }

        private static Vector3 ReadVector3(JsonElement arrayNode, Vector3 fallback)
        {
            var values = new float[3];
            var index = 0;
            foreach (var item in arrayNode.EnumerateArray())
            {
                if (index >= 3)
                {
                    break;
                }

                if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out values[index]))
                {
                    return fallback;
                }

                index++;
            }

            if (index < 3)
            {
                return fallback;
            }

            return new Vector3(values[0], values[1], values[2]);
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

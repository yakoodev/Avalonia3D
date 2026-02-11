using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Model3D = Avalonia3D.Model.Model;

namespace Avalonia3D.Rendering
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct VertexHalf
    {
        public Half Px; public Half Py; public Half Pz;
        public Half Nx; public Half Ny; public Half Nz;
        public Half U; public Half V;
        public Half U1; public Half V1;
    }

    public sealed class RenderResources
    {
        private readonly Dictionary<TextureSemantic, TextureBindingState> _textureBindings = new();
        private readonly IReadOnlyDictionary<TextureSemantic, TextureBindingState> _readonlyTextureBindings;

        public RenderResources()
        {
            _readonlyTextureBindings = new ReadOnlyDictionary<TextureSemantic, TextureBindingState>(_textureBindings);
        }

        public uint Vao { get; internal set; }
        public uint Vbo { get; internal set; }
        public uint Ebo { get; internal set; }
        public uint BaseColorTextureId { get; internal set; }
        public uint NormalTextureId { get; internal set; }
        public uint MetallicRoughnessTextureId { get; internal set; }
        public uint OcclusionTextureId { get; internal set; }
        public uint EmissiveTextureId { get; internal set; }
        public uint ClearcoatTextureId { get; internal set; }
        public uint ClearcoatRoughnessTextureId { get; internal set; }
        public uint ClearcoatNormalTextureId { get; internal set; }
        public uint SheenColorTextureId { get; internal set; }
        public uint SheenRoughnessTextureId { get; internal set; }
        public uint SpecularTextureId { get; internal set; }
        public uint SpecularColorTextureId { get; internal set; }
        public uint TransmissionTextureId { get; internal set; }
        public uint VolumeThicknessTextureId { get; internal set; }
        public int VertexCount { get; internal set; }
        public int IndexCount { get; internal set; }
        public bool IndicesUShort { get; internal set; }
        public TextureColorFlags TextureColorFlags { get; internal set; }
        public IReadOnlyDictionary<TextureSemantic, TextureBindingState> TextureBindings => _readonlyTextureBindings;
        internal string? CacheKey { get; set; }

        internal TextureBindingState GetTextureBindingState(TextureSemantic semantic)
            => _textureBindings.TryGetValue(semantic, out var state)
                ? state
                : new TextureBindingState(semantic, 0, false, TextureColorFlags.None, null, null, GLEnum.NoError, 0, 0, false, null);

        internal void SetTextureBindingState(TextureBindingState state)
        {
            _textureBindings[state.Semantic] = state;
        }

        internal void MarkTextureGpuBinding(TextureSemantic semantic, int textureUnit, bool wasBound)
        {
            var previous = GetTextureBindingState(semantic);
            SetTextureBindingState(previous with
            {
                LastBoundTextureUnit = textureUnit,
                WasBoundToGpu = wasBound
            });
        }
    }

    public sealed record TextureBindingState(
        TextureSemantic Semantic,
        uint TextureId,
        bool IsLoaded,
        TextureColorFlags FormatFlags,
        InternalFormat? PreferredInternalFormat,
        InternalFormat? UsedInternalFormat,
        GLEnum GlError,
        int Width,
        int Height,
        bool WasBoundToGpu,
        int? LastBoundTextureUnit);


    public sealed record InstanceBatchRequest(string Key, IReadOnlyList<MeshObject> Instances);

    public interface IInstanceBatchPlanner
    {
        bool TryBuildBatch(MeshObject candidate, out InstanceBatchRequest? batchRequest);
    }

    public sealed class RenderResourceManager
    {
        internal interface ITextureGlAdapter
        {
            uint GenTexture();
            void BindTexture(TextureTarget target, uint textureId);
            GLEnum GetError();
            unsafe void TexImage2D(TextureTarget target, int level, int internalFormat, uint width, uint height, int border, PixelFormat format, PixelType type, void* data);
            void TexParameter(TextureTarget target, TextureParameterName pname, int param);
            void GenerateMipmap(TextureTarget target);
            void DeleteTexture(uint textureId);
        }

        private sealed class GlTextureAdapter : ITextureGlAdapter
        {
            private readonly GL _gl;

            public GlTextureAdapter(GL gl)
            {
                _gl = gl;
            }

            public uint GenTexture() => _gl.GenTexture();
            public void BindTexture(TextureTarget target, uint textureId) => _gl.BindTexture(target, textureId);
            public GLEnum GetError() => _gl.GetError();
            public unsafe void TexImage2D(TextureTarget target, int level, int internalFormat, uint width, uint height, int border, PixelFormat format, PixelType type, void* data)
                => _gl.TexImage2D(target, level, internalFormat, width, height, border, format, type, data);
            public void TexParameter(TextureTarget target, TextureParameterName pname, int param) => _gl.TexParameter(target, pname, param);
            public void GenerateMipmap(TextureTarget target) => _gl.GenerateMipmap(target);
            public void DeleteTexture(uint textureId) => _gl.DeleteTexture(textureId);
        }

        private static readonly TextureSemantic[] DiagnosticSemantics =
        {
            TextureSemantic.BaseColor,
            TextureSemantic.Normal,
            TextureSemantic.MetallicRoughness,
            TextureSemantic.Occlusion,
            TextureSemantic.Emissive,
            TextureSemantic.Clearcoat,
            TextureSemantic.ClearcoatRoughness,
            TextureSemantic.ClearcoatNormal,
            TextureSemantic.SheenColor,
            TextureSemantic.SheenRoughness,
            TextureSemantic.Specular,
            TextureSemantic.SpecularColor,
            TextureSemantic.Transmission,
            TextureSemantic.VolumeThickness
        };

        private readonly Dictionary<string, GeometryInfo> _geometryCache = new();
        private readonly object _cacheLock = new();
        private readonly ITextureGlAdapter _textureGl;

        public RenderResourceManager(GL gl)
        {
            Gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _textureGl = new GlTextureAdapter(gl);
        }

        internal RenderResourceManager(ITextureGlAdapter textureGl)
        {
            _textureGl = textureGl ?? throw new ArgumentNullException(nameof(textureGl));
            Gl = null!;
        }

        public GL Gl { get; }
        public IInstanceBatchPlanner? InstanceBatchPlanner { get; set; }

        public bool TryGetInstancingBatch(MeshObject candidate, out InstanceBatchRequest? batchRequest)
        {
            batchRequest = null;
            if (candidate == null || InstanceBatchPlanner == null)
            {
                return false;
            }

            return InstanceBatchPlanner.TryBuildBatch(candidate, out batchRequest);
        }

        private sealed class GeometryInfo
        {
            public RenderResources Resources { get; init; }
            public int RefCount { get; set; }
            public DateTime LastAccessed { get; set; }
        }

        public RenderResources Acquire(Model3D model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (!string.IsNullOrEmpty(model.PrimitiveKey))
            {
                lock (_cacheLock)
                {
                    if (_geometryCache.TryGetValue(model.PrimitiveKey, out var info))
                    {
                        info.RefCount++;
                        info.LastAccessed = DateTime.UtcNow;
                        return info.Resources;
                    }
                }
            }

            var resources = CreateResources(model);
            resources.CacheKey = string.IsNullOrEmpty(model.PrimitiveKey) ? null : model.PrimitiveKey;

            if (!string.IsNullOrEmpty(resources.CacheKey))
            {
                var entry = new GeometryInfo
                {
                    Resources = resources,
                    RefCount = 1,
                    LastAccessed = DateTime.UtcNow
                };

                lock (_cacheLock)
                {
                    _geometryCache[resources.CacheKey] = entry;
                }
            }

            return resources;
        }

        public void Release(RenderResources resources)
        {
            if (resources == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(resources.CacheKey))
            {
                lock (_cacheLock)
                {
                    if (_geometryCache.TryGetValue(resources.CacheKey, out var info))
                    {
                        info.RefCount = Math.Max(0, info.RefCount - 1);
                        info.LastAccessed = DateTime.UtcNow;
                        return;
                    }
                }
            }

            DeleteResources(resources);
        }

        public void CleanupOldCacheEntries(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var keysToRemove = new List<string>();

            lock (_cacheLock)
            {
                foreach (var (key, info) in _geometryCache)
                {
                    if (info.RefCount == 0 && info.LastAccessed < cutoff)
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    if (_geometryCache.TryGetValue(key, out var info))
                    {
                        DeleteResources(info.Resources);
                        _geometryCache.Remove(key);
                    }
                }
            }

            if (keysToRemove.Count > 0)
            {
                Log.Information("Cleaned up {Count} old cache entries", keysToRemove.Count);
            }
        }

        public void ClearAll()
        {
            lock (_cacheLock)
            {
                foreach (var info in _geometryCache.Values)
                {
                    DeleteResources(info.Resources);
                }

                _geometryCache.Clear();
            }

            Log.Information("Geometry cache cleared");
        }

        public void LogCacheStats()
        {
            lock (_cacheLock)
            {
                Log.Information("Geometry Cache Stats:");
                Log.Information("  Total entries: {Count}", _geometryCache.Count);

                int activeRefs = 0;
                foreach (var info in _geometryCache.Values)
                {
                    activeRefs += info.RefCount;
                }

                Log.Information("  Active references: {Refs}", activeRefs);
            }
        }

        private void DeleteResources(RenderResources resources)
        {
            if (resources.Vao != 0)
            {
                Gl.DeleteVertexArray(resources.Vao);
            }

            if (resources.Vbo != 0)
            {
                Gl.DeleteBuffer(resources.Vbo);
            }

            if (resources.Ebo != 0)
            {
                Gl.DeleteBuffer(resources.Ebo);
            }

            DeleteTextureIfNeeded(resources.BaseColorTextureId);
            DeleteTextureIfNeeded(resources.NormalTextureId);
            DeleteTextureIfNeeded(resources.MetallicRoughnessTextureId);
            DeleteTextureIfNeeded(resources.OcclusionTextureId);
            DeleteTextureIfNeeded(resources.EmissiveTextureId);
            DeleteTextureIfNeeded(resources.ClearcoatTextureId);
            DeleteTextureIfNeeded(resources.ClearcoatRoughnessTextureId);
            DeleteTextureIfNeeded(resources.ClearcoatNormalTextureId);
            DeleteTextureIfNeeded(resources.SheenColorTextureId);
            DeleteTextureIfNeeded(resources.SheenRoughnessTextureId);
            DeleteTextureIfNeeded(resources.SpecularTextureId);
            DeleteTextureIfNeeded(resources.SpecularColorTextureId);
            DeleteTextureIfNeeded(resources.TransmissionTextureId);
            DeleteTextureIfNeeded(resources.VolumeThicknessTextureId);
        }

        private unsafe RenderResources CreateResources(Model3D model)
        {
            var resources = new RenderResources();

            resources.Vao = Gl.GenVertexArray();
            Gl.BindVertexArray(resources.Vao);

            resources.Vbo = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, resources.Vbo);

            if (model.Vertices != null && model.Vertices.Length > 0)
            {
                UploadVertexData(model.Vertices, resources);
                SetupVertexAttributes();
            }

            SetupIndexBuffer(model, resources);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            Gl.BindVertexArray(0);

            SetupMaterialTextures(model, resources);

            return resources;
        }


        public unsafe void UpdateVertexBuffer(RenderResources resources, Vertex[] vertices)
        {
            if (resources == null || vertices == null || vertices.Length == 0 || resources.Vbo == 0)
            {
                return;
            }

            Gl.BindBuffer(GLEnum.ArrayBuffer, resources.Vbo);

            int vertexCount = vertices.Length;
            var pool = ArrayPool<VertexHalf>.Shared;
            var halfVertices = pool.Rent(vertexCount);
            try
            {
                for (var i = 0; i < vertexCount; i++)
                {
                    var v = vertices[i];
                    halfVertices[i] = new VertexHalf
                    {
                        Px = (Half)v.Position.X,
                        Py = (Half)v.Position.Y,
                        Pz = (Half)v.Position.Z,
                        Nx = (Half)v.Normal.X,
                        Ny = (Half)v.Normal.Y,
                        Nz = (Half)v.Normal.Z,
                        U = (Half)v.TexCoord.X,
                        V = (Half)v.TexCoord.Y,
                        U1 = (Half)v.TexCoord1.X,
                        V1 = (Half)v.TexCoord1.Y
                    };
                }

                fixed (VertexHalf* pHalf = &halfVertices[0])
                {
                    Gl.BufferData(GLEnum.ArrayBuffer,
                        (nuint)(vertexCount * sizeof(VertexHalf)),
                        pHalf,
                        GLEnum.DynamicDraw);
                }
            }
            finally
            {
                pool.Return(halfVertices, true);
            }

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        }

        private void DeleteTextureIfNeeded(uint textureId)
        {
            if (textureId != 0)
            {
                _textureGl.DeleteTexture(textureId);
            }
        }

        private void SetupMaterialTextures(Model3D model, RenderResources resources)
        {
            EnsureTextureBindingDefaults(resources);

            var material = model.Material;
            var modelLabel = string.IsNullOrWhiteSpace(model.Name) ? (string.IsNullOrWhiteSpace(model.PrimitiveKey) ? "$unnamed-model" : model.PrimitiveKey) : model.Name;
            var materialLabel = material?.ShaderId ?? "$default-material";

            if (material == null)
            {
                if (model.TextureData != null)
                {
                    resources.BaseColorTextureId = SetupTexture(model.TextureData, TextureSemantic.BaseColor, resources, modelLabel, "$material-null");
                }

                return;
            }

            resources.BaseColorTextureId = SetupTexture(material.BaseColorTexture ?? model.TextureData, TextureSemantic.BaseColor, resources, modelLabel, materialLabel);
            resources.NormalTextureId = SetupTexture(material.NormalTexture, TextureSemantic.Normal, resources, modelLabel, materialLabel);
            resources.MetallicRoughnessTextureId = SetupTexture(material.MetallicRoughnessTexture, TextureSemantic.MetallicRoughness, resources, modelLabel, materialLabel);
            resources.OcclusionTextureId = SetupTexture(material.OcclusionTexture, TextureSemantic.Occlusion, resources, modelLabel, materialLabel);
            resources.EmissiveTextureId = SetupTexture(material.EmissiveTexture, TextureSemantic.Emissive, resources, modelLabel, materialLabel);
            resources.ClearcoatTextureId = SetupTexture(material.ExtensionTextures.ClearcoatTexture, TextureSemantic.Clearcoat, resources, modelLabel, materialLabel);
            resources.ClearcoatRoughnessTextureId = SetupTexture(material.ExtensionTextures.ClearcoatRoughnessTexture, TextureSemantic.ClearcoatRoughness, resources, modelLabel, materialLabel);
            resources.ClearcoatNormalTextureId = SetupTexture(material.ExtensionTextures.ClearcoatNormalTexture, TextureSemantic.ClearcoatNormal, resources, modelLabel, materialLabel);
            resources.SheenColorTextureId = SetupTexture(material.ExtensionTextures.SheenColorTexture, TextureSemantic.SheenColor, resources, modelLabel, materialLabel);
            resources.SheenRoughnessTextureId = SetupTexture(material.ExtensionTextures.SheenRoughnessTexture, TextureSemantic.SheenRoughness, resources, modelLabel, materialLabel);
            resources.SpecularTextureId = SetupTexture(material.ExtensionTextures.SpecularTexture, TextureSemantic.Specular, resources, modelLabel, materialLabel);
            resources.SpecularColorTextureId = SetupTexture(material.ExtensionTextures.SpecularColorTexture, TextureSemantic.SpecularColor, resources, modelLabel, materialLabel);
            resources.TransmissionTextureId = SetupTexture(material.ExtensionTextures.TransmissionTexture, TextureSemantic.Transmission, resources, modelLabel, materialLabel);
            resources.VolumeThicknessTextureId = SetupTexture(material.ExtensionTextures.VolumeThicknessTexture, TextureSemantic.VolumeThickness, resources, modelLabel, materialLabel);
        }

        private static void EnsureTextureBindingDefaults(RenderResources resources)
        {
            foreach (var semantic in DiagnosticSemantics)
            {
                resources.SetTextureBindingState(resources.GetTextureBindingState(semantic));
            }
        }

        private unsafe void UploadVertexData(Vertex[] vertices, RenderResources resources)
        {
            int vertexCount = vertices.Length;
            resources.VertexCount = vertexCount;

            var pool = ArrayPool<VertexHalf>.Shared;
            var halfVertices = pool.Rent(vertexCount);

            try
            {
                var batchSize = Math.Min(1024, vertexCount);
                for (int batch = 0; batch < vertexCount; batch += batchSize)
                {
                    int end = Math.Min(batch + batchSize, vertexCount);
                    for (int i = batch; i < end; i++)
                    {
                        var v = vertices[i];
                        halfVertices[i] = new VertexHalf
                        {
                            Px = (Half)v.Position.X,
                            Py = (Half)v.Position.Y,
                            Pz = (Half)v.Position.Z,
                            Nx = (Half)v.Normal.X,
                            Ny = (Half)v.Normal.Y,
                            Nz = (Half)v.Normal.Z,
                            U = (Half)v.TexCoord.X,
                            V = (Half)v.TexCoord.Y,
                            U1 = (Half)v.TexCoord1.X,
                            V1 = (Half)v.TexCoord1.Y
                        };
                    }
                }

                fixed (VertexHalf* p = &halfVertices[0])
                {
                    Gl.BufferData(GLEnum.ArrayBuffer,
                        (nuint)(vertexCount * sizeof(VertexHalf)),
                        p,
                        GLEnum.StaticDraw);
                }
            }
            finally
            {
                pool.Return(halfVertices, true);
            }
        }

        private unsafe void SetupIndexBuffer(Model3D model, RenderResources resources)
        {
            resources.IndexCount = 0;
            resources.Ebo = 0;
            resources.IndicesUShort = false;

            if (model.Indices == null || model.Indices.Length == 0)
            {
                model.Indices = null;
                return;
            }

            var indices = model.Indices;
            uint maxIndex = 0;

            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] > maxIndex)
                {
                    maxIndex = indices[i];
                }
            }

            resources.IndexCount = indices.Length;
            resources.Ebo = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, resources.Ebo);

            if (maxIndex <= ushort.MaxValue)
            {
                resources.IndicesUShort = true;
                var pool = ArrayPool<ushort>.Shared;
                var shortIndices = pool.Rent(indices.Length);

                try
                {
                    for (int i = 0; i < indices.Length; i++)
                    {
                        shortIndices[i] = (ushort)indices[i];
                    }

                    fixed (ushort* p = &shortIndices[0])
                    {
                        Gl.BufferData(GLEnum.ElementArrayBuffer,
                            (nuint)(indices.Length * sizeof(ushort)),
                            p,
                            GLEnum.StaticDraw);
                    }
                }
                finally
                {
                    pool.Return(shortIndices, true);
                }
            }
            else
            {
                fixed (uint* p = &indices[0])
                {
                    Gl.BufferData(GLEnum.ElementArrayBuffer,
                        (nuint)(indices.Length * sizeof(uint)),
                        p,
                        GLEnum.StaticDraw);
                }
            }
        }

        internal static InternalFormat ResolveInternalFormat(TextureSemantic semantic)
        {
            return semantic switch
            {
                TextureSemantic.BaseColor or TextureSemantic.Emissive => InternalFormat.SrgbAlpha,
                _ => InternalFormat.Rgba
            };
        }

        internal static bool IsSrgbSemantic(TextureSemantic semantic)
        {
            return semantic == TextureSemantic.BaseColor || semantic == TextureSemantic.Emissive;
        }


        internal static InternalFormat ResolveFallbackInternalFormat(TextureSemantic semantic)
        {
            return IsSrgbSemantic(semantic) ? InternalFormat.Rgba : ResolveInternalFormat(semantic);
        }

        internal unsafe uint SetupTextureForTests(TextureData? textureData, TextureSemantic semantic, RenderResources resources, string modelLabel, string materialLabel)
            => SetupTexture(textureData, semantic, resources, modelLabel, materialLabel);

        private unsafe uint SetupTexture(TextureData? textureData, TextureSemantic semantic, RenderResources resources, string modelLabel, string materialLabel)
        {
            var preferredInternalFormat = ResolveInternalFormat(semantic);
            var usedInternalFormat = preferredInternalFormat;
            var width = textureData?.Width ?? 0;
            var height = textureData?.Height ?? 0;
            var glError = GLEnum.NoError;
            uint textureId = 0;

            if (textureData == null || textureData.Data == null)
            {
                resources.SetTextureBindingState(new TextureBindingState(
                    semantic,
                    0,
                    false,
                    resources.TextureColorFlags,
                    preferredInternalFormat,
                    null,
                    glError,
                    width,
                    height,
                    false,
                    null));

                Log.Debug("Texture state: model={Model}, material={Material}, semantic={Semantic}, texture={TextureId}, preferredFormat={PreferredInternalFormat}, usedFormat={UsedInternalFormat}, glError={GlError}, size={Width}x{Height}", modelLabel, materialLabel, semantic, 0, preferredInternalFormat, null, glError, width, height);
                return 0;
            }

            textureId = _textureGl.GenTexture();
            if (textureId == 0)
            {
                resources.SetTextureBindingState(new TextureBindingState(
                    semantic,
                    0,
                    false,
                    resources.TextureColorFlags,
                    preferredInternalFormat,
                    null,
                    GLEnum.OutOfMemory,
                    width,
                    height,
                    false,
                    null));

                Log.Warning("Texture allocation failed before upload: model={Model}, material={Material}, semantic={Semantic}, texture={TextureId}, preferredFormat={PreferredInternalFormat}, usedFormat={UsedInternalFormat}, glError={GlError}, size={Width}x{Height}", modelLabel, materialLabel, semantic, 0, preferredInternalFormat, null, GLEnum.OutOfMemory, width, height);
                return 0;
            }

            _textureGl.BindTexture(TextureTarget.Texture2D, textureId);

            while (_textureGl.GetError() != GLEnum.NoError)
            {
            }

            fixed (byte* dataPtr = textureData.Data)
            {
                _textureGl.TexImage2D(TextureTarget.Texture2D, 0, (int)preferredInternalFormat,
                    (uint)textureData.Width, (uint)textureData.Height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, dataPtr);

                glError = _textureGl.GetError();

                if (glError != GLEnum.NoError && IsSrgbSemantic(semantic))
                {
                    while (_textureGl.GetError() != GLEnum.NoError)
                    {
                    }

                    usedInternalFormat = ResolveFallbackInternalFormat(semantic);
                    _textureGl.TexImage2D(TextureTarget.Texture2D, 0, (int)usedInternalFormat,
                        (uint)textureData.Width, (uint)textureData.Height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, dataPtr);

                    glError = _textureGl.GetError();
                }
            }

            if (glError == GLEnum.NoError)
            {
                SetTextureParameters();
                glError = _textureGl.GetError();
            }

            if (glError != GLEnum.NoError)
            {
                _textureGl.DeleteTexture(textureId);
                resources.SetTextureBindingState(new TextureBindingState(
                    semantic,
                    0,
                    false,
                    resources.TextureColorFlags,
                    preferredInternalFormat,
                    usedInternalFormat,
                    glError,
                    width,
                    height,
                    false,
                    null));

                Log.Warning("Texture upload failed and texture was deleted: model={Model}, material={Material}, semantic={Semantic}, texture={TextureId}, preferredFormat={PreferredInternalFormat}, usedFormat={UsedInternalFormat}, glError={GlError}, size={Width}x{Height}", modelLabel, materialLabel, semantic, textureId, preferredInternalFormat, usedInternalFormat, glError, width, height);
                return 0;
            }

            if (TextureColorManagement.ShouldFlagMissingSrgbDecode(semantic, preferredInternalFormat, usedInternalFormat))
            {
                resources.TextureColorFlags |= TextureColorManagement.GetMissingSrgbDecodeFlag(semantic);
            }

            resources.SetTextureBindingState(new TextureBindingState(
                semantic,
                textureId,
                true,
                resources.TextureColorFlags,
                preferredInternalFormat,
                usedInternalFormat,
                glError,
                width,
                height,
                false,
                null));

            Log.Information("Texture loaded: model={Model}, material={Material}, semantic={Semantic}, texture={TextureId}, preferredFormat={PreferredInternalFormat}, usedFormat={UsedInternalFormat}, glError={GlError}, size={Width}x{Height}, colorFlags={TextureColorFlags}", modelLabel, materialLabel, semantic, textureId, preferredInternalFormat, usedInternalFormat, glError, width, height, resources.TextureColorFlags);

            return textureId;
        }

        private void SetTextureParameters()
        {
            _textureGl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter,
                (int)GLEnum.LinearMipmapLinear);

            _textureGl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter,
                (int)GLEnum.Linear);

            _textureGl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS,
                (int)GLEnum.Repeat);

            _textureGl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT,
                (int)GLEnum.Repeat);

            _textureGl.GenerateMipmap(TextureTarget.Texture2D);
        }

        private unsafe void SetupVertexAttributes()
        {
            uint stride = (uint)sizeof(VertexHalf);

            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.HalfFloat, false, stride, (void*)0);

            Gl.EnableVertexAttribArray(1);
            Gl.VertexAttribPointer(1, 3, VertexAttribPointerType.HalfFloat, false, stride, (void*)6);

            Gl.EnableVertexAttribArray(2);
            Gl.VertexAttribPointer(2, 2, VertexAttribPointerType.HalfFloat, false, stride, (void*)12);

            Gl.EnableVertexAttribArray(3);
            Gl.VertexAttribPointer(3, 2, VertexAttribPointerType.HalfFloat, false, stride, (void*)16);
        }
    }
}

using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
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
    }

    public sealed class RenderResources
    {
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
        internal string? CacheKey { get; set; }
    }


    public sealed record InstanceBatchRequest(string Key, IReadOnlyList<MeshObject> Instances);

    public interface IInstanceBatchPlanner
    {
        bool TryBuildBatch(MeshObject candidate, out InstanceBatchRequest? batchRequest);
    }

    public sealed class RenderResourceManager
    {
        private readonly Dictionary<string, GeometryInfo> _geometryCache = new();
        private readonly object _cacheLock = new();

        public RenderResourceManager(GL gl)
        {
            Gl = gl ?? throw new ArgumentNullException(nameof(gl));
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

        private void DeleteTextureIfNeeded(uint textureId)
        {
            if (textureId != 0)
            {
                Gl.DeleteTexture(textureId);
            }
        }

        private void SetupMaterialTextures(Model3D model, RenderResources resources)
        {
            var material = model.Material;

            if (material == null)
            {
                if (model.TextureData != null)
                {
                    resources.BaseColorTextureId = SetupTexture(model.TextureData);
                }

                return;
            }

            resources.BaseColorTextureId = SetupTexture(material.BaseColorTexture ?? model.TextureData);
            resources.NormalTextureId = SetupTexture(material.NormalTexture);
            resources.MetallicRoughnessTextureId = SetupTexture(material.MetallicRoughnessTexture);
            resources.OcclusionTextureId = SetupTexture(material.OcclusionTexture);
            resources.EmissiveTextureId = SetupTexture(material.EmissiveTexture);
            resources.ClearcoatTextureId = SetupTexture(material.ExtensionTextures.ClearcoatTexture);
            resources.ClearcoatRoughnessTextureId = SetupTexture(material.ExtensionTextures.ClearcoatRoughnessTexture);
            resources.ClearcoatNormalTextureId = SetupTexture(material.ExtensionTextures.ClearcoatNormalTexture);
            resources.SheenColorTextureId = SetupTexture(material.ExtensionTextures.SheenColorTexture);
            resources.SheenRoughnessTextureId = SetupTexture(material.ExtensionTextures.SheenRoughnessTexture);
            resources.SpecularTextureId = SetupTexture(material.ExtensionTextures.SpecularTexture);
            resources.SpecularColorTextureId = SetupTexture(material.ExtensionTextures.SpecularColorTexture);
            resources.TransmissionTextureId = SetupTexture(material.ExtensionTextures.TransmissionTexture);
            resources.VolumeThicknessTextureId = SetupTexture(material.ExtensionTextures.VolumeThicknessTexture);
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
                            V = (Half)v.TexCoord.Y
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

        private unsafe uint SetupTexture(TextureData? textureData)
        {
            if (textureData == null || textureData.Data == null)
            {
                return 0;
            }

            uint textureId = Gl.GenTexture();
            if (textureId == 0)
            {
                Log.Warning("Texture allocation failed before upload: {Width}x{Height}", textureData.Width, textureData.Height);
                return 0;
            }

            Gl.BindTexture(TextureTarget.Texture2D, textureId);

            while (Gl.GetError() != GLEnum.NoError)
            {
            }

            fixed (byte* dataPtr = textureData.Data)
            {
                Gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba,
                    (uint)textureData.Width, (uint)textureData.Height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, dataPtr);
            }

            SetTextureParameters();
            var glError = Gl.GetError();

            if (glError != GLEnum.NoError)
            {
                Log.Warning("Texture upload GL error {GlError} for {Width}x{Height}, texture {TextureId}", glError, textureData.Width, textureData.Height, textureId);
            }
            else
            {
                Log.Information("Texture loaded: {Width}x{Height}, ID: {TextureId}", textureData.Width, textureData.Height, textureId);
            }

            return textureId;
        }

        private void SetTextureParameters()
        {
            Gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter,
                (int)GLEnum.LinearMipmapLinear);

            Gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter,
                (int)GLEnum.Linear);

            Gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS,
                (int)GLEnum.Repeat);

            Gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT,
                (int)GLEnum.Repeat);

            Gl.GenerateMipmap(TextureTarget.Texture2D);
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
        }
    }
}

using Avalonia3D.Interfaces;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Avalonia3D.Model.StandObjects
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexHalf
    {
        public Half Px; public Half Py; public Half Pz;
        public Half Nx; public Half Ny; public Half Nz;
        public Half U; public Half V;
    }

    public class MeshObject : SceneObject
    {
        // GPU handles (may be owned by cache or instance)
        private uint _vao;
        private uint _vbo;
        private uint _ebo;
        private uint _textureId;

        // Counts and types
        private int _vertexCount;
        private int _indexCount;
        private bool _indicesAreUShort;

        private GL _gl;
        private Model _model;
        private string _cacheKeyUsed;

        private static readonly Dictionary<string, GeometryInfo> GeometryCache = new();
        private static readonly object CacheLock = new();

        private class GeometryInfo
        {
            public uint Vao;
            public uint Vbo;
            public uint Ebo;
            public uint TextureId;
            public int VertexCount;
            public int IndexCount;
            public bool IndicesUShort;
            public int RefCount;
            public DateTime LastAccessed;
        }

        public void Setup(GL gl, Model model)
        {
            if (gl == null || model == null) return;
            _gl = gl;
            _model = model;
            SetupModelBuffers(gl, model);
            if (model.TextureData != null && _textureId == 0)
            {
                SetupTexture(gl, model.TextureData);
            }
        }

        private static Vector3 GetCenterOfGravity(Vertex[] vertices)
        {
            if (vertices == null || vertices.Length == 0)
                return Vector3.Zero;

            Vector3 sum = Vector3.Zero;

            foreach (var v in vertices)
                sum += v.Position;

            return sum / vertices.Length;
        }

        public unsafe void SetupModelBuffers(GL gl, Model currentModel)
        {
            if (gl == null || currentModel == null) return;

            // Проверяем кеш первым делом
            if (!string.IsNullOrEmpty(currentModel.PrimitiveKey))
            {
                lock (CacheLock)
                {
                    if (GeometryCache.TryGetValue(currentModel.PrimitiveKey, out var info))
                    {
                        _vao = info.Vao;
                        _vbo = info.Vbo;
                        _ebo = info.Ebo;
                        _textureId = info.TextureId;
                        _vertexCount = info.VertexCount;
                        _indexCount = info.IndexCount;
                        _indicesAreUShort = info.IndicesUShort;
                        info.RefCount++;
                        info.LastAccessed = DateTime.UtcNow;
                        _cacheKeyUsed = currentModel.PrimitiveKey;                        
                        return;
                    }
                }
            }

            try
            {
                // VAO
                _vao = gl.GenVertexArray();
                gl.BindVertexArray(_vao);

                // VBO - пакетная обработка для больших моделей
                _vbo = gl.GenBuffer();
                gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);

                if (currentModel.Vertices != null && currentModel.Vertices.Length > 0)
                {
                    UploadVertexData(gl, currentModel.Vertices);
                    SetupVertexAttributes(gl);
                    Gravity = GetCenterOfGravity(currentModel.Vertices);
                }

                // EBO - оптимизированная загрузка индексов
                SetupIndexBuffer(gl, currentModel);

                // Unbind CPU buffers; keep EBO bound to VAO
                gl.BindBuffer(GLEnum.ArrayBuffer, 0);
                gl.BindVertexArray(0);

                // Кешируем результат
                CacheGeometry(currentModel);
            }
            finally
            {   

                // Принудительная сборка мусора для больших моделей
                if (_vertexCount > 10000 || _indexCount > 30000)
                {
                    GC.Collect(1, GCCollectionMode.Optimized);
                }
            }
        }

        private unsafe void UploadVertexData(GL gl, Vertex[] vertices)
        {
            int vc = vertices.Length;
            _vertexCount = vc;

            // Используем арендованный буфер для временных данных
            var pool = ArrayPool<VertexHalf>.Shared;
            var halfVertices = pool.Rent(vc);

            try
            {
                // Конвертируем batch-ами для лучшей производительности
                var batchSize = Math.Min(1024, vc);
                for (int batch = 0; batch < vc; batch += batchSize)
                {
                    int end = Math.Min(batch + batchSize, vc);
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
                    gl.BufferData(GLEnum.ArrayBuffer,
                        (nuint)(vc * sizeof(VertexHalf)),
                        p,
                        GLEnum.StaticDraw);
                }
            }
            finally
            {
                pool.Return(halfVertices, true); // Очищаем арендованный буфер
            }
        }

        private unsafe void SetupIndexBuffer(GL gl, Model currentModel)
        {
            _indexCount = 0;
            _ebo = 0;
            _indicesAreUShort = false;

            if (currentModel.Indices == null || currentModel.Indices.Length == 0)
            {
                currentModel.Indices = null;
                return;
            }

            var indices = currentModel.Indices;
            uint maxIndex = 0;

            // Находим максимальный индекс
            for (int i = 0; i < indices.Length; i++)
                if (indices[i] > maxIndex) maxIndex = indices[i];

            _indexCount = indices.Length;
            _ebo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ElementArrayBuffer, _ebo);

            if (maxIndex <= ushort.MaxValue)
            {
                // Используем 16-битные индексы для экономии памяти
                _indicesAreUShort = true;
                var pool = ArrayPool<ushort>.Shared;
                var shortIndices = pool.Rent(indices.Length);

                try
                {
                    for (int i = 0; i < indices.Length; i++)
                        shortIndices[i] = (ushort)indices[i];

                    fixed (ushort* p = &shortIndices[0])
                    {
                        gl.BufferData(GLEnum.ElementArrayBuffer,
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
                    gl.BufferData(GLEnum.ElementArrayBuffer,
                        (nuint)(indices.Length * sizeof(uint)),
                        p,
                        GLEnum.StaticDraw);
                }
            }            
        }      

        private void CacheGeometry(Model currentModel)
        {
            if (string.IsNullOrEmpty(currentModel.PrimitiveKey)) return;

            var gi = new GeometryInfo
            {
                Vao = _vao,
                Vbo = _vbo,
                Ebo = _ebo,
                TextureId = _textureId,
                VertexCount = _vertexCount,
                IndexCount = _indexCount,
                IndicesUShort = _indicesAreUShort,
                RefCount = 1,
                LastAccessed = DateTime.UtcNow
            };

            lock (CacheLock)
            {
                GeometryCache[currentModel.PrimitiveKey] = gi;
            }
            _cacheKeyUsed = currentModel.PrimitiveKey;
        }

        public unsafe void SetupTexture(GL gl, TextureData textureData)
        {
            if (textureData == null || textureData.Data == null) return;

            try
            {
                if (_textureId != 0)
                {
                    gl.DeleteTexture(_textureId);
                    _textureId = 0;
                }

                _textureId = gl.GenTexture();
                gl.BindTexture(TextureTarget.Texture2D, _textureId);

                fixed (byte* dataPtr = textureData.Data)
                {
                    gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba,
                        (uint)textureData.Width, (uint)textureData.Height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, dataPtr);
                }

                SetTextureParameters(gl);

                Log.Information($"Texture loaded: {textureData.Width}x{textureData.Height}, ID: {_textureId}");
            }
            finally
            {
               
            }
        }

        private void SetTextureParameters(GL gl)
        {
            if (gl == null) return;

            gl.TexParameter(TextureTarget.Texture2D,
                 TextureParameterName.TextureMinFilter,
                 (int)GLEnum.LinearMipmapLinear);

            gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter,
                (int)GLEnum.Linear);

            gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS,
                (int)GLEnum.Repeat);

            gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT,
                (int)GLEnum.Repeat);

            gl.GenerateMipmap(TextureTarget.Texture2D);
        }

        private unsafe void SetupVertexAttributes(GL gl)
        {
            uint stride = (uint)sizeof(VertexHalf);

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.HalfFloat, false, stride, (void*)0);

            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.HalfFloat, false, stride, (void*)6);

            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 2, VertexAttribPointerType.HalfFloat, false, stride, (void*)12);
        }

        public override unsafe void Render(IRenderContext renderContext)
        {
            if (_gl == null) return;          

            foreach (var shader in renderContext.Scene.Shaders)
            {
                shader.BindTexture(_textureId);
                shader.SetUniforms(renderContext, this);
                RenderModel();
            }
        }

        public unsafe void RenderModel()
        {
            if (_gl == null) return;
            _gl.BindVertexArray(_vao);

            if (_indexCount > 0)
            {
                var drawType = _indicesAreUShort ? DrawElementsType.UnsignedShort : DrawElementsType.UnsignedInt;
                _gl.DrawElements(PrimitiveType.Triangles,
                    (uint)_indexCount,
                    drawType,
                    null);
            }
            else if (_vertexCount > 0)
            {
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
            }

            _gl.BindVertexArray(0);
        }

        public override void Dispose()
        {
            if (_gl == null) return;

            if (!string.IsNullOrEmpty(_cacheKeyUsed))
            {
                lock (CacheLock)
                {
                    if (GeometryCache.TryGetValue(_cacheKeyUsed, out var gi))
                    {
                        gi.RefCount--;
                        gi.LastAccessed = DateTime.UtcNow;
                        // ⚠️ НЕ удаляем GPU ресурсы здесь!
                        // Они останутся до ручной очистки
                    }
                }
                _cacheKeyUsed = null;
            }
            else
            {
                // Одноразовый объект (без кеша)
                CleanupGPUResources();
            }

            _vao = _vbo = _ebo = _textureId = 0;
            _model = null;
            _gl = null;
        }

        private void CleanupGeometryInfo(GeometryInfo gi)
        {
            if (gi.Vao != 0) _gl.DeleteVertexArray(gi.Vao);
            if (gi.Vbo != 0) _gl.DeleteBuffer(gi.Vbo);
            if (gi.Ebo != 0) _gl.DeleteBuffer(gi.Ebo);
            if (gi.TextureId != 0) _gl.DeleteTexture(gi.TextureId);
        }

        private void CleanupGPUResources()
        {
            if (_vao != 0) _gl.DeleteVertexArray(_vao);
            if (_vbo != 0) _gl.DeleteBuffer(_vbo);
            if (_ebo != 0) _gl.DeleteBuffer(_ebo);
            if (_textureId != 0) _gl.DeleteTexture(_textureId);
        }

        // Методы для управления кешем
        public static void CleanupOldCacheEntries(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var keysToRemove = new List<string>();

            lock (CacheLock)
            {
                foreach (var kvp in GeometryCache)
                {
                    if (kvp.Value.RefCount == 0 && kvp.Value.LastAccessed < cutoff)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    var gi = GeometryCache[key];
                    // Note: Нужно передать GL instance для очистки
                    GeometryCache.Remove(key);
                }
            }

            if (keysToRemove.Count > 0)
            {
                Log.Information($"Cleaned up {keysToRemove.Count} old cache entries");
            }
        }

        public static void ClearGeometryCache(GL gl)
        {
            lock (CacheLock)
            {
                foreach (var kvp in GeometryCache)
                {
                    var gi = kvp.Value;
                    if (gi.Vao != 0) gl.DeleteVertexArray(gi.Vao);
                    if (gi.Vbo != 0) gl.DeleteBuffer(gi.Vbo);
                    if (gi.Ebo != 0) gl.DeleteBuffer(gi.Ebo);
                    if (gi.TextureId != 0) gl.DeleteTexture(gi.TextureId);
                }
                GeometryCache.Clear();
            }
            Log.Information("Geometry cache cleared");
        }

        public static int GetCacheSize()
        {
            lock (CacheLock)
            {
                return GeometryCache.Count;
            }
        }

        public static void LogCacheStats()
        {
            lock (CacheLock)
            {
                Log.Information($"Geometry Cache Stats:");
                Log.Information($"  Total entries: {GeometryCache.Count}");

                int activeRefs = 0;
                foreach (var gi in GeometryCache.Values)
                {
                    activeRefs += gi.RefCount;
                }
                Log.Information($"  Active references: {activeRefs}");
            }
        }        
    }
}
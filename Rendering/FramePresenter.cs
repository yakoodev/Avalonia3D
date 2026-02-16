using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering
{
    public interface IFramePresenter
    {
        event Action<WriteableBitmap>? FrameReady;
        void Resize(int width, int height);
        void Present(GL gl, int width, int height);
    }

    public sealed class DirectSurfaceFramePresenter : IFramePresenter
    {
        public event Action<WriteableBitmap>? FrameReady;

        public void Resize(int width, int height)
        {
        }

        public void Present(GL gl, int width, int height)
        {
        }
    }

    public sealed class PboFramePresenter : IFramePresenter, IDisposable
    {
        private const int BufferCount = 2;
        private readonly uint[] _pbos = new uint[BufferCount];
        private GL? _gl;
        private bool _pboInitialized;
        private bool _hasData;
        private int _pboIndex;
        private int _width;
        private int _height;
        private int _bufferSize;
        private int _allocatedBufferSize;
        private WriteableBitmap? _bitmap;

        public event Action<WriteableBitmap>? FrameReady;

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (width == _width && height == _height)
            {
                return;
            }

            _width = width;
            _height = height;
            _bufferSize = checked(width * height * 4);

            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            _hasData = false;
        }

        public unsafe void Present(GL gl, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            Resize(width, height);
            EnsurePbos(gl);

            gl.PixelStore(PixelStoreParameter.PackAlignment, 1);

            int readIndex = _pboIndex;
            int mapIndex = (_pboIndex + 1) % BufferCount;

            gl.BindBuffer(GLEnum.PixelPackBuffer, _pbos[readIndex]);
            gl.ReadPixels(0, 0, (uint)width, (uint)height, GLEnum.Bgra, GLEnum.UnsignedByte, (int*)0);

            if (_hasData && _bitmap != null)
            {
                gl.BindBuffer(GLEnum.PixelPackBuffer, _pbos[mapIndex]);
                void* mapped = gl.MapBufferRange(GLEnum.PixelPackBuffer, 0, (nuint)_bufferSize, (uint)GLEnum.MapReadBit);

                if (mapped != null)
                {
                    using (var fbmp = _bitmap.Lock())
                    {
                        int stride = width * 4;
                        byte* dstBase = (byte*)fbmp.Address;
                        byte* srcBase = (byte*)mapped;

                        for (int y = 0; y < height; y++)
                        {
                            byte* src = srcBase + (height - 1 - y) * stride;
                            byte* dst = dstBase + y * fbmp.RowBytes;
                            System.Buffer.MemoryCopy(src, dst, stride, stride);
                        }
                    }

                    gl.UnmapBuffer(GLEnum.PixelPackBuffer);

                    var bitmap = _bitmap;
                    Dispatcher.UIThread.Post(() =>
                    {
                        FrameReady?.Invoke(bitmap);
                    }, DispatcherPriority.Render);
                }
            }

            gl.BindBuffer(GLEnum.PixelPackBuffer, 0);

            _pboIndex = mapIndex;
            _hasData = true;
        }

        public void Dispose()
        {
            if (_pboInitialized && _gl != null)
            {
                _gl.DeleteBuffers(_pbos);
                Array.Clear(_pbos, 0, _pbos.Length);
                _pboInitialized = false;
                _allocatedBufferSize = 0;
            }

            _bitmap?.Dispose();
            _bitmap = null;
            _hasData = false;
        }

        private unsafe void EnsurePbos(GL gl)
        {
            _gl = gl;

            if (_pboInitialized)
            {
                if (_allocatedBufferSize != _bufferSize)
                {
                    for (int i = 0; i < BufferCount; i++)
                    {
                        gl.BindBuffer(GLEnum.PixelPackBuffer, _pbos[i]);
                        gl.BufferData(GLEnum.PixelPackBuffer, (nuint)_bufferSize, null, GLEnum.StreamRead);
                    }

                    gl.BindBuffer(GLEnum.PixelPackBuffer, 0);
                    _allocatedBufferSize = _bufferSize;
                }
                return;
            }

            gl.GenBuffers(_pbos);
            for (int i = 0; i < BufferCount; i++)
            {
                gl.BindBuffer(GLEnum.PixelPackBuffer, _pbos[i]);
                gl.BufferData(GLEnum.PixelPackBuffer, (nuint)_bufferSize, null, GLEnum.StreamRead);
            }

            gl.BindBuffer(GLEnum.PixelPackBuffer, 0);
            _pboInitialized = true;
            _allocatedBufferSize = _bufferSize;
        }
    }
}

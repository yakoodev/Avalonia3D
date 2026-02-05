using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia3D.Interfaces;
using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Shaders;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Numerics;
using Vector = Avalonia.Vector;

namespace Avalonia3D.Controls
{
    namespace Avalonia3D.Rendering
    {
        public class GLRenderer3D : IRenderContext
        {
            private GL? _gl;
            private byte[]? _pixelBuffer;
            private WriteableBitmap? _bitmap;            


            public Scene3D Scene { get; } = new();

            public GL? GL => _gl;

            public event Action<WriteableBitmap>? FrameReady;

            public void Init(GL gl)
            {
                _gl = gl;
                Scene.Init(gl);                
                Scene.Shaders.Add(GLShader.Create(gl));                
                Scene.LoadModel(Path.Combine(AppContext.BaseDirectory, "Assets", "gltf"));
                ConfigureOpenGLState();
                InitializeCamera();
            }

            public void Resize(uint width, uint height)
            {
                if (_gl == null) return;
                _gl.Viewport(0, 0, width, height);
                Scene.Camera.Width = (int)width;
                Scene.Camera.Height = (int)height;
            }

            public unsafe void RenderFrame(int w, int h)
            {
                if (_gl == null) return;

                if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
                {
                    _pixelBuffer = new byte[w * h * 4];
                    _bitmap = new WriteableBitmap(
                        new PixelSize(w, h),
                        new Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        AlphaFormat.Unpremul);
                }

                // Рисуем сцену                
                _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                Scene.Render(this);

                // Чтение пикселей
                fixed (byte* ptr = _pixelBuffer)
                    _gl.ReadPixels(0, 0, (uint)w, (uint)h, GLEnum.Bgra, GLEnum.UnsignedByte, ptr);

                // Копируем в WriteableBitmap
                using (var fbmp = _bitmap.Lock())
                {
                    int stride = w * 4;
                    byte* dstBase = (byte*)fbmp.Address;

                    fixed (byte* srcBase = _pixelBuffer)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            byte* src = srcBase + (h - 1 - y) * stride; // flip по Y
                            byte* dst = dstBase + y * fbmp.RowBytes;
                            System.Buffer.MemoryCopy(src, dst, stride, stride);
                        }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    FrameReady?.Invoke(_bitmap);
                }, DispatcherPriority.Render);                      
            }          

            public void Clear()
            {
                Scene.Clear();
            }

            private void ConfigureOpenGLState()
            {
                if (_gl == null) return;
                _gl.Enable(EnableCap.DepthTest);
                _gl.DepthFunc(DepthFunction.Lequal);

                _gl.Disable(EnableCap.CullFace);
                _gl.CullFace(GLEnum.Back);
                _gl.FrontFace(FrontFaceDirection.Ccw);

                _gl.Disable(EnableCap.Blend);

                _gl.DepthMask(true);
                _gl.ColorMask(true, true, true, true);
            }

            private void InitializeCamera()
            {
                Scene.Lights.Add(new Light()
                {
                    Position = new Vector3(0f, 600.0f, 600.0f),
                    Color = new Vector3(1f, 1f, 1f),
                    Intensity = 0.5f
                });

                Scene.Lights.Add(new Light()
                {
                    Position = new Vector3(100f, 300, 300.0f),
                    Color = new Vector3(1f, 1f, 1f),
                    Intensity = 0.5f
                });

                Scene.Camera.Distance = Scene3DDefault.DistantionBase;
                Scene.Camera.Pitch = Scene3DDefault.PitchBase;
                Scene.Camera.Yaw = Scene3DDefault.YawBase;
                Scene.Camera.Fov = MathF.PI / 4;
                Scene.Camera.Near = 0.1f;
                Scene.Camera.Far = 1400f;
            }          
        }
    }

}

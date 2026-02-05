using System;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Avalonia3D.Controls
{
    unsafe class EglPbufferOffscreen : IDisposable
    {
        // --- EGL constants (нужные только те, что используются) ---
        private const int EGL_DEFAULT_DISPLAY = 0;
        private const int EGL_NO_DISPLAY = 0;
        private const int EGL_NO_SURFACE = 0;
        private const int EGL_NO_CONTEXT = 0;

        private const int EGL_SURFACE_TYPE = 0x3033;
        private const int EGL_PBUFFER_BIT = 0x0001;
        private const int EGL_RED_SIZE = 0x3024;
        private const int EGL_GREEN_SIZE = 0x3023;
        private const int EGL_BLUE_SIZE = 0x3022;
        private const int EGL_DEPTH_SIZE = 0x3025;
        private const int EGL_RENDERABLE_TYPE = 0x3040;
        private const int EGL_OPENGL_BIT = 0x0008;
        private const int EGL_NONE = 0x3038;
        private const int EGL_WIDTH = 0x3057;
        private const int EGL_HEIGHT = 0x3056;
        private const int EGL_OPENGL_API = 0x30A2;

        // --- P/Invoke для libEGL.so.1 ---
        private const string LibEgl = "libEGL.so.1";

        [DllImport(LibEgl)]
        private static extern IntPtr eglGetDisplay(IntPtr display);

        [DllImport(LibEgl)]
        private static extern bool eglInitialize(IntPtr dpy, out int major, out int minor);

        [DllImport(LibEgl)]
        private static extern bool eglChooseConfig(IntPtr dpy, int[] attribList, IntPtr[] configs, int config_size, out int num_config);

        [DllImport(LibEgl)]
        private static extern IntPtr eglCreatePbufferSurface(IntPtr dpy, IntPtr config, int[] attrib_list);

        [DllImport(LibEgl)]
        private static extern bool eglBindAPI(int api);

        [DllImport(LibEgl)]
        private static extern IntPtr eglCreateContext(IntPtr dpy, IntPtr config, IntPtr share_context, int[] attrib_list);

        [DllImport(LibEgl)]
        private static extern bool eglMakeCurrent(IntPtr dpy, IntPtr draw, IntPtr read, IntPtr ctx);

        [DllImport(LibEgl)]
        private static extern IntPtr eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procname);

        [DllImport(LibEgl)]
        private static extern bool eglSwapBuffers(IntPtr dpy, IntPtr surface);

        [DllImport(LibEgl)]
        private static extern bool eglDestroySurface(IntPtr dpy, IntPtr surface);

        [DllImport(LibEgl)]
        private static extern bool eglDestroyContext(IntPtr dpy, IntPtr ctx);

        [DllImport(LibEgl)]
        private static extern bool eglTerminate(IntPtr dpy);

        // --- поля экземпляра ---
        private IntPtr _display;
        private IntPtr _config;
        private IntPtr _surface;
        private IntPtr _context;
        public GL _gl; // Silk.NET OpenGL API
        private int _width;
        private int _height;
        private bool _inited;

        public void Init(int width, int height)
        {
            if (_inited) throw new InvalidOperationException("Already inited");
            _width = width;
            _height = height;

            // 1) get display
            _display = eglGetDisplay(new IntPtr(EGL_DEFAULT_DISPLAY));
            if (_display == IntPtr.Zero)
                throw new Exception("eglGetDisplay failed");

            // 2) init
            if (!eglInitialize(_display, out int maj, out int min))
                throw new Exception("eglInitialize failed");

            // 3) choose config
            int[] attribs = new[]
            {
            EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
            EGL_RED_SIZE, 8,
            EGL_GREEN_SIZE, 8,
            EGL_BLUE_SIZE, 8,
            EGL_DEPTH_SIZE, 24,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_BIT,
            EGL_NONE
        };

            IntPtr[] configs = new IntPtr[1];
            if (!eglChooseConfig(_display, attribs, configs, 1, out int numConfigs) || numConfigs == 0)
                throw new Exception("eglChooseConfig failed");

            _config = configs[0];

            // 4) create pbuffer surface
            int[] pbufAttribs = new[]
            {
            EGL_WIDTH, width,
            EGL_HEIGHT, height,
            EGL_NONE
        };
            _surface = eglCreatePbufferSurface(_display, _config, pbufAttribs);
            if (_surface == IntPtr.Zero)
                throw new Exception("eglCreatePbufferSurface failed");

            // 5) bind API OpenGL
            if (!eglBindAPI(EGL_OPENGL_API))
                throw new Exception("eglBindAPI failed");

            // 6) create context
            _context = eglCreateContext(_display, _config, IntPtr.Zero, new int[] { EGL_NONE });
            if (_context == IntPtr.Zero)
                throw new Exception("eglCreateContext failed");

            // 7) make current
            if (!eglMakeCurrent(_display, _surface, _surface, _context))
                throw new Exception("eglMakeCurrent failed");

            // 8) load GL api via eglGetProcAddress
            _gl = GL.GetApi(procName => eglGetProcAddress(procName));

            _inited = true;
        }

        ///// <summary>
        ///// Простая отрисовка + чтение пикселей RGBA8 (bottom-up — OpenGL даёт снизу вверх).
        ///// Возвращает байты в формате RGBA (строка0 — нижняя).
        ///// </summary>
        //public byte[] RenderAndReadPixels()
        //{
        //    if (!_inited) throw new InvalidOperationException("Not inited");

        //    // Пример рендера: красный фон
        //    _gl.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
        //    _gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

        //    // Если нужно, swap buffers (для pbuffer не обязателен, но безопасно)
        //    eglSwapBuffers(_display, _surface);

        //    // ReadPixels
        //    int size = _width * _height * 4;
        //    byte[] pixels = new byte[size];

        //    fixed (byte* p = pixels)
        //    {
        //        // 注意: glReadPixels читает в формате RGBA и снизу вверх
        //        _gl.ReadPixels(0, 0, (uint)_width, (uint)_height, (uint)PixelFormat.Rgba, (uint)PixelType.UnsignedByte, p);
        //    }

        //    return pixels;
        //}

        public void Dispose()
        {
            if (_inited)
            {
                eglMakeCurrent(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (_surface != IntPtr.Zero) eglDestroySurface(_display, _surface);
                if (_context != IntPtr.Zero) eglDestroyContext(_display, _context);
                eglTerminate(_display);
                _inited = false;
            }
        }       
    }
}

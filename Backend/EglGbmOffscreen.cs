// EglGbmOffscreen.cs
using System;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Serilog;
using System.IO;
using System.Text;

namespace Avalonia3D.Backend
{
    unsafe class EglGbmOffscreen : IDisposable
    {
        private const string LibEgl = "libEGL.so.1";
        private const string LibGbm = "libgbm.so.1";
        private const string LibC = "libc.so.6";

        // --- EGL constants ---
        private const int EGL_NONE = 0x3038;
        private const int EGL_PLATFORM_GBM_KHR = 0x31D7;
        private const int EGL_OPENGL_API = 0x30A2;
        private const int EGL_PBUFFER_BIT = 0x0001;
        private const int EGL_OPENGL_BIT = 0x0008;
        private const int EGL_DEFAULT_DISPLAY = 0;

        // EGL attribs
        private const int EGL_SURFACE_TYPE = 0x3033;
        private const int EGL_RED_SIZE = 0x3024;
        private const int EGL_GREEN_SIZE = 0x3023;
        private const int EGL_BLUE_SIZE = 0x3022;
        private const int EGL_ALPHA_SIZE = 0x3021;
        private const int EGL_DEPTH_SIZE = 0x3025;
        private const int EGL_RENDERABLE_TYPE = 0x3040;
        private const int EGL_WIDTH = 0x3057;
        private const int EGL_HEIGHT = 0x3056;

        // libc open flags
        private const int O_RDWR = 0x0002;
        private const int O_CLOEXEC = 0x80000;

        // --- P/Invoke EGL core ---
        [DllImport(LibEgl)] private static extern nint eglGetPlatformDisplayEXT(int platform, nint native_display, int[] attrib_list);
        [DllImport(LibEgl)] private static extern nint eglGetDisplay(nint display_id);
        [DllImport(LibEgl)] private static extern bool eglInitialize(nint dpy, out int major, out int minor);
        [DllImport(LibEgl)] private static extern bool eglChooseConfig(nint dpy, int[] attrib_list, nint[] configs, int config_size, out int num_config);
        [DllImport(LibEgl)] private static extern nint eglCreatePbufferSurface(nint dpy, nint config, int[] attrib_list);
        [DllImport(LibEgl)] private static extern bool eglBindAPI(int api);
        [DllImport(LibEgl)] private static extern nint eglCreateContext(nint dpy, nint config, nint share_context, int[] attrib_list);
        [DllImport(LibEgl)] private static extern bool eglMakeCurrent(nint dpy, nint draw, nint read, nint ctx);
        [DllImport(LibEgl)] private static extern nint eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procname);
        [DllImport(LibEgl)] private static extern bool eglSwapBuffers(nint dpy, nint surface);
        [DllImport(LibEgl)] private static extern bool eglDestroySurface(nint dpy, nint surface);
        [DllImport(LibEgl)] private static extern bool eglDestroyContext(nint dpy, nint ctx);
        [DllImport(LibEgl)] private static extern bool eglTerminate(nint dpy);
        [DllImport(LibEgl)] public static extern bool eglGetConfigAttrib(nint dpy, nint cfg, int attribute, out int value);

        // --- P/Invoke GBM ---
        [DllImport(LibGbm)] private static extern nint gbm_create_device(int fd);
        [DllImport(LibGbm)] private static extern void gbm_device_destroy(nint device);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nint eglGetPlatformDisplayEXTDelegate(int platform, nint native_display, int[] attrib_list);
        

        // --- P/Invoke libc open/close ---
        [DllImport(LibC, EntryPoint = "open", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, int flags);
        [DllImport(LibC, EntryPoint = "close", SetLastError = true)]
        private static extern int close(int fd);

        // instance fields
        private nint _display = nint.Zero;
        private nint _config = nint.Zero;
        private nint _surface = nint.Zero;
        private nint _context = nint.Zero;
        private nint _gbmDevice = nint.Zero;
        private int _drmFd = -1;

        public GL GL { get; private set; } = null!;
        private bool _inited;
        private uint _width, _height;
        private eglGetPlatformDisplayEXTDelegate _eglGetPlatformDisplayEXT;
        private uint _glFbo;
        private uint _glColorTex;
        private uint _glDepthRbo;

        public void Init(string renderNodePath, uint width, uint height)
        {
            if (_inited) throw new InvalidOperationException("Already initialized");            

            // пробуем получить eglGetPlatformDisplayEXT
            var addr = eglGetProcAddress("eglGetPlatformDisplayEXT");
            if (addr != nint.Zero)
            {
                _eglGetPlatformDisplayEXT =
                    Marshal.GetDelegateForFunctionPointer<eglGetPlatformDisplayEXTDelegate>(addr);
            }

            if (_eglGetPlatformDisplayEXT != null)
            {
                // открыть render node
                int fd = open(renderNodePath, O_RDWR);
                if (fd < 0)
                    throw new Exception("❌ Не удалось открыть render node: " + renderNodePath);

                nint gbmDevice = gbm_create_device(fd);
                if (gbmDevice == nint.Zero)
                    throw new Exception("❌ gbm_create_device failed");

                _display = _eglGetPlatformDisplayEXT(EGL_PLATFORM_GBM_KHR, gbmDevice, null);
                if (_display == nint.Zero)
                    throw new Exception("❌ eglGetPlatformDisplayEXT failed");
            }
            else
            {
                Console.WriteLine("⚠️ eglGetPlatformDisplayEXT не доступен, используем eglGetDisplay(EGL_DEFAULT_DISPLAY)");
                _display = eglGetDisplay(nint.Zero);
            }

            if (_display == nint.Zero)
                throw new Exception("Failed to obtain EGL display");

            if (!eglInitialize(_display, out int maj, out int min))
                throw new Exception("eglInitialize failed");

            // выбираем любой конфиг, для FBO не нужен pbuffer
            int[] attribs = new[]
            {        EGL_RENDERABLE_TYPE, EGL_OPENGL_BIT, // EGL_OPENGL_ES3_BIT_KHR
        EGL_NONE
    };

            if (!eglChooseConfig(_display, attribs, null, 0, out int numConfigs) || numConfigs == 0)
                throw new Exception("No EGL configs available");

            var configs = new nint[numConfigs];
            if (!eglChooseConfig(_display, attribs, configs, numConfigs, out numConfigs) || numConfigs == 0)
                throw new Exception("eglChooseConfig failed second call");

            _config = configs[0];

            // bind API and create context
            if (!eglBindAPI(EGL_OPENGL_API))
                throw new Exception("eglBindAPI failed");

            _context = eglCreateContext(_display, _config, nint.Zero, new int[] { EGL_NONE });
            if (_context == nint.Zero)
                throw new Exception("eglCreateContext failed");

            // MakeCurrent с нулевым surface = surfaceless
            if (!eglMakeCurrent(_display, nint.Zero, nint.Zero, _context))
                throw new Exception("eglMakeCurrent failed");

            // load GL functions
            GL = GL.GetApi(name => eglGetProcAddress(name));


            Resize(width, height);

            _inited = true;

            // log GL renderer
            var renderer = Marshal.PtrToStringAnsi((nint)GL.GetString(GLEnum.Renderer)) ?? "<null>";
            var vendor = Marshal.PtrToStringAnsi((nint)GL.GetString(GLEnum.Vendor)) ?? "<null>";
            var version = Marshal.PtrToStringAnsi((nint)GL.GetString(GLEnum.Version)) ?? "<null>";
            Log.Information("GL Vendor   : {Vendor}", vendor);
            Log.Information("GL Renderer : {Renderer}", renderer);
            Log.Information("GL Version  : {Version}", version);
        }

        public void Resize(uint width, uint height)
        {
            if ((_width == width) && (_height == height)) return;

            _width = width;
            _height = height;

            if (_glFbo != 0)
            {
                GL.DeleteFramebuffer(_glFbo);
                GL.DeleteTexture(_glColorTex);
                GL.DeleteRenderbuffer(_glDepthRbo);
            }

            Log.Verbose($"Resize  width: {width} height: {height}");

            // создаём FBO + текстуру для offscreen рендера
            _glFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(GLEnum.Framebuffer, _glFbo);

            _glColorTex = GL.GenTexture();
            GL.BindTexture(GLEnum.Texture2D, _glColorTex);
            GL.TexImage2D(GLEnum.Texture2D, 0, (int)InternalFormat.Rgba8, _width, _height, 0, GLEnum.Rgba, GLEnum.UnsignedByte, null);
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            GL.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, _glColorTex, 0);

            _glDepthRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(GLEnum.Renderbuffer, _glDepthRbo);
            GL.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, _width, _height);
            GL.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, _glDepthRbo);

            if (GL.CheckFramebufferStatus(GLEnum.Framebuffer) != GLEnum.FramebufferComplete)
                throw new Exception("FBO incomplete!");        
        }

        public unsafe byte[] RenderAndReadPixels()
        {
            if (!_inited) throw new InvalidOperationException("Not initialized");
            // make current again (safe)
            if (!eglMakeCurrent(_display, _surface, _surface, _context))
                throw new Exception("eglMakeCurrent failed");

            // clear red for test
            GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
            GL.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            GL.Finish();

            uint size = _width * _height * 4;
            byte[] pixels = new byte[size];
            fixed (byte* p = pixels)
            {
                // read in BGRA if you prefer, but here RGBA:
                GL.ReadPixels(0, 0, _width, _height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }

            return pixels;
        }

        public void Dispose()
        {
            try
            {
                if (_inited)
                {
                    eglMakeCurrent(_display, nint.Zero, nint.Zero, nint.Zero);
                    if (_surface != nint.Zero) eglDestroySurface(_display, _surface);
                    if (_context != nint.Zero) eglDestroyContext(_display, _context);
                    if (_display != nint.Zero) eglTerminate(_display);
                    _inited = false;
                }
            }
            finally
            {
                if (_gbmDevice != nint.Zero) gbm_device_destroy(_gbmDevice);
                if (_drmFd >= 0) { close(_drmFd); _drmFd = -1; }
            }
        }
    }
}


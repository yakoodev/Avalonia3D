using Avalonia.OpenGL;
using Serilog;
using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace Avalonia3D.Backend
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
        private const int EGL_ALPHA_SIZE = 0x3021;
        private const int EGL_DEPTH_SIZE = 0x3025;
        private const int EGL_RENDERABLE_TYPE = 0x3040;
        private const int EGL_OPENGL_BIT = 0x0008;
        private const int EGL_NONE = 0x3038;
        private const int EGL_WIDTH = 0x3057;
        private const int EGL_HEIGHT = 0x3056;
        private const int EGL_OPENGL_API = 0x30A2;        
        private const int EGL_VERSION = 0x3054;
        private const int EGL_VENDOR = 0x3053;
        private const int EGL_EXTENSIONS = 0x3055;
        private const int EGL_CLIENT_APIS = 0x308D;
        
        private const int EGL_DEVICE_EXTENSIONS = 0x3138; // not standardized, but many implementations provide query string
        private const int EGL_PLATFORM_DEVICE_EXT = 0x313F;          // EGL_EXT_platform_device
        private const int EGL_PLATFORM_GBM_KHR = 0x31D7;          // EGL_KHR_platform_gbm (if needed)

        // --- P/Invoke для libEGL.so.1 ---
        private const string LibEgl = "libEGL.so.1";

        [DllImport(LibEgl)]
        private static extern nint eglGetDisplay(nint display);

        [DllImport(LibEgl)]
        private static extern bool eglInitialize(nint dpy, out int major, out int minor);

        [DllImport(LibEgl)]
        private static extern bool eglChooseConfig(nint dpy, int[] attribList, nint[] configs, int config_size, out int num_config);

        [DllImport(LibEgl)]
        private static extern nint eglCreatePbufferSurface(nint dpy, nint config, int[] attrib_list);

        [DllImport(LibEgl)]
        private static extern bool eglBindAPI(int api);

        [DllImport(LibEgl)]
        private static extern nint eglCreateContext(nint dpy, nint config, nint share_context, int[] attrib_list);

        [DllImport(LibEgl)]
        private static extern bool eglMakeCurrent(nint dpy, nint draw, nint read, nint ctx);

        [DllImport(LibEgl)]
        private static extern nint eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procname);

        [DllImport(LibEgl)]
        private static extern bool eglSwapBuffers(nint dpy, nint surface);

        [DllImport(LibEgl)]
        private static extern bool eglDestroySurface(nint dpy, nint surface);

        [DllImport(LibEgl)]
        private static extern bool eglDestroyContext(nint dpy, nint ctx);

        [DllImport(LibEgl)]
        private static extern bool eglTerminate(nint dpy);

        [DllImport(LibEgl)]
        public static extern bool eglGetConfigAttrib(nint display, nint config, int attribute, out int value);

        [DllImport(LibEgl, CharSet = CharSet.Ansi)]
        private static extern nint eglQueryString(nint dpy, int name);

        // Note: these functions are provided by libEGL when corresponding extensions exist.
        [DllImport(LibEgl)]
        private static extern bool eglQueryDevicesEXT(int max_devices, [Out] nint[] devices, out int num_devices);

        [DllImport(LibEgl)]
        private static extern nint eglGetPlatformDisplayEXT(int platform, nint native_display, int[] attrib_list);

        [DllImport(LibEgl)]
        private static extern nint eglQueryDeviceStringEXT(nint device, int name);


        // --- поля экземпляра ---
        private nint _display;
        private nint _config;
        private nint _surface;
        private nint _context;
        public GL _gl; // Silk.NET OpenGL API
        private int _width;
        private int _height;
        private bool _inited;

        public static nint ChooseConfigWithAlpha(nint display)
        {
            // 1) Базовые требования
            int[] attribs = new[]
            {
        EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
        EGL_RED_SIZE, 8,
        EGL_GREEN_SIZE, 8,
        EGL_BLUE_SIZE, 8,
        EGL_ALPHA_SIZE, 8,
        EGL_DEPTH_SIZE, 24,
        EGL_RENDERABLE_TYPE, EGL_OPENGL_BIT,
        EGL_NONE
    };

            // 2) Получаем список всех конфигов
            if (!eglChooseConfig(display, attribs, null, 0, out int numConfigs) || numConfigs == 0)
                throw new Exception("eglChooseConfig failed: no configs");

            var configs = new nint[numConfigs];
            if (!eglChooseConfig(display, attribs, configs, configs.Length, out numConfigs) || numConfigs == 0)
                throw new Exception("eglChooseConfig failed: second call");

            // 3) Ищем конфиг с альфой
            foreach (var cfg in configs)
            {
                if (eglGetConfigAttrib(display, cfg, EGL_ALPHA_SIZE, out int alphaBits) && alphaBits >= 8)
                {
                    Console.WriteLine($"✅ Выбран EGLConfig с EGL_ALPHA_SIZE={alphaBits}");
                    return cfg;
                }
            }

            throw new Exception("Не найден EGLConfig с альфой (EGL_ALPHA_SIZE >= 8)");
        }

        string GetEglString(int name)
        {
            nint p = eglQueryString(_display, name);
            return p == nint.Zero ? string.Empty : Marshal.PtrToStringAnsi(p) ?? string.Empty;
        }

        // Try to enumerate devices and return an EGLDisplay bound to a physical device (first usable)
        private nint GetDisplayFromEglDevices()
        {
            try
            {
                // allocate array for a few devices
                nint[] devices = new nint[16];
                if (!eglQueryDevicesEXT(devices.Length, devices, out int numDevices) || numDevices == 0)
                {
                    Log.Debug("eglQueryDevicesEXT not available or returned no devices");
                    return nint.Zero;
                }

                Log.Information("eglQueryDevicesEXT found {Num} devices", numDevices);

                // Try devices one by one and get a platform display
                for (int i = 0; i < numDevices; i++)
                {
                    var dev = devices[i];
                    // Optionally query device string for debug (if eglQueryDeviceStringEXT available)
                    string devInfo = string.Empty;
                    try
                    {
                        nint ptr = eglQueryDeviceStringEXT(dev, EGL_EXTENSIONS);
                        if (ptr != nint.Zero)
                            devInfo = Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
                    }
                    catch { /*ignore*/ }

                    Log.Information("Device #{Index} info: {Info}", i, devInfo);

                    // Request platform display for this device
                    nint dpy = eglGetPlatformDisplayEXT(EGL_PLATFORM_DEVICE_EXT, dev, null);
                    if (dpy != nint.Zero)
                    {
                        Log.Information("eglGetPlatformDisplayEXT succeeded for device #{Index}", i);
                        return dpy;
                    }
                }

                Log.Warning("No EGL device produced a platform display");
                return nint.Zero;
            }
            catch (DllNotFoundException)
            {
                Log.Warning("eglQueryDevicesEXT/eglGetPlatformDisplayEXT not found in libEGL");
                return nint.Zero;
            }
            catch (EntryPointNotFoundException)
            {
                Log.Warning("eglQueryDevicesEXT/eglGetPlatformDisplayEXT not exported");
                return nint.Zero;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calling eglQueryDevicesEXT");
                return nint.Zero;
            }
        }

        public void Init(int width, int height)
        {
            if (_inited) throw new InvalidOperationException("Already inited");
            _width = width;
            _height = height;


            // 1) Try to get EGLDisplay from EGL device (preferred)
            _display = GetDisplayFromEglDevices();
            if (_display == nint.Zero)
            {
                Log.Warning("eglQueryDevicesEXT or platform display not available — falling back to eglGetDisplay(EGL_DEFAULT_DISPLAY)");
                _display = eglGetDisplay(new nint(0)); // EGL_DEFAULT_DISPLAY
            }

            if (_display == nint.Zero)
                throw new Exception("Failed to obtain EGLDisplay");

            // 1) get display
            _display = eglGetDisplay(new nint(EGL_DEFAULT_DISPLAY));
            if (_display == nint.Zero)
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
            EGL_ALPHA_SIZE, 8,
            EGL_DEPTH_SIZE, 24,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_BIT,
            EGL_NONE
        };            

            nint[] configs = new nint[1];
            if (!eglChooseConfig(_display, attribs, configs, 1, out int numConfigs) || numConfigs == 0)
                throw new Exception("eglChooseConfig failed");

            _config = ChooseConfigWithAlpha(_display);

            // 4) create pbuffer surface
            int[] pbufAttribs = new[]
            {
            EGL_WIDTH, width,
            EGL_HEIGHT, height,
            EGL_NONE
        };

            eglGetConfigAttrib(_display, _config, EGL_ALPHA_SIZE, out int alphaBits);
            Log.Information($"Alpha bits in EGLConfig: {alphaBits}");

            eglGetConfigAttrib(_display, _config, EGL_DEPTH_SIZE, out int depthBits);
            Log.Information($"Depth bits: {depthBits}");

            Log.Information($"EGL version: {GetEglString(EGL_VERSION)}");
            Log.Information($"EGL vendor: {GetEglString(EGL_VENDOR)}");
            Log.Information($"EGL client apis: {GetEglString(EGL_CLIENT_APIS)}");
            Log.Information($"EGL extensions: {GetEglString(EGL_EXTENSIONS)}");


            _surface = eglCreatePbufferSurface(_display, _config, pbufAttribs);
            if (_surface == nint.Zero)
                throw new Exception("eglCreatePbufferSurface failed");

            // 5) bind API OpenGL
            if (!eglBindAPI(EGL_OPENGL_API))
                throw new Exception("eglBindAPI failed");

            // 6) create context
            _context = eglCreateContext(_display, _config, nint.Zero, new int[] { EGL_NONE });
            if (_context == nint.Zero)
                throw new Exception("eglCreateContext failed");

            // 7) make current
            if (!eglMakeCurrent(_display, _surface, _surface, _context))
                throw new Exception("eglMakeCurrent failed");

            // 8) load GL api via eglGetProcAddress
            _gl = GL.GetApi(procName => eglGetProcAddress(procName));
            var vendor = Marshal.PtrToStringAnsi((nint)_gl.GetString(GLEnum.Vendor));
            var renderer = Marshal.PtrToStringAnsi((nint)_gl.GetString(GLEnum.Renderer));
            var version = Marshal.PtrToStringAnsi((nint)_gl.GetString(GLEnum.Version));
            var shadingLang = Marshal.PtrToStringAnsi((nint)_gl.GetString(GLEnum.ShadingLanguageVersion));

            Log.Information("GL Vendor   : {Vendor}", vendor);
            Log.Information("GL Renderer : {Renderer}", renderer);
            Log.Information("GL Version  : {Version}", version);
            Log.Information("GLSL        : {GLSL}", shadingLang);

            _inited = true;
        }

        public byte[] RenderAndReadPixels()
        {
            if (!_inited) throw new InvalidOperationException("Not inited");

            // Снова делаем контекст текущим на этом потоке
            if (!eglMakeCurrent(_display, _surface, _surface, _context))
                throw new Exception("eglMakeCurrent failed");

            _gl.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
            _gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

            // Чтение пикселей
            int size = _width * _height * 4;
            byte[] pixels = new byte[size];

            fixed (byte* p = pixels)
            {
                _gl.ReadPixels(0, 0, (uint)_width, (uint)_height, PixelFormat.Bgra, PixelType.UnsignedByte, p);
            }

            return pixels;
        }

        public void Dispose()
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
    }
}

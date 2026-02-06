using Avalonia;
using Avalonia.LinuxFramebuffer;
using Avalonia.LinuxFramebuffer.Input.LibInput;
using Avalonia3D.Loaders;
using System;
using System.Linq;

namespace Avalonia3D
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static int Main(string[] args)
        {
            ImportValidationConfiguration.Configure(ImportValidationConfiguration.ResolveFrom(args));

            if (OperatingSystem.IsLinux())
                return StartDRM(args);
            else
                return StartDefault(args);
        }
        private static int StartDefault(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        private static int StartDRM(string[] args)
        {
            string cardCommandPattern = "-card=";
            string card = "/dev/dri/card1";
            string? drmString = args.FirstOrDefault(t => t.StartsWith(cardCommandPattern));
            if (drmString != null)
            {
                var drmdevice = drmString.Replace(cardCommandPattern, "").Replace("=", "");

                if (string.IsNullOrWhiteSpace(drmdevice) == false)
                {
                    card = $"/dev/dri/{drmdevice}";
                }
            }

            string resolutionCommandPattern = "-resolution=";
            var resolutionCommand = args.FirstOrDefault(t => t.StartsWith(resolutionCommandPattern));
            PixelSize? resoulution = null;

            if (resolutionCommand != null)
            {
                var temp = resolutionCommand.ToLower().Replace(resolutionCommandPattern, "").Split("x");
                if (temp.Length == 2)
                {
                    if (int.TryParse(temp[0], out var width) && int.TryParse(temp[1], out var heingh))
                    {
                        resoulution = new PixelSize(width, heingh);
                    }
                }
            }

            var drmOpt = new DrmOutputOptions()
            {
                Scaling = 1,
                EnableInitialBufferSwapping = true,
            };

            if (resoulution != null) drmOpt.VideoMode = resoulution;

            var libInputOpt = new LibInputBackendOptions() { };

            return BuildAvaloniaApp().StartLinuxDrm(args, card, true, drmOpt, new LibInputBackend(libInputOpt));
        }
        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}

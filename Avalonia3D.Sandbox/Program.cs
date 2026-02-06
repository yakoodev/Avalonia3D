using Avalonia;
using Avalonia.LinuxFramebuffer;
using Avalonia.LinuxFramebuffer.Input.LibInput;
using Serilog;
using System;
using System.Linq;

namespace Avalonia3D.Sandbox;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("Starting Avalonia3D.Sandbox with args: {Args}", string.Join(" ", args));

        if (OperatingSystem.IsLinux())
        {
            return StartDrm(args);
        }

        return StartDefault(args);
    }

    private static int StartDefault(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    private static int StartDrm(string[] args)
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

        var drmOpt = new DrmOutputOptions
        {
            Scaling = 1,
            EnableInitialBufferSwapping = true,
        };

        if (resoulution != null)
        {
            drmOpt.VideoMode = resoulution;
        }

        var libInputOpt = new LibInputBackendOptions();

        return BuildAvaloniaApp().StartLinuxDrm(args, card, true, drmOpt, new LibInputBackend(libInputOpt));
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

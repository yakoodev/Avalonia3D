using Serilog;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering.Diagnostics;

public static class PbrFrameDiagnostics
{
    private static readonly bool Enabled =
        string.Equals(Environment.GetEnvironmentVariable("AVALONIA3D_PBR_FRAME_DIAGNOSTICS"), "1", StringComparison.OrdinalIgnoreCase);

    private static DateTime _lastLoggedAtUtc = DateTime.MinValue;
    private static bool _loggedGlInfo;

    public static bool IsEnabled => Enabled;

    public static unsafe void LogFrameIfEnabled(GL gl, RenderFrameState frameState, int width, int height, GraphicsProfile profile)
    {
        if (!Enabled || gl == null || frameState == null || width <= 0 || height <= 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastLoggedAtUtc < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastLoggedAtUtc = now;

        if (!_loggedGlInfo)
        {
            _loggedGlInfo = true;
            var vendor = gl.GetStringS(GLEnum.Vendor);
            var renderer = gl.GetStringS(GLEnum.Renderer);
            var version = gl.GetStringS(GLEnum.Version);
            Log.Information("PBR frame diagnostics GL: vendor={Vendor}, renderer={Renderer}, version={Version}", vendor, renderer, version);
        }

        var sampleWidth = Math.Min(width, 32);
        var sampleHeight = Math.Min(height, 32);
        var pixelCount = sampleWidth * sampleHeight;
        var bytes = new byte[pixelCount * 4];

        fixed (byte* ptr = bytes)
        {
            gl.ReadPixels(0, 0, (uint)sampleWidth, (uint)sampleHeight, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        var minLum = 1f;
        var maxLum = 0f;
        var sumLum = 0f;
        var clipped = 0;

        for (var i = 0; i < pixelCount; i++)
        {
            var idx = i * 4;
            var r = bytes[idx] / 255f;
            var g = bytes[idx + 1] / 255f;
            var b = bytes[idx + 2] / 255f;

            var luminance = (0.2126f * r) + (0.7152f * g) + (0.0722f * b);
            minLum = Math.Min(minLum, luminance);
            maxLum = Math.Max(maxLum, luminance);
            sumLum += luminance;

            if (r >= 0.99f && g >= 0.99f && b >= 0.99f)
            {
                clipped++;
            }
        }

        var meanLum = pixelCount > 0 ? sumLum / pixelCount : 0f;
        var clippedRatio = pixelCount > 0 ? (float)clipped / pixelCount : 0f;

        var framebufferSrgbEnabled = gl.IsEnabled(EnableCap.FramebufferSrgb);

        Log.Information(
            "PBR frame diagnostics: debugMode={DebugMode}, exposure={Exposure:0.###}, toneMapping={ToneMapping}, gamma={Gamma:0.###}, framebufferSrgb={FramebufferSrgb}, luminance(min/mean/max)=({Min:0.###}/{Mean:0.###}/{Max:0.###}), whiteClipRatio={WhiteClipRatio:0.###}, sample={SampleWidth}x{SampleHeight}",
            frameState.PbrDebugViewMode,
            profile.PbrTuning.Exposure,
            profile.PostFx.ToneMapping,
            profile.PostFx.Gamma,
            framebufferSrgbEnabled,
            minLum,
            meanLum,
            maxLum,
            clippedRatio,
            sampleWidth,
            sampleHeight);
    }
}

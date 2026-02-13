using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace Avalonia3D.Loaders;

public enum TextureDecodeMode
{
    Fast,
    Balanced,
    Quality
}

public interface ITextureDecodePolicy
{
    TextureDecodeMode Mode { get; }

    IResampler GetResamplerFor(TextureDecodeMode mode);
}

public sealed class TextureDecodePolicy : ITextureDecodePolicy
{
    public TextureDecodePolicy(TextureDecodeMode mode)
    {
        Mode = mode;
    }

    public TextureDecodeMode Mode { get; }

    public IResampler GetResamplerFor(TextureDecodeMode mode)
    {
        return mode switch
        {
            TextureDecodeMode.Fast => KnownResamplers.Triangle,
            TextureDecodeMode.Balanced => KnownResamplers.Bicubic,
            TextureDecodeMode.Quality => KnownResamplers.Lanczos3,
            _ => KnownResamplers.Bicubic
        };
    }
}

public static class TextureDecodePolicies
{
    public static ITextureDecodePolicy Fast { get; } = new TextureDecodePolicy(TextureDecodeMode.Fast);
    public static ITextureDecodePolicy Balanced { get; } = new TextureDecodePolicy(TextureDecodeMode.Balanced);
    public static ITextureDecodePolicy Quality { get; } = new TextureDecodePolicy(TextureDecodeMode.Quality);
}

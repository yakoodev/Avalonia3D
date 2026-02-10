using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

public class MorphSignalNormalizerTests
{
    [Fact]
    public void Normalize_WithAdaptiveRange_MapsObservedMinMaxToZeroOne()
    {
        var n = new MorphSignalNormalizer();

        var first = n.Normalize(0.2f);
        var min = n.Normalize(0.1f);
        var max = n.Normalize(0.6f);
        var mid = n.Normalize(0.35f);

        Assert.InRange(first, 0.19f, 0.21f);
        Assert.InRange(min, 0f, 0.001f);
        Assert.InRange(max, 0.999f, 1f);
        Assert.InRange(mid, 0.49f, 0.51f);
    }

    [Fact]
    public void Normalize_Reset_DropsObservedRange()
    {
        var n = new MorphSignalNormalizer();
        n.Normalize(0.1f);
        n.Normalize(0.5f);

        n.Reset();
        var value = n.Normalize(0.4f);

        Assert.InRange(value, 0.39f, 0.41f);
    }
}

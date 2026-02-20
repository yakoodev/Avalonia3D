using Avalonia3D.Sandbox.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class FramebufferTargetResolverTests
{
    [Fact]
    public void Resolve_WhenIncomingIsValid_UsesIncomingAndUpdatesLastValid()
    {
        var output = FramebufferTargetResolver.Resolve(7, 0, out var updated);

        Assert.Equal((uint)7, output);
        Assert.Equal(7, updated);
    }

    [Fact]
    public void Resolve_WhenIncomingIsNegativeAndLastValidExists_ReusesLastValid()
    {
        var output = FramebufferTargetResolver.Resolve(-1, 5, out var updated);

        Assert.Equal((uint)5, output);
        Assert.Equal(5, updated);
    }

    [Fact]
    public void Resolve_WhenIncomingIsNegativeAndNoLastValid_FallsBackToDefault()
    {
        var output = FramebufferTargetResolver.Resolve(-1, null, out var updated);

        Assert.Equal((uint)0, output);
        Assert.Equal(0, updated);
    }
}

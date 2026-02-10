using Avalonia3D.Rendering;
using Xunit;

namespace Avalonia3D.Tests;

public class RenderFrameStateTests
{
    [Fact]
    public void HasEmissiveTarget_ReturnsTrue_OnlyWhenFramebufferAndTexturePresent()
    {
        var state = new RenderFrameState();

        Assert.False(state.HasEmissiveTarget);

        state.EmissiveFramebufferId = 2;
        Assert.False(state.HasEmissiveTarget);

        state.EmissiveTextureId = 3;
        Assert.True(state.HasEmissiveTarget);
    }

    [Fact]
    public void ResetForwardTargets_ClearsAllForwardAndEmissiveHandles()
    {
        var state = new RenderFrameState
        {
            ForwardFramebufferId = 7,
            ForwardColorTextureId = 8,
            EmissiveFramebufferId = 9,
            EmissiveTextureId = 10
        };

        state.ResetForwardTargets();

        Assert.Equal(0u, state.ForwardFramebufferId);
        Assert.Equal(0u, state.ForwardColorTextureId);
        Assert.Equal(0u, state.EmissiveFramebufferId);
        Assert.Equal(0u, state.EmissiveTextureId);
        Assert.False(state.HasEmissiveTarget);
    }
}

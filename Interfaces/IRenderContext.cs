using Avalonia3D.Model;
using Silk.NET.OpenGL;

namespace Avalonia3D.Interfaces
{
    public interface IRenderContext
    {
        GL? GL { get; }
        Scene3D Scene { get; }
        RenderFrameState FrameState { get; }
    }
}

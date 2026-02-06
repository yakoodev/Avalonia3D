using Avalonia3D.Model;
using Avalonia3D.Rendering;

namespace Avalonia3D.Composition;

public interface ISceneBootstrap
{
    void Bootstrap(Scene3D scene, GraphicsProfile? profile = null);
}

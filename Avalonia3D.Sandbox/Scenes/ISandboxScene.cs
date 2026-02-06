using Avalonia3D.Model;

namespace Avalonia3D.Sandbox.Scenes;

public interface ISandboxScene
{
    string Id { get; }
    string Title { get; }
    string Description { get; }
    void Load(Scene3D scene, string assetsRoot);
}

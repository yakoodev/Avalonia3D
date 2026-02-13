using Avalonia3D.Model;

namespace Avalonia3D.Sandbox.Scenes;

public interface ISceneBackgroundPreparation
{
    object Prepare(string assetsRoot);

    void LoadPrepared(Scene3D scene, string assetsRoot, object preparedPayload);
}

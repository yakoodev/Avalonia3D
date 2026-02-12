using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;

namespace Avalonia3D.Sandbox.Services;

public interface ISceneCameraPolicy
{
    void ApplyDefaults(Scene3D scene3D, ISandboxScene sceneInfo);

    void ApplyPostLoad(Scene3D scene3D, ISandboxScene sceneInfo, SceneLoadOptions loadOptions);
}

using Avalonia3D.Sandbox.Scenes;
using System;

namespace Avalonia3D.Sandbox.Services;

public interface ISceneLoadService
{
    event Action<ISandboxScene>? SceneChanged;

    void MarkRendererReady();

    void Load(ISandboxScene scene);
}

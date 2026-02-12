using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;
using System;

namespace Avalonia3D.Sandbox.Services;

public sealed class SceneLoader : ISceneLoadService
{
    private readonly ISceneLoadService _sceneLoadService;

    public SceneLoader(ISceneLoadService sceneLoadService)
    {
        _sceneLoadService = sceneLoadService ?? throw new ArgumentNullException(nameof(sceneLoadService));
    }

    public SceneLoader(Scene3D scene, string assetsRoot, IRenderThreadScheduler renderThreadScheduler)
        : this(new RenderThreadSceneLoadOrchestrator(
            new SceneLoadService(
                scene,
                assetsRoot,
                new DefaultSceneCameraPolicy(),
                new DefaultSceneDiagnosticsReporter(),
                new InMemorySceneAssetCache()),
            renderThreadScheduler))
    {
    }

    public event Action<ISandboxScene>? SceneChanged
    {
        add => _sceneLoadService.SceneChanged += value;
        remove => _sceneLoadService.SceneChanged -= value;
    }

    public void MarkRendererReady() => _sceneLoadService.MarkRendererReady();

    public void Load(ISandboxScene scene) => _sceneLoadService.Load(scene);
}

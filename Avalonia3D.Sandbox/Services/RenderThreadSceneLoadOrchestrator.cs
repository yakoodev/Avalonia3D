using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;

namespace Avalonia3D.Sandbox.Services;

public sealed class RenderThreadSceneLoadOrchestrator : ISceneLoadService
{
    private readonly SceneLoadService _inner;
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private bool _isRendererReady;
    private ISandboxScene? _pendingScene;

    public RenderThreadSceneLoadOrchestrator(SceneLoadService inner, IRenderThreadScheduler renderThreadScheduler)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _renderThreadScheduler = renderThreadScheduler ?? throw new ArgumentNullException(nameof(renderThreadScheduler));
        _inner.SceneChanged += scene => SceneChanged?.Invoke(scene);
    }

    public event Action<ISandboxScene>? SceneChanged;

    public void MarkRendererReady()
    {
        _isRendererReady = true;
        if (_pendingScene == null)
        {
            return;
        }

        QueueLoad(_pendingScene);
        _pendingScene = null;
    }

    public void Load(ISandboxScene scene)
    {
        if (scene == null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        Log.Information("Scene load requested: {SceneId} - {SceneTitle}", scene.Id, scene.Title);

        if (!_isRendererReady)
        {
            _pendingScene = scene;
            return;
        }

        QueueLoad(scene);
    }

    private void QueueLoad(ISandboxScene scene)
    {
        _renderThreadScheduler.Enqueue(() => _inner.LoadNow(scene));
    }
}

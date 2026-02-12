using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;
using System.Threading;

namespace Avalonia3D.Sandbox.Services;

public sealed class RenderThreadSceneLoadOrchestrator : ISceneLoadService
{
    private readonly SceneLoadService _inner;
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private bool _isRendererReady;
    private ISandboxScene? _pendingScene;
    private int _lastRequestedVersion;

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

        QueueLoad(_pendingScene, Volatile.Read(ref _lastRequestedVersion));
        _pendingScene = null;
    }

    public void Load(ISandboxScene scene)
    {
        if (scene == null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        var version = Interlocked.Increment(ref _lastRequestedVersion);
        Log.Information("Scene load requested: {SceneId} - {SceneTitle}. Version={Version}", scene.Id, scene.Title, version);

        if (!_isRendererReady)
        {
            _pendingScene = scene;
            return;
        }

        QueueLoad(scene, version);
    }

    private void QueueLoad(ISandboxScene scene, int requestVersion)
    {
        _renderThreadScheduler.Enqueue(() =>
        {
            if (requestVersion != Volatile.Read(ref _lastRequestedVersion))
            {
                Log.Information("Scene load canceled (superseded). Scene={SceneId}, Version={Version}", scene.Id, requestVersion);
                return;
            }

            _inner.LoadNow(scene);
        });
    }
}

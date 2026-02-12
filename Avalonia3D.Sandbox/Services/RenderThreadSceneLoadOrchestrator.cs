using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia3D.Sandbox.Services;

public sealed class RenderThreadSceneLoadOrchestrator : ISceneLoadService
{
    private readonly SceneLoadService _inner;
    private readonly object _sync = new();

    private bool _isRendererReady;
    private ISandboxScene? _pendingScene;
    private bool _isPumpRunning;
    private int _lastRequestedVersion;

    public RenderThreadSceneLoadOrchestrator(SceneLoadService inner, IRenderThreadScheduler renderThreadScheduler)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _inner.SceneChanged += scene => SceneChanged?.Invoke(scene);
    }

    public event Action<ISandboxScene>? SceneChanged;

    public void MarkRendererReady()
    {
        lock (_sync)
        {
            _isRendererReady = true;
        }

        EnsurePump();
    }

    public void Load(ISandboxScene scene)
    {
        if (scene == null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        var version = Interlocked.Increment(ref _lastRequestedVersion);
        Log.Information("Scene load requested: {SceneId} - {SceneTitle}. Version={Version}", scene.Id, scene.Title, version);

        lock (_sync)
        {
            _pendingScene = scene;
        }

        EnsurePump();
    }

    private void EnsurePump()
    {
        lock (_sync)
        {
            if (!_isRendererReady || _isPumpRunning)
            {
                return;
            }

            _isPumpRunning = true;
        }

        _ = Task.Run(PumpAsync);
    }

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                ISandboxScene? scene;
                var versionSnapshot = Volatile.Read(ref _lastRequestedVersion);

                lock (_sync)
                {
                    scene = _pendingScene;
                    _pendingScene = null;
                }

                if (scene == null)
                {
                    break;
                }

                if (versionSnapshot != Volatile.Read(ref _lastRequestedVersion))
                {
                    Log.Information("Scene load skipped (superseded before start). Scene={SceneId}, Version={Version}", scene.Id, versionSnapshot);
                    continue;
                }

                try
                {
                    await Task.Run(() => _inner.LoadNow(scene)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Scene load failed: {SceneId}", scene.Id);
                }
            }
        }
        finally
        {
            lock (_sync)
            {
                _isPumpRunning = false;
            }

            lock (_sync)
            {
                if (_pendingScene != null && _isRendererReady)
                {
                    EnsurePump();
                }
            }
        }
    }
}

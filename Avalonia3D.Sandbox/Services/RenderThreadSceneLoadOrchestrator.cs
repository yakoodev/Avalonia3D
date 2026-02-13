using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia3D.Sandbox.Services;

public sealed class RenderThreadSceneLoadOrchestrator : ISceneLoadService
{
    private readonly SceneLoadService _inner;
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private readonly object _sync = new();

    private bool _isRendererReady;
    private ISandboxScene? _pendingScene;
    private bool _isPumpRunning;
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

                lock (_sync)
                {
                    scene = _pendingScene;
                    _pendingScene = null;
                }

                if (scene == null)
                {
                    break;
                }

                var requestedVersion = Volatile.Read(ref _lastRequestedVersion);
                object? preparedPayload = null;

                var unloaded = new ManualResetEventSlim(false);
                _renderThreadScheduler.Enqueue(() =>
                {
                    try
                    {
                        if (requestedVersion != Volatile.Read(ref _lastRequestedVersion))
                        {
                            return;
                        }

                        _inner.UnloadCurrentSceneForTransition();
                    }
                    finally
                    {
                        unloaded.Set();
                    }
                });

                await Task.Run(() => unloaded.Wait()).ConfigureAwait(false);

                if (scene is ISceneBackgroundPreparation preparable)
                {
                    try
                    {
                        preparedPayload = await Task.Run(() => preparable.Prepare(_inner.AssetsRoot)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Scene background prepare failed: {SceneId}", scene.Id);
                        preparedPayload = null;
                    }
                }

                if (requestedVersion != Volatile.Read(ref _lastRequestedVersion))
                {
                    Log.Information("Scene load canceled (superseded). Scene={SceneId}, Version={Version}", scene.Id, requestedVersion);
                    continue;
                }

                var applied = new ManualResetEventSlim(false);
                _renderThreadScheduler.Enqueue(() =>
                {
                    try
                    {
                        if (requestedVersion != Volatile.Read(ref _lastRequestedVersion))
                        {
                            Log.Information("Scene apply canceled (superseded). Scene={SceneId}, Version={Version}", scene.Id, requestedVersion);
                            return;
                        }

                        if (preparedPayload != null && scene is ISceneBackgroundPreparation preparedScene)
                        {
                            _inner.LoadNow(scene, preparedPayload, preparedScene);
                            return;
                        }

                        _inner.LoadNow(scene);
                    }
                    finally
                    {
                        applied.Set();
                    }
                });

                await Task.Run(() => applied.Wait()).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_sync)
            {
                _isPumpRunning = false;
            }

            EnsurePump();
        }
    }
}

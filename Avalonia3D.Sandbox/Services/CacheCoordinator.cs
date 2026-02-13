using System;

namespace Avalonia3D.Sandbox.Services;

/// <summary>
/// Single entry point for cache invalidation across cache levels.
/// Keep future cache orchestration changes centralized here.
/// </summary>
public sealed class CacheCoordinator
{
    private static readonly object Sync = new();
    private static CacheCoordinator _current = CreateDefault();

    public CacheCoordinator(ISceneAssetCache sceneAssetCache, ISceneImportResultCache sceneImportResultCache)
    {
        SceneAssetCache = sceneAssetCache ?? throw new ArgumentNullException(nameof(sceneAssetCache));
        SceneImportResultCache = sceneImportResultCache ?? throw new ArgumentNullException(nameof(sceneImportResultCache));
    }

    public ISceneAssetCache SceneAssetCache { get; }

    public ISceneImportResultCache SceneImportResultCache { get; }

    public static CacheCoordinator Current
    {
        get
        {
            lock (Sync)
            {
                return _current;
            }
        }
    }

    public static void Configure(CacheCoordinator coordinator)
    {
        if (coordinator == null)
        {
            throw new ArgumentNullException(nameof(coordinator));
        }

        lock (Sync)
        {
            _current = coordinator;
        }
    }

    public void Invalidate(string key)
    {
        SceneAssetCache.Invalidate(key);
        SceneImportResultCache.Invalidate(key);
    }

    public void InvalidateAll()
    {
        SceneAssetCache.InvalidateAll();
        SceneImportResultCache.InvalidateAll();
    }

    private static CacheCoordinator CreateDefault()
    {
        return new CacheCoordinator(new InMemorySceneAssetCache(), new NullSceneImportResultCache());
    }
}

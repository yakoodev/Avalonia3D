using System;
using System.Collections.Generic;

namespace Avalonia3D.Sandbox.Services;

public interface ISceneAssetCache
{
    bool TryGet(string key, out SceneAssetCacheEntry entry);

    void Set(string key, SceneAssetCacheEntry entry, TimeSpan? ttl = null);

    void Prewarm(IEnumerable<KeyValuePair<string, SceneAssetCacheEntry>> entries, TimeSpan? ttl = null);

    void Invalidate(string key);

    void InvalidateAll();
}

public sealed record SceneAssetCacheEntry(string SceneId, string SourceTag, DateTime CachedAtUtc);

using System;
using System.Collections.Generic;

namespace Avalonia3D.Sandbox.Services;

public sealed class InMemorySceneAssetCache : ISceneAssetCache
{
    private readonly Dictionary<string, CacheRecord> _entries = new(StringComparer.Ordinal);

    public bool TryGet(string key, out SceneAssetCacheEntry entry)
    {
        if (_entries.TryGetValue(key, out var record))
        {
            if (record.ExpiresAtUtc is null || DateTime.UtcNow <= record.ExpiresAtUtc.Value)
            {
                entry = record.Entry;
                return true;
            }

            _entries.Remove(key);
        }

        entry = default!;
        return false;
    }

    public void Set(string key, SceneAssetCacheEntry entry, TimeSpan? ttl = null)
    {
        _entries[key] = new CacheRecord(entry, ToExpiresAt(ttl));
    }

    public void Prewarm(IEnumerable<KeyValuePair<string, SceneAssetCacheEntry>> entries, TimeSpan? ttl = null)
    {
        foreach (var pair in entries)
        {
            Set(pair.Key, pair.Value, ttl);
        }
    }

    public void Invalidate(string key)
    {
        _entries.Remove(key);
    }

    private static DateTime? ToExpiresAt(TimeSpan? ttl)
    {
        if (ttl is null)
        {
            return null;
        }

        return DateTime.UtcNow + ttl.Value;
    }

    private sealed record CacheRecord(SceneAssetCacheEntry Entry, DateTime? ExpiresAtUtc);
}

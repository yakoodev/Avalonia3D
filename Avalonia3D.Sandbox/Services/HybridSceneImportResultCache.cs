using Avalonia3D.Loaders;
using Serilog;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Avalonia3D.Sandbox.Services;

public sealed class HybridSceneImportResultCache : ISceneImportResultCache
{
    private readonly long _memoryBudgetBytes;
    private readonly string _diskCachePath;
    private readonly Dictionary<string, MemoryEntry> _memoryEntries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private readonly object _sync = new();
    private long _memoryUsedBytes;

    public HybridSceneImportResultCache(long memoryBudgetBytes = 16 * 1024 * 1024, string? diskCachePath = null)
    {
        _memoryBudgetBytes = Math.Max(memoryBudgetBytes, 4 * 1024 * 1024);
        _diskCachePath = diskCachePath ?? Path.Combine(Path.GetTempPath(), "Avalonia3D", "scene-import-cache");
        Directory.CreateDirectory(_diskCachePath);
    }

    public bool TryGet(string key, out SceneImportResult importResult)
    {
        lock (_sync)
        {
            if (_memoryEntries.TryGetValue(key, out var inMemory))
            {
                Touch(inMemory);
                if (inMemory.ImportResult.TryGetTarget(out importResult!))
                {
                    return true;
                }
            }
        }

        var payload = ReadDiskPayload(key);
        if (payload == null)
        {
            importResult = default!;
            return false;
        }

        try
        {
            importResult = BuildImportResult(payload);
            UpsertMemoryEntry(key, importResult, payload.Length);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to restore cached scene import result. Key={CacheKey}", key);
            importResult = default!;
            return false;
        }
    }

    public void Set(string key, SceneImportResult importResult, ReadOnlyMemory<byte> intermediatePayload)
    {
        WriteDiskPayload(key, intermediatePayload.Span);
        UpsertMemoryEntry(key, importResult, intermediatePayload.Length);
    }

    public void Invalidate(string key)
    {
        lock (_sync)
        {
            if (_memoryEntries.Remove(key, out var removed))
            {
                _lru.Remove(removed.LruNode);
                _memoryUsedBytes -= removed.ApproximateSize;
            }
        }

        var diskPath = GetDiskPath(key);
        if (File.Exists(diskPath))
        {
            File.Delete(diskPath);
        }
    }

    public void InvalidateAll()
    {
        lock (_sync)
        {
            _memoryEntries.Clear();
            _lru.Clear();
            _memoryUsedBytes = 0;
        }

        if (!Directory.Exists(_diskCachePath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_diskCachePath, "*.glb", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
        }
    }

    private void UpsertMemoryEntry(string key, SceneImportResult importResult, int approximateSize)
    {
        if (approximateSize <= 0 || approximateSize > _memoryBudgetBytes)
        {
            lock (_sync)
            {
                if (_memoryEntries.Remove(key, out var removed))
                {
                    _lru.Remove(removed.LruNode);
                    _memoryUsedBytes -= removed.ApproximateSize;
                }
            }

            return;
        }

        lock (_sync)
        {
            if (_memoryEntries.Remove(key, out var previous))
            {
                _lru.Remove(previous.LruNode);
                _memoryUsedBytes -= previous.ApproximateSize;
            }

            var node = new LinkedListNode<string>(key);
            _lru.AddFirst(node);
            _memoryEntries[key] = new MemoryEntry(new WeakReference<SceneImportResult>(importResult), node, approximateSize);
            _memoryUsedBytes += approximateSize;
            TrimToBudget();
        }
    }

    private void TrimToBudget()
    {
        while (_memoryUsedBytes > _memoryBudgetBytes && _lru.Last != null)
        {
            var key = _lru.Last.Value;
            _lru.RemoveLast();

            if (!_memoryEntries.Remove(key, out var removed))
            {
                continue;
            }

            _memoryUsedBytes -= removed.ApproximateSize;
        }
    }

    private static SceneImportResult BuildImportResult(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        var model = ModelRoot.ReadGLB(stream);
        var importer = new GltfSceneImporter
        {
            ValidationPolicy = ImportValidationConfiguration.CurrentPolicy
        };

        return importer.ImportWithAnimations(model);
    }

    private byte[]? ReadDiskPayload(string key)
    {
        var diskPath = GetDiskPath(key);
        if (!File.Exists(diskPath))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(diskPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read scene import payload from disk cache. Key={CacheKey}", key);
            return null;
        }
    }

    private void WriteDiskPayload(string key, ReadOnlySpan<byte> payload)
    {
        var diskPath = GetDiskPath(key);

        try
        {
            using var stream = new FileStream(diskPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(payload);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write scene import payload to disk cache. Key={CacheKey}", key);
        }
    }

    private void Touch(MemoryEntry entry)
    {
        _lru.Remove(entry.LruNode);
        _lru.AddFirst(entry.LruNode);
    }

    private string GetDiskPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(_diskCachePath, $"{hash}.glb");
    }

    private sealed record MemoryEntry(WeakReference<SceneImportResult> ImportResult, LinkedListNode<string> LruNode, int ApproximateSize);
}

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

    public HybridSceneImportResultCache(long memoryBudgetBytes = 128 * 1024 * 1024, string? diskCachePath = null)
    {
        _memoryBudgetBytes = Math.Max(memoryBudgetBytes, 8 * 1024 * 1024);
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
                importResult = inMemory.ImportResult;
                return true;
            }
        }

        var diskPath = GetDiskPath(key);
        if (!File.Exists(diskPath))
        {
            importResult = default!;
            return false;
        }

        try
        {
            var payload = File.ReadAllBytes(diskPath);
            var model = ModelRoot.ReadGLB(new MemoryStream(payload));
            var importer = new GltfSceneImporter
            {
                ValidationPolicy = ImportValidationConfiguration.CurrentPolicy
            };

            importResult = importer.ImportWithAnimations(model);
            SetInternal(key, importResult, payload);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read scene import result cache from disk. Key={CacheKey}", key);
            importResult = default!;
            return false;
        }
    }

    public void Set(string key, SceneImportResult importResult, ReadOnlyMemory<byte> intermediatePayload)
    {
        SetInternal(key, importResult, intermediatePayload.ToArray());
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

    private void SetInternal(string key, SceneImportResult importResult, byte[] payload)
    {
        var size = Math.Max(payload.Length, 1);
        lock (_sync)
        {
            if (_memoryEntries.Remove(key, out var previous))
            {
                _lru.Remove(previous.LruNode);
                _memoryUsedBytes -= previous.ApproximateSize;
            }

            var node = new LinkedListNode<string>(key);
            _lru.AddFirst(node);
            _memoryEntries[key] = new MemoryEntry(importResult, node, size);
            _memoryUsedBytes += size;
            TrimToBudget();
        }

        var diskPath = GetDiskPath(key);
        File.WriteAllBytes(diskPath, payload);
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

    private sealed record MemoryEntry(SceneImportResult ImportResult, LinkedListNode<string> LruNode, int ApproximateSize);
}

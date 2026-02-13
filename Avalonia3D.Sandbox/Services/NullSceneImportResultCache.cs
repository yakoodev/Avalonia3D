using System;
using Avalonia3D.Loaders;

namespace Avalonia3D.Sandbox.Services;

public sealed class NullSceneImportResultCache : ISceneImportResultCache
{
    public bool TryGet(string key, out SceneImportResult importResult)
    {
        importResult = default!;
        return false;
    }

    public void Set(string key, SceneImportResult importResult, ReadOnlyMemory<byte> intermediatePayload)
    {
    }

    public void Invalidate(string key)
    {
    }

    public void InvalidateAll()
    {
    }
}

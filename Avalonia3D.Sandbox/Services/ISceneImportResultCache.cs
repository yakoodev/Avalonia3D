using Avalonia3D.Loaders;
using System;

namespace Avalonia3D.Sandbox.Services;

public interface ISceneImportResultCache
{
    bool TryGet(string key, out SceneImportResult importResult);

    void Set(string key, SceneImportResult importResult, ReadOnlyMemory<byte> intermediatePayload);

    void Invalidate(string key);

    void InvalidateAll();
}

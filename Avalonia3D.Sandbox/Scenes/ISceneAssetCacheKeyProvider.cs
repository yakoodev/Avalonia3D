using System;

namespace Avalonia3D.Sandbox.Scenes;

public interface ISceneAssetCacheKeyProvider
{
    string BuildCacheKey(string assetsRoot);

    TimeSpan? CacheTtl => null;
}

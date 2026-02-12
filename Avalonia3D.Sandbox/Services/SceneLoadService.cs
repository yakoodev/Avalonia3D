using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;

namespace Avalonia3D.Sandbox.Services;

public sealed class SceneLoadService
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(10);

    private readonly Scene3D _scene;
    private readonly string _assetsRoot;
    private readonly ISceneCameraPolicy _cameraPolicy;
    private readonly ISceneDiagnosticsReporter _diagnosticsReporter;
    private readonly ISceneAssetCache _sceneAssetCache;

    public SceneLoadService(
        Scene3D scene,
        string assetsRoot,
        ISceneCameraPolicy cameraPolicy,
        ISceneDiagnosticsReporter diagnosticsReporter,
        ISceneAssetCache sceneAssetCache)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _assetsRoot = assetsRoot ?? throw new ArgumentNullException(nameof(assetsRoot));
        _cameraPolicy = cameraPolicy ?? throw new ArgumentNullException(nameof(cameraPolicy));
        _diagnosticsReporter = diagnosticsReporter ?? throw new ArgumentNullException(nameof(diagnosticsReporter));
        _sceneAssetCache = sceneAssetCache ?? throw new ArgumentNullException(nameof(sceneAssetCache));
    }

    public event Action<ISandboxScene>? SceneChanged;

    public void LoadNow(ISandboxScene sceneInfo)
    {
        _cameraPolicy.ApplyDefaults(_scene, sceneInfo);

        RegisterCacheAccess(sceneInfo);

        sceneInfo.Load(_scene, _assetsRoot);

        var loadOptions = sceneInfo is ISceneLoadOptionsProvider provider
            ? provider.LoadOptions
            : SceneLoadOptions.Default;

        _cameraPolicy.ApplyPostLoad(_scene, sceneInfo, loadOptions);
        _diagnosticsReporter.Report(_scene, sceneInfo);

        if (_scene.LastImportReport.IsDegraded)
        {
            Log.Warning("Scene {SceneId} loaded in degraded import mode. Issues: {Issues}", sceneInfo.Id, string.Join(" | ", _scene.LastImportReport.Issues));
        }

        Log.Information("Scene loaded: {SceneId} - {SceneTitle}", sceneInfo.Id, sceneInfo.Title);
        SceneChanged?.Invoke(sceneInfo);
    }

    private void RegisterCacheAccess(ISandboxScene sceneInfo)
    {
        if (sceneInfo is not ISceneAssetCacheKeyProvider cacheKeyProvider)
        {
            return;
        }

        var key = cacheKeyProvider.BuildCacheKey(_assetsRoot);
        if (_sceneAssetCache.TryGet(key, out _))
        {
            Log.Information("Scene asset cache hit: {SceneId}, Key={CacheKey}", sceneInfo.Id, key);
            return;
        }

        Log.Information("Scene asset cache miss: {SceneId}, Key={CacheKey}", sceneInfo.Id, key);
        _sceneAssetCache.Set(
            key,
            new SceneAssetCacheEntry(sceneInfo.Id, sceneInfo.GetType().Name, DateTime.UtcNow),
            cacheKeyProvider.CacheTtl ?? DefaultCacheTtl);
    }
}

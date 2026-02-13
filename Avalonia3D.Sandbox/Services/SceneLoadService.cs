using Avalonia3D.Memory;
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
    private readonly CacheCoordinator _cacheCoordinator;

    public SceneLoadService(
        Scene3D scene,
        string assetsRoot,
        ISceneCameraPolicy cameraPolicy,
        ISceneDiagnosticsReporter diagnosticsReporter,
        CacheCoordinator cacheCoordinator)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _assetsRoot = assetsRoot ?? throw new ArgumentNullException(nameof(assetsRoot));
        _cameraPolicy = cameraPolicy ?? throw new ArgumentNullException(nameof(cameraPolicy));
        _diagnosticsReporter = diagnosticsReporter ?? throw new ArgumentNullException(nameof(diagnosticsReporter));
        _cacheCoordinator = cacheCoordinator ?? throw new ArgumentNullException(nameof(cacheCoordinator));
    }

    public event Action<ISandboxScene>? SceneChanged;

    public string AssetsRoot => _assetsRoot;

    public void UnloadCurrentSceneForTransition()
    {
        _scene.ClearCaches(CacheScope.SceneOnly);
        MemoryManager.PerformSoftCleanup("scene-switch", allowGen2: false);
    }

    public void LoadNow(ISandboxScene sceneInfo)
    {
        LoadNow(sceneInfo, null, null);
    }

    public void LoadNow(ISandboxScene sceneInfo, object? preparedPayload, ISceneBackgroundPreparation? preparedScene)
    {
        _cameraPolicy.ApplyDefaults(_scene, sceneInfo);

        RegisterCacheAccess(sceneInfo);

        if (preparedPayload != null && preparedScene != null)
        {
            preparedScene.LoadPrepared(_scene, _assetsRoot, preparedPayload);
        }
        else
        {
            sceneInfo.Load(_scene, _assetsRoot);
        }

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
        if (_cacheCoordinator.SceneAssetCache.TryGet(key, out _))
        {
            Log.Information("Scene asset cache hit: {SceneId}, Key={CacheKey}", sceneInfo.Id, key);
            return;
        }

        Log.Information("Scene asset cache miss: {SceneId}, Key={CacheKey}", sceneInfo.Id, key);
        _cacheCoordinator.SceneAssetCache.Set(
            key,
            new SceneAssetCacheEntry(sceneInfo.Id, sceneInfo.GetType().Name, DateTime.UtcNow),
            cacheKeyProvider.CacheTtl ?? DefaultCacheTtl);
    }
}

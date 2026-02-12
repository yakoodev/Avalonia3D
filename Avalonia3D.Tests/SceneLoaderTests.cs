using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;
using Avalonia3D.Sandbox.Services;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class SceneLoaderTests
{
    [Fact]
    public void Load_WhenRendererIsNotReady_DefersAndLoadsOnlyLatestPendingScene()
    {
        var coreLoader = CreateCoreLoader();
        var scheduler = new ImmediateScheduler();
        var orchestrator = new RenderThreadSceneLoadOrchestrator(coreLoader, scheduler);

        var first = new RecordingSandboxScene("first");
        var second = new RecordingSandboxScene("second");

        orchestrator.Load(first);
        orchestrator.Load(second);

        Assert.Equal(0, first.LoadCallCount);
        Assert.Equal(0, second.LoadCallCount);

        orchestrator.MarkRendererReady();

        Assert.Equal(0, first.LoadCallCount);
        Assert.Equal(1, second.LoadCallCount);
        Assert.Equal(1, scheduler.EnqueueCalls);
    }

    [Fact]
    public void Load_WhenRendererReady_ExecutesOnSchedulerAndRaisesSceneChanged()
    {
        var coreLoader = CreateCoreLoader();
        var scheduler = new ImmediateScheduler();
        var orchestrator = new RenderThreadSceneLoadOrchestrator(coreLoader, scheduler);
        var sample = new RecordingSandboxScene("vehicle");
        ISandboxScene? changedTo = null;
        orchestrator.SceneChanged += loaded => changedTo = loaded;

        orchestrator.MarkRendererReady();
        orchestrator.Load(sample);

        Assert.Equal(1, scheduler.EnqueueCalls);
        Assert.Equal(1, sample.LoadCallCount);
        Assert.Same(sample, changedTo);
    }

    [Fact]
    public void SceneLoadService_RepeatedLoads_WithAssetCacheHitAndMiss()
    {
        var trackingCache = new TrackingSceneAssetCache();
        var scene = new Scene3D();
        var loader = new SceneLoadService(scene, "/tmp/assets", new NoopCameraPolicy(), new NoopDiagnosticsReporter(), trackingCache);
        var cacheable = new CacheableScene("cache-scene", "stable-key-1");

        loader.LoadNow(cacheable);
        loader.LoadNow(cacheable);

        Assert.Equal(2, cacheable.LoadCallCount);
        Assert.Equal(2, trackingCache.TryGetCalls);
        Assert.Equal(1, trackingCache.SetCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SceneLoadService_CallsCameraPolicy_WithExpectedAutoFrameFlag(bool autoFrame)
    {
        var policy = new TrackingCameraPolicy();
        var loader = new SceneLoadService(new Scene3D(), "/tmp/assets", policy, new NoopDiagnosticsReporter(), new InMemorySceneAssetCache());
        var scene = new SceneWithOptions("opts", autoFrame);

        loader.LoadNow(scene);

        Assert.True(policy.ApplyDefaultsCalled);
        Assert.Equal(autoFrame, policy.LastAutoFrameOption);
    }

    [Fact]
    public void GltfFileScene_BuildCacheKey_ChangesWhenFileTimestampChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gltf-key-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var relativePath = "model.gltf";
        var fullPath = Path.Combine(root, relativePath);
        File.WriteAllText(fullPath, "{}");

        try
        {
            var scene = new GltfFileScene(relativePath);
            var initialKey = scene.BuildCacheKey(root);

            var updatedTime = DateTime.UtcNow.AddMinutes(1);
            File.SetLastWriteTimeUtc(fullPath, updatedTime);
            var updatedKey = scene.BuildCacheKey(root);

            Assert.NotEqual(initialKey, updatedKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateDefault_DiscoversExternalGltfScenesRecursively()
    {
        var assetsRoot = Path.Combine(Path.GetTempPath(), $"SceneCatalogTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(assetsRoot);
        File.WriteAllText(Path.Combine(assetsRoot, "A.gltf"), "{}");
        File.WriteAllText(Path.Combine(assetsRoot, "b.gltf"), "{}");
        var nested = Path.Combine(assetsRoot, "droid");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Unit.gltf"), "{}");

        try
        {
            var scenes = SceneCatalog.CreateDefault(assetsRoot);

            var discovered = scenes.Where(s => s is GltfFileScene).Select(s => s.Title).ToArray();
            Assert.Equal(new[] { "Модель: A", "Модель: b", "Модель: Unit" }, discovered);
        }
        finally
        {
            Directory.Delete(assetsRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateDefault_WhenDirectoryMissing_ReturnsNoScenes()
    {
        var assetsRoot = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        var scenes = SceneCatalog.CreateDefault(assetsRoot);

        Assert.Empty(scenes);
    }

    private static SceneLoadService CreateCoreLoader()
    {
        return new SceneLoadService(new Scene3D(), "/tmp/assets", new DefaultSceneCameraPolicy(), new DefaultSceneDiagnosticsReporter(), new InMemorySceneAssetCache());
    }

    private sealed class ImmediateScheduler : IRenderThreadScheduler
    {
        public int EnqueueCalls { get; private set; }

        public void Enqueue(Action action)
        {
            EnqueueCalls++;
            action();
        }
    }

    private sealed class RecordingSandboxScene : ISandboxScene
    {
        public RecordingSandboxScene(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public string Title => $"title:{Id}";
        public string Description => $"description:{Id}";
        public int LoadCallCount { get; private set; }

        public void Load(Scene3D scene, string assetsRoot)
        {
            LoadCallCount++;
        }
    }

    private sealed class CacheableScene : ISandboxScene, ISceneAssetCacheKeyProvider
    {
        public CacheableScene(string id, string cacheKey)
        {
            Id = id;
            CacheKey = cacheKey;
        }

        public string Id { get; }
        public string CacheKey { get; }
        public string Title => Id;
        public string Description => Id;
        public int LoadCallCount { get; private set; }

        public string BuildCacheKey(string assetsRoot) => CacheKey;

        public void Load(Scene3D scene, string assetsRoot)
        {
            LoadCallCount++;
        }
    }

    private sealed class SceneWithOptions : ISandboxScene, ISceneLoadOptionsProvider
    {
        public SceneWithOptions(string id, bool autoFrame)
        {
            Id = id;
            LoadOptions = new SceneLoadOptions(autoFrame);
        }

        public string Id { get; }
        public string Title => Id;
        public string Description => Id;
        public SceneLoadOptions LoadOptions { get; }

        public void Load(Scene3D scene, string assetsRoot)
        {
        }
    }

    private sealed class TrackingCameraPolicy : ISceneCameraPolicy
    {
        public bool ApplyDefaultsCalled { get; private set; }
        public bool? LastAutoFrameOption { get; private set; }

        public void ApplyDefaults(Scene3D scene3D, ISandboxScene sceneInfo)
        {
            ApplyDefaultsCalled = true;
        }

        public void ApplyPostLoad(Scene3D scene3D, ISandboxScene sceneInfo, SceneLoadOptions loadOptions)
        {
            LastAutoFrameOption = loadOptions.AutoFrameCamera;
        }
    }

    private sealed class NoopDiagnosticsReporter : ISceneDiagnosticsReporter
    {
        public void Report(Scene3D scene3D, ISandboxScene sceneInfo)
        {
        }
    }

    private sealed class NoopCameraPolicy : ISceneCameraPolicy
    {
        public void ApplyDefaults(Scene3D scene3D, ISandboxScene sceneInfo)
        {
        }

        public void ApplyPostLoad(Scene3D scene3D, ISandboxScene sceneInfo, SceneLoadOptions loadOptions)
        {
        }
    }

    private sealed class TrackingSceneAssetCache : ISceneAssetCache
    {
        private readonly Dictionary<string, SceneAssetCacheEntry> _entries = new(StringComparer.Ordinal);

        public int TryGetCalls { get; private set; }
        public int SetCalls { get; private set; }

        public bool TryGet(string key, out SceneAssetCacheEntry entry)
        {
            TryGetCalls++;
            return _entries.TryGetValue(key, out entry!);
        }

        public void Set(string key, SceneAssetCacheEntry entry, TimeSpan? ttl = null)
        {
            SetCalls++;
            _entries[key] = entry;
        }

        public void Prewarm(IEnumerable<KeyValuePair<string, SceneAssetCacheEntry>> entries, TimeSpan? ttl = null)
        {
            foreach (var pair in entries)
            {
                _entries[pair.Key] = pair.Value;
            }
        }

        public void Invalidate(string key)
        {
            _entries.Remove(key);
        }
    }
}

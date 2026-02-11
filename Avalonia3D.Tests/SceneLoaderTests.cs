using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
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
        var scene = new Scene3D();
        var scheduler = new ImmediateScheduler();
        var loader = new SceneLoader(scene, assetsRoot: "/tmp/assets", scheduler);
        var first = new RecordingSandboxScene("first");
        var second = new RecordingSandboxScene("second");

        loader.Load(first);
        loader.Load(second);

        Assert.Equal(0, first.LoadCallCount);
        Assert.Equal(0, second.LoadCallCount);

        loader.MarkRendererReady();

        Assert.Equal(0, first.LoadCallCount);
        Assert.Equal(1, second.LoadCallCount);
        Assert.Equal(1, scheduler.EnqueueCalls);
    }

    [Fact]
    public void Load_WhenRendererReady_ExecutesOnSchedulerAndRaisesSceneChanged()
    {
        var scene = new Scene3D();
        var scheduler = new ImmediateScheduler();
        var loader = new SceneLoader(scene, assetsRoot: "/tmp/assets", scheduler);
        var sample = new RecordingSandboxScene("vehicle", addGeometry: true);
        ISandboxScene? changedTo = null;
        loader.SceneChanged += loaded => changedTo = loaded;

        loader.MarkRendererReady();
        loader.Load(sample);

        Assert.Equal(1, scheduler.EnqueueCalls);
        Assert.Equal(1, sample.LoadCallCount);
        Assert.Same(sample, changedTo);
        Assert.True(scene.Camera.Distance > 0f);
        Assert.True(scene.Camera.Near > 0f);
        Assert.True(scene.Camera.Far > scene.Camera.Near);
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

        Assert.Single(scenes);
        Assert.Equal("pbr-regression", scenes[0].Id);
    }


    [Fact]
    public void Load_WhenSceneDisablesAutoFrame_KeepsPresetCamera()
    {
        var scene = new Scene3D();
        var scheduler = new ImmediateScheduler();
        var loader = new SceneLoader(scene, assetsRoot: "/tmp/assets", scheduler);
        var sample = new FixedCameraScene("fixed");

        loader.MarkRendererReady();
        loader.Load(sample);

        Assert.Equal(42f, scene.Camera.Distance);
        Assert.Equal(0.3f, scene.Camera.Pitch);
        Assert.Equal(-0.1f, scene.Camera.Yaw);
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
        private readonly bool _addGeometry;

        public RecordingSandboxScene(string id, bool addGeometry = false)
        {
            Id = id;
            _addGeometry = addGeometry;
        }

        public string Id { get; }
        public string Title => $"title:{Id}";
        public string Description => $"description:{Id}";
        public int LoadCallCount { get; private set; }

        public void Load(Scene3D scene, string assetsRoot)
        {
            LoadCallCount++;

            if (!_addGeometry)
            {
                return;
            }

            scene.SceneGraph.Clear();
            scene.SceneGraph.AddRoot(CreateMesh(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f)));
        }

        private static MeshObject CreateMesh(Vector3 min, Vector3 max)
        {
            var mesh = new MeshObject();
            var model = new Model.Model
            {
                Vertices =
                [
                    new Vertex { Position = min },
                    new Vertex { Position = max }
                ]
            };

            mesh.AssignModel(model);
            return mesh;
        }
    }

    private sealed class FixedCameraScene : ISandboxScene, ISceneLoadOptionsProvider
    {
        public FixedCameraScene(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public string Title => $"title:{Id}";
        public string Description => $"description:{Id}";
        public SceneLoadOptions LoadOptions => new(AutoFrameCamera: false);

        public void Load(Scene3D scene, string assetsRoot)
        {
            scene.Camera.Distance = 42f;
            scene.Camera.Pitch = 0.3f;
            scene.Camera.Yaw = -0.1f;
        }
    }

}

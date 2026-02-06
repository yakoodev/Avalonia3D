using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;
using System.Numerics;

namespace Avalonia3D.Sandbox.Services;

public sealed class SceneLoader
{
    private readonly Scene3D _scene;
    private readonly string _assetsRoot;
    private bool _isRendererReady;
    private ISandboxScene? _pendingScene;

    public SceneLoader(Scene3D scene, string assetsRoot)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _assetsRoot = assetsRoot ?? throw new ArgumentNullException(nameof(assetsRoot));
    }

    public event Action<ISandboxScene>? SceneChanged;

    public void MarkRendererReady()
    {
        _isRendererReady = true;
        if (_pendingScene != null)
        {
            LoadInternal(_pendingScene);
            _pendingScene = null;
        }
    }

    public void Load(ISandboxScene scene)
    {
        if (scene == null)
        {
            throw new ArgumentNullException(nameof(scene));
        }

        Log.Information("Scene load requested: {SceneId} - {SceneTitle}", scene.Id, scene.Title);

        if (!_isRendererReady)
        {
            _pendingScene = scene;
            return;
        }

        LoadInternal(scene);
    }

    private void LoadInternal(ISandboxScene scene)
    {
        ApplyDefaults();
        scene.Load(_scene, _assetsRoot);
        Log.Information("Scene loaded: {SceneId} - {SceneTitle}", scene.Id, scene.Title);
        SceneChanged?.Invoke(scene);
    }

    private void ApplyDefaults()
    {
        _scene.Lights.Clear();

        var camera = _scene.Camera;
        camera.Target = Vector3.Zero;
        camera.Distance = 12f;
        camera.Pitch = -0.3f;
        camera.Yaw = 0.6f;
        camera.Fov = MathF.PI / 4;
        camera.Near = 0.1f;
        camera.Far = 200f;
    }
}

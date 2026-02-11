using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Sandbox.Scenes;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Avalonia3D.Sandbox.Services;

public sealed class SceneLoader
{
    private static readonly Dictionary<string, CameraPreset> CameraPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = new CameraPreset(12f, -0.3f, 0.6f, 0.1f, 200f),
        ["vehicle"] = new CameraPreset(35f, -0.2f, 0.6f, 0.1f, 1000f)
    };

    private readonly Scene3D _scene;
    private readonly string _assetsRoot;
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private bool _isRendererReady;
    private ISandboxScene? _pendingScene;

    public SceneLoader(Scene3D scene, string assetsRoot, IRenderThreadScheduler renderThreadScheduler)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _assetsRoot = assetsRoot ?? throw new ArgumentNullException(nameof(assetsRoot));
        _renderThreadScheduler = renderThreadScheduler ?? throw new ArgumentNullException(nameof(renderThreadScheduler));
    }

    public event Action<ISandboxScene>? SceneChanged;

    public void MarkRendererReady()
    {
        _isRendererReady = true;
        if (_pendingScene != null)
        {
            QueueLoad(_pendingScene);
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

        QueueLoad(scene);
    }

    private void QueueLoad(ISandboxScene scene)
    {
        _renderThreadScheduler.Enqueue(() => LoadInternal(scene));
    }

    private void LoadInternal(ISandboxScene scene)
    {
        ApplyDefaults(scene);
        scene.Load(_scene, _assetsRoot);

        if (SceneCameraFramer.TryFrame(_scene.SceneGraph, _scene.Camera))
        {
            Log.Information("Camera auto-framed for scene {SceneId}. Target: {Target}, Distance: {Distance:0.00}, Near/Far: {Near:0.00}/{Far:0.00}",
                scene.Id,
                _scene.Camera.Target,
                _scene.Camera.Distance,
                _scene.Camera.Near,
                _scene.Camera.Far);
        }
        else
        {
            Log.Warning("Scene {SceneId} has no geometry bounds for auto-frame; using preset camera.", scene.Id);
        }

        LogSceneDiagnostics(scene);
        if (_scene.LastImportReport.IsDegraded)
        {
            Log.Warning("Scene {SceneId} loaded in degraded import mode. Issues: {Issues}", scene.Id, string.Join(" | ", _scene.LastImportReport.Issues));
        }

        Log.Information("Scene loaded: {SceneId} - {SceneTitle}", scene.Id, scene.Title);
        SceneChanged?.Invoke(scene);
    }

    private void ApplyDefaults(ISandboxScene scene)
    {
        _scene.Lights.Clear();

        var preset = CameraPresets.TryGetValue(scene.Id, out var scenePreset)
            ? scenePreset
            : CameraPresets["default"];

        var camera = _scene.Camera;
        camera.Target = Vector3.Zero;
        camera.Distance = preset.Distance;
        camera.Pitch = preset.Pitch;
        camera.Yaw = preset.Yaw;
        camera.Fov = MathF.PI / 4;
        camera.Near = preset.Near;
        camera.Far = preset.Far;
    }

    private readonly record struct CameraPreset(float Distance, float Pitch, float Yaw, float Near, float Far);

    private void LogSceneDiagnostics(ISandboxScene scene)
    {
        var roots = _scene.SceneGraph.RootObjects;
        var meshes = CollectMeshes(roots);
        var transparent = meshes.Count(m => m.Material?.IsTransparent == true || m.Opacity < 1f);
        var sourceBlendMaterials = meshes.Count(m => m.Material?.SourceAlphaMode == MaterialAlphaMode.Blend);
        var opaque = meshes.Count - transparent;

        var hasBounds = SceneCameraFramer.TryComputeWorldBounds(_scene.SceneGraph, out var min, out var max);
        var boundsText = hasBounds
            ? $"Min={min}, Max={max}, Size={max - min}"
            : "no-geometry-bounds";

        Log.Information(
            "Scene diagnostics: {SceneId}. Roots={Roots}, Meshes={Meshes} (opaque={Opaque}, transparent={Transparent}, sourceAlphaBlend={SourceAlphaBlend}), Lights={Lights}, CameraPos={CameraPos}, CameraTarget={CameraTarget}, Bounds={Bounds}",
            scene.Id,
            roots.Count,
            meshes.Count,
            opaque,
            transparent,
            sourceBlendMaterials,
            _scene.Lights.Count,
            _scene.Camera.Position,
            _scene.Camera.Target,
            boundsText);

        if (_scene.Lights.Count == 0)
        {
            Log.Warning("Scene {SceneId} has zero lights. Objects can appear black depending on material/shader path.", scene.Id);
        }
    }

    private static List<MeshObject> CollectMeshes(IReadOnlyList<SceneObject> roots)
    {
        var result = new List<MeshObject>();
        foreach (var root in roots)
        {
            CollectMeshesRecursive(root, result);
        }

        return result;
    }

    private static void CollectMeshesRecursive(SceneObject node, List<MeshObject> result)
    {
        if (node is MeshObject mesh)
        {
            result.Add(mesh);
            return;
        }

        if (node is MeshGroup group)
        {
            foreach (var child in group)
            {
                CollectMeshesRecursive(child, result);
            }
        }
    }

}

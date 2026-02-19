using Avalonia3D.Sandbox.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia3D.Sandbox.Services;

public sealed class SceneRuntimeController : ISceneRuntimeController
{
    private readonly Dictionary<string, ISandboxScene> _scenesById;

    public SceneRuntimeController(string assetsRoot)
    {
        var scenes = SceneCatalog.CreateDefault(assetsRoot);
        Scenes = scenes;
        _scenesById = scenes.ToDictionary(scene => scene.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ISandboxScene> Scenes { get; }

    public string ResolveStartupSceneId(string preferredSceneId)
    {
        if (_scenesById.ContainsKey(preferredSceneId))
        {
            return preferredSceneId;
        }

        return Scenes.Count > 0 ? Scenes[0].Id : string.Empty;
    }

    public bool TryGetScene(string? sceneId, out ISandboxScene? scene, out string? error)
    {
        if (string.IsNullOrWhiteSpace(sceneId) || !_scenesById.TryGetValue(sceneId, out scene))
        {
            error = $"Scene '{sceneId}' not found";
            scene = null;
            return false;
        }

        error = null;
        return true;
    }
}

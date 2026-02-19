using Avalonia3D.Sandbox.Scenes;
using System.Collections.Generic;

namespace Avalonia3D.Sandbox.Services;

public interface ISceneRuntimeController
{
    IReadOnlyList<ISandboxScene> Scenes { get; }

    string ResolveStartupSceneId(string preferredSceneId);

    bool TryGetScene(string? sceneId, out ISandboxScene? scene, out string? error);
}

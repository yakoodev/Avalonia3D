using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;

namespace Avalonia3D.Sandbox.Services;

public interface ISceneDiagnosticsReporter
{
    void Report(Scene3D scene3D, ISandboxScene sceneInfo);
}

using System.Collections.Generic;

namespace Avalonia3D.Sandbox.Scenes;

public static class SceneCatalog
{
    public static IReadOnlyList<ISandboxScene> CreateDefault()
    {
        return new ISandboxScene[]
        {
            new SimpleScene(),
            new HierarchyScene(),
            new PbrScene(),
            new VehicleScene()
        };
    }
}

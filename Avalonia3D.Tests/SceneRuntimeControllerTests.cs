using Avalonia3D.Sandbox.Services;

namespace Avalonia3D.Tests;

public sealed class SceneRuntimeControllerTests
{
    [Fact]
    public void ResolveStartupSceneId_ReturnsPreferred_WhenExists()
    {
        var controller = new SceneRuntimeController(GetAssetsRoot());

        var startupId = controller.ResolveStartupSceneId("gltf:car2/scene");

        Assert.Equal("gltf:car2/scene", startupId);
    }

    [Fact]
    public void TryGetScene_ReturnsError_ForUnknownScene()
    {
        var controller = new SceneRuntimeController(GetAssetsRoot());

        var result = controller.TryGetScene("missing-scene", out var scene, out var error);

        Assert.False(result);
        Assert.Null(scene);
        Assert.NotNull(error);
    }

    private static string GetAssetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Avalonia3D.Sandbox", "Assets", "TestScenes");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Sandbox assets root not found.");
    }
}

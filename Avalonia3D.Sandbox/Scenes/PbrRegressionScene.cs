using Avalonia3D.Lights;
using Avalonia3D.Model;
using Serilog;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class PbrRegressionScene : ISandboxScene, ISceneLoadOptionsProvider
{
    public string Id => "pbr-regression";
    public string Title => "PBR Regression (фикс. свет/камера)";
    public string Description => "Регрессионная сцена для быстрой проверки PBR/Unlit и baseColor-текстур.";
    public SceneLoadOptions LoadOptions => new(AutoFrameCamera: false);

    public void Load(Scene3D scene, string assetsRoot)
    {
        var asset = PbrQaAssetRegistry.Load(assetsRoot).FirstOrDefault(x => x.IncludeInRegressionScene);
        if (asset == null)
        {
            Log.Warning("PBR regression scene skipped: no QA asset marked for regression scene.");
            return;
        }

        var path = Path.Combine(assetsRoot, asset.RelativePath);
        Log.Information("Loading PBR regression asset: {AssetPath}", path);
        scene.LoadScene(path);

        scene.Camera.Target = new Vector3(0f, 1.4f, 0f);
        scene.Camera.Distance = 11f;
        scene.Camera.Pitch = -0.22f;
        scene.Camera.Yaw = 0.46f;
        scene.Camera.Near = 0.1f;
        scene.Camera.Far = 250f;

        scene.Lights.Clear();
        scene.Lights.Add(new Light
        {
            Position = new Vector3(5f, 8f, 7f),
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 1.0f
        });

        scene.Lights.Add(new Light
        {
            Position = new Vector3(-6f, 4f, -5f),
            Color = new Vector3(0.75f, 0.82f, 1f),
            Intensity = 0.55f
        });
    }
}

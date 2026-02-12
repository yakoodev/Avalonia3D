using Avalonia3D.Lights;
using Avalonia3D.Model;
using Avalonia3D.Sandbox.Services;
using Serilog;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class PbrRegressionScene : ISandboxScene, ISceneLoadOptionsProvider
{
    private readonly PbrQaAssetEntry? _boundAsset;

    public PbrRegressionScene()
    {
    }

    public PbrRegressionScene(PbrQaAssetEntry boundAsset)
    {
        _boundAsset = boundAsset;
    }

    public string Id => _boundAsset == null
        ? "pbr-regression"
        : $"pbr-regression-{ToSceneToken(_boundAsset.RelativePath)}";

    public string Title => _boundAsset == null
        ? "PBR Regression (фикс. свет/камера)"
        : $"PBR Regression: {_boundAsset.DisplayName}";

    public string Description => _boundAsset == null
        ? "Регрессионная сцена для быстрой проверки PBR/Unlit и baseColor-текстур."
        : $"Отдельный регрессионный кейс для ассета {_boundAsset.DisplayName} (фикс. свет/камера).";
    public SceneLoadOptions LoadOptions => new(AutoFrameCamera: false);

    public void Load(Scene3D scene, string assetsRoot)
    {
        var asset = _boundAsset
            ?? PbrQaAssetRegistry.Load(assetsRoot).FirstOrDefault(x => x.IncludeInRegressionScene);
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

        GltfAssetDiagnostics.WriteCompactModelReport(path, scene);
    }

    private static string ToSceneToken(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        return new string(chars).Trim('-');
    }
}

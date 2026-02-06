using Avalonia3D.Lights;
using Avalonia3D.Model;
using Serilog;
using System;
using System.IO;
using System.Numerics;

namespace Avalonia3D.Sandbox.Scenes;

public sealed class GltfFileScene : ISandboxScene
{
    private readonly string _fileName;

    public GltfFileScene(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("GLTF file name is required.", nameof(fileName));

        _fileName = fileName;
        var shortName = Path.GetFileNameWithoutExtension(fileName);
        Id = $"gltf:{shortName.ToLowerInvariant()}";
        Title = $"Модель: {shortName}";
        Description = $"Авто-сцена для файла {fileName} из Assets/TestScenes.";
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }

    public void Load(Scene3D scene, string assetsRoot)
    {
        var path = Path.Combine(assetsRoot, _fileName);
        Log.Information("Loading auto-discovered GLTF scene from: {Path}", path);
        scene.LoadScene(path);

        if (scene.Lights.Count == 0)
        {
            // Единая дефолтная схема света для внешних моделей.
            scene.Lights.Add(new Light
            {
                Position = new Vector3(0f, 8f, 10f),
                Color = new Vector3(1f, 1f, 1f),
                Intensity = 1.0f
            });

            scene.Lights.Add(new Light
            {
                Position = new Vector3(-8f, 5f, -6f),
                Color = new Vector3(0.8f, 0.9f, 1f),
                Intensity = 0.65f
            });
        }
    }
}

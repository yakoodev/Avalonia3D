using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Sandbox.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Avalonia3D.Tests;

public class GltfAssetDiagnosticsTests
{
    [Fact]
    public void WriteCompactModelReport_CollectsNestedMeshesFromMeshGroup()
    {
        var gltfPath = Path.Combine(Path.GetTempPath(), $"diag-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(gltfPath, "{\"asset\":{\"version\":\"2.0\"},\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{}]}");

        try
        {
            var scene = new Scene3D();
            var root = new MeshGroup { Name = "root" };
            root.Add(CreateMesh("m0", "material:0"));
            root.Add(CreateMesh("m1", "material:1"));
            scene.SceneGraph.AddRoot(root);

            var reportPath = GltfAssetDiagnostics.WriteCompactModelReport(gltfPath, scene);
            var reportValues = ParseReport(reportPath);

            Assert.Equal("2", reportValues["objectsMesh"]);
            Assert.Equal("2", reportValues["meshesWithMaterial"]);
            Assert.Equal("0", reportValues["meshesWithoutMaterial"]);
            Assert.Equal("2", reportValues["materials"]);
        }
        finally
        {
            File.Delete(gltfPath);
        }
    }

    private static MeshObject CreateMesh(string name, string key)
    {
        var mesh = new MeshObject { Name = name };
        mesh.AssignModel(new Avalonia3D.Model.Model
        {
            Name = name,
            MaterialKey = key,
            Vertices =
            [
                new Vertex(),
                new Vertex(),
                new Vertex()
            ],
            Indices = [0u, 1u, 2u],
            Material = new Avalonia3D.Model.Material()
        });

        return mesh;
    }

    private static Dictionary<string, string> ParseReport(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = line[..idx];
            var value = line[(idx + 1)..];
            values[key] = value;
        }

        return values;
    }
}

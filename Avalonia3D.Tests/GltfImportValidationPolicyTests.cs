using Avalonia3D.Loaders;
using System;
using System.IO;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class GltfImportValidationPolicyTests
{
    [Fact]
    public void Import_ValidAsset_StrictMode_SucceedsWithoutDegradation()
    {
        var path = GetTestAssetPath("Fox.gltf");
        var importer = new GltfSceneImporter
        {
            ValidationPolicy = ImportValidationPolicy.Strict
        };

        var result = importer.ImportWithAnimations(path);

        Assert.False(result.IsDegraded);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Graph);
    }

    [Fact]
    public void Import_PartiallyBrokenAsset_RelaxedMode_MarksAsDegraded()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "partial.gltf");
            File.WriteAllText(path, """
            {
              "asset": { "version": "2.0" },
              "buffers": [ { "uri": "missing.bin", "byteLength": 12 } ],
              "bufferViews": [ { "buffer": 0, "byteOffset": 0, "byteLength": 12 } ],
              "accessors": [ { "bufferView": 0, "componentType": 5126, "count": 1, "type": "VEC3" } ],
              "meshes": [ { "primitives": [ { "attributes": { "POSITION": 0 } } ] } ],
              "nodes": [ { "mesh": 0 } ],
              "scenes": [ { "nodes": [0] } ],
              "scene": 0
            }
            """);

            var importer = new GltfSceneImporter
            {
                ValidationPolicy = ImportValidationPolicy.RelaxedWithWarnings
            };

            var result = importer.ImportWithAnimations(path);

            Assert.True(result.IsDegraded);
            Assert.Contains(result.Issues, issue => issue.Contains("Missing dependency", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Import_CompletelyBrokenAsset_StrictMode_ThrowsWithIssuesList()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "broken.gltf");
            File.WriteAllText(path, "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"missing.bin\",\"byteLength\":12}],\"bufferViews\":[{\"buffer\":5,\"byteOffset\":0,\"byteLength\":12}]}");

            var importer = new GltfSceneImporter
            {
                ValidationPolicy = ImportValidationPolicy.Strict
            };

            var exception = Assert.Throws<InvalidDataException>(() => importer.ImportWithAnimations(path));

            Assert.Contains("strict mode", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("missing.bin", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gltf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetTestAssetPath(string fileName)
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(current, "Avalonia3D.Sandbox", "Assets", "TestScenes", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Path.GetFullPath(Path.Combine(current, ".."));
        }

        throw new FileNotFoundException($"Test asset {fileName} not found.");
    }
}

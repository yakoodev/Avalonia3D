using Avalonia3D.Loaders;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "Import")]
public class GltfDependencyInspectorTests
{
    [Fact]
    public void ReadPreflight_ForGltf_CollectsExternalUris()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gltf-preflight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var path = Path.Combine(tempDir, "scene.gltf");
            File.WriteAllText(path, "{\"buffers\":[{\"uri\":\"scene.bin\"}],\"images\":[{\"uri\":\"textures/base.png\"}]}");

            var preflight = GltfDependencyInspector.ReadPreflight(path);

            Assert.Equal(GltfContainerKind.GltfJson, preflight.ContainerKind);
            Assert.True(preflight.ExternalDependencyScanSupported);
            Assert.Contains("scene.bin", preflight.ExternalUris);
            Assert.Contains("textures/base.png", preflight.ExternalUris);
            Assert.Empty(preflight.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadPreflight_ForGlb_ParsesJsonChunkWithoutJsonWarnings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"glb-preflight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var path = Path.Combine(tempDir, "scene.glb");
            WriteMinimalGlb(path, "{\"asset\":{\"version\":\"2.0\"},\"images\":[{\"uri\":\"textures/albedo.png\"}]}");

            var preflight = GltfDependencyInspector.ReadPreflight(path);

            Assert.Equal(GltfContainerKind.GlbBinary, preflight.ContainerKind);
            Assert.True(preflight.ExternalDependencyScanSupported);
            Assert.Contains("textures/albedo.png", preflight.ExternalUris);
            Assert.Empty(preflight.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteMinimalGlb(string path, string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonPaddedLength = (jsonBytes.Length + 3) & ~3;
        var padded = new byte[jsonPaddedLength];
        Buffer.BlockCopy(jsonBytes, 0, padded, 0, jsonBytes.Length);
        for (var i = jsonBytes.Length; i < padded.Length; i++)
        {
            padded[i] = 0x20;
        }

        var totalLength = 12 + 8 + padded.Length;

        using var fs = File.Create(path);
        using var writer = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);
        writer.Write(0x46546C67u); // glTF
        writer.Write(2u);
        writer.Write((uint)totalLength);
        writer.Write((uint)padded.Length);
        writer.Write(0x4E4F534Au); // JSON
        writer.Write(padded);
    }
}

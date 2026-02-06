using System;
using System.IO;
using Avalonia3D.Loaders;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class SceneGraphIdentityTests
{
    [Fact]
    public void FindNodeByKey_PrioritizesSemanticId_OverStableIdAndName()
    {
        var graph = new SceneGraph();

        var semanticNode = new MeshGroup { Name = "DoorBySemantic" };
        semanticNode.Node.SemanticId = "shared.key";
        semanticNode.Node.StableId = "stable.semantic";

        var stableNode = new MeshGroup { Name = "DoorByStable" };
        stableNode.Node.StableId = "shared.key";

        graph.AddRoot(semanticNode);
        graph.AddRoot(stableNode);

        var found = graph.FindNodeByKey("shared.key");

        Assert.Same(semanticNode.Node, found);
    }

    [Fact]
    public void Import_PreservesStableAndSemanticIdsAcrossReexportWithChangedIndices()
    {
        var firstPath = CreateTempGltf("""
            {
              "asset": { "version": "2.0" },
              "scenes": [ { "nodes": [1] } ],
              "nodes": [
                { "name": "Spare" },
                {
                  "name": "Door",
                  "extras": {
                    "id": "door-guid-001",
                    "semanticId": "vehicle.door.front_left"
                  }
                }
              ]
            }
            """);

        var secondPath = CreateTempGltf("""
            {
              "asset": { "version": "2.0" },
              "scenes": [ { "nodes": [0] } ],
              "nodes": [
                {
                  "name": "DoorRenamed",
                  "extras": {
                    "id": "door-guid-001",
                    "semanticId": "vehicle.door.front_left"
                  }
                },
                { "name": "AddedLater" }
              ]
            }
            """);

        try
        {
            var importer = new GltfSceneImporter();
            var graphV1 = importer.Import(firstPath);
            var graphV2 = importer.Import(secondPath);

            var doorV1 = graphV1.FindNodeByKey("vehicle.door.front_left");
            var doorV2 = graphV2.FindNodeByKey("vehicle.door.front_left");

            Assert.NotNull(doorV1);
            Assert.NotNull(doorV2);
            Assert.Equal("door-guid-001", doorV1!.StableId);
            Assert.Equal("door-guid-001", doorV2!.StableId);

            Assert.Same(doorV1, graphV1.FindNodeByKey("door-guid-001"));
            Assert.Same(doorV2, graphV2.FindNodeByKey("door-guid-001"));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    private static string CreateTempGltf(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scene-id-test-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, json);
        return path;
    }
}

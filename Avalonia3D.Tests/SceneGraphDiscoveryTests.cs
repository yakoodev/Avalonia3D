using System;
using System.Linq;
using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public sealed class SceneGraphDiscoveryTests
{
    [Fact]
    public void EnumerateNodes_ReturnsPreOrderDepthFirstTraversal()
    {
        var graph = CreateGraph();

        var orderedNames = graph.EnumerateNodes().Select(node => node.Name).ToArray();

        Assert.Equal(
            ["Root", "Vehicle", "WheelLF", "WheelRF", "DoorL", "MirrorL", "DoorR"],
            orderedNames);
    }

    [Fact]
    public void FindNodesByNameContains_UsesConfiguredComparisonAndTraversalOrder()
    {
        var graph = CreateGraph();

        var found = graph
            .FindNodesByNameContains("wHeEl", StringComparison.OrdinalIgnoreCase)
            .Select(node => node.Name)
            .ToArray();

        Assert.Equal(["WheelLF", "WheelRF"], found);
    }

    [Fact]
    public void FindNodesByNameStartsWith_ReturnsMatchingBranchInPreOrder()
    {
        var graph = CreateGraph();

        var found = graph
            .FindNodesByNameStartsWith("door", StringComparison.OrdinalIgnoreCase)
            .Select(node => node.Name)
            .ToArray();

        Assert.Equal(["DoorL", "DoorR"], found);
    }

    [Fact]
    public void FindNodeApis_RemainThinWrappersOverDiscoveryApi()
    {
        var graph = CreateGraph();

        var firstDoorByPredicate = graph.FindNodes(node => node.Name?.Contains("Door", StringComparison.Ordinal) == true)
            .FirstOrDefault();

        Assert.Same(firstDoorByPredicate, graph.FindNode("DoorL"));
        Assert.Same(firstDoorByPredicate, graph.FindByName("DoorL"));
    }

    private static SceneGraph CreateGraph()
    {
        var graph = new SceneGraph();

        var vehicle = new SceneNode { Name = "Vehicle" };
        var wheelLf = new SceneNode { Name = "WheelLF" };
        var wheelRf = new SceneNode { Name = "WheelRF" };
        var doorL = new SceneNode { Name = "DoorL" };
        var mirrorL = new SceneNode { Name = "MirrorL" };
        var doorR = new SceneNode { Name = "DoorR" };

        graph.Root.AddChild(vehicle);
        vehicle.AddChild(wheelLf);
        vehicle.AddChild(wheelRf);
        vehicle.AddChild(doorL);
        doorL.AddChild(mirrorL);
        vehicle.AddChild(doorR);

        return graph;
    }
}

using System.Numerics;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Shaders;
using Xunit;

namespace Avalonia3D.Tests;

public class EmissionUniformResolverTests
{
    [Fact]
    public void ResolveSceneEmissionColor_WithMaterial_StillUsesSceneObjectEmission()
    {
        var mesh = new MeshObject
        {
            EmissionColor = new Vector3(3f, 0.5f, 0.25f)
        };

        var material = new Material { EmissiveFactor = Vector3.Zero, EmissiveIntensity = 1f };

        var result = EmissionUniformResolver.ResolveSceneEmissionColor(material, mesh);

        Assert.Equal(mesh.EmissionColor, result);
    }

    [Fact]
    public void ResolveSceneEmissionColor_WithoutMaterial_UsesSceneObjectEmission()
    {
        var mesh = new MeshObject
        {
            EmissionColor = new Vector3(1f, 2f, 3f)
        };

        var result = EmissionUniformResolver.ResolveSceneEmissionColor(null, mesh);

        Assert.Equal(mesh.EmissionColor, result);
    }
}

using System.Numerics;
using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Shaders;
using Xunit;

namespace Avalonia3D.Tests;

public class EmissionUniformResolverTests
{
    [Fact]
    public void ResolveSceneEmissionColor_WithMaterial_AndNoOverride_ReturnsZero()
    {
        var mesh = new MeshObject
        {
            EmissionColor = new Vector3(3f, 0.5f, 0.25f)
        };

        var material = new Material { EmissiveFactor = Vector3.Zero, EmissiveIntensity = 1f };

        var result = EmissionUniformResolver.ResolveSceneEmissionColor(material, mesh);

        Assert.Equal(Vector3.Zero, result);
    }

    [Fact]
    public void ResolveSceneEmissionColor_WithMaterial_AndOverride_ReturnsOverrideColor()
    {
        var obj = new AdditiveEmissionSceneObject
        {
            EmissionColor = new Vector3(1f, 2f, 3f),
            HasAdditiveSceneEmission = true,
            AdditiveSceneEmissionColor = new Vector3(7f, 8f, 9f)
        };

        var material = new Material();
        var result = EmissionUniformResolver.ResolveSceneEmissionColor(material, obj);

        Assert.Equal(new Vector3(7f, 8f, 9f), result);
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

    private sealed class AdditiveEmissionSceneObject : SceneObject, IAdditiveSceneEmissionProvider
    {
        public bool HasAdditiveSceneEmission { get; set; }
        public Vector3 AdditiveSceneEmissionColor { get; set; }
        public override void Dispose() { }
        public override void Render(IRenderContext context) { }
    }
}

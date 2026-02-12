using Avalonia3D.Model.StandObjects;
using System.Numerics;
using Xunit;

namespace Avalonia3D.Tests;

public class SceneObjectDefaultsTests
{
    [Fact]
    public void MeshObject_DefaultEmissionColor_IsZero()
    {
        var mesh = new MeshObject();

        Assert.Equal(Vector3.Zero, mesh.EmissionColor);
    }
}

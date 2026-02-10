using System.Numerics;
using Avalonia3D.Model;
using Xunit;

namespace Avalonia3D.Tests;

public class MorphDrivenEmissionComposerTests
{
    [Fact]
    public void Compose_ZeroActivation_ReturnsBaseValues()
    {
        var c = new MorphDrivenEmissionComposer();
        var baseFactor = new Vector3(0.2f, 0.3f, 0.4f);
        var baseIntensity = 1.2f;
        var baseScene = new Vector3(0.1f, 0.2f, 0.3f);

        var r = c.Compose(baseFactor, baseIntensity, baseScene, 0f);

        Assert.Equal(baseFactor, r.EmissiveFactor);
        Assert.Equal(baseIntensity, r.EmissiveIntensity);
        Assert.Equal(baseScene, r.SceneEmissionColor);
    }

    [Fact]
    public void Compose_ActivationBoostsMaterialAndSceneRedChannel()
    {
        var c = new MorphDrivenEmissionComposer
        {
            MaterialIntensityBoost = 4f,
            SceneRedBoost = 2.5f
        };

        var r = c.Compose(
            baseEmissiveFactor: new Vector3(0.1f, 0.8f, 0.6f),
            baseEmissiveIntensity: 1f,
            baseSceneEmissionColor: new Vector3(0f, 0.3f, 0.2f),
            normalizedActivation: 0.5f);

        Assert.Equal(new Vector3(0.5f, 0.4f, 0.3f), r.EmissiveFactor);
        Assert.InRange(r.EmissiveIntensity, 2.99f, 3.01f);
        Assert.Equal(new Vector3(1.25f, 0.15f, 0.1f), r.SceneEmissionColor);
    }
}

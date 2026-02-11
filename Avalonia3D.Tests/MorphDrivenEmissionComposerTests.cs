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
            SceneActivationThreshold = 0.05f,
            SceneRedAtThreshold = 3.5f,
            SceneRedAtFullActivation = 9f
        };

        var r = c.Compose(
            baseEmissiveFactor: new Vector3(0.1f, 0.8f, 0.6f),
            baseEmissiveIntensity: 1f,
            baseSceneEmissionColor: new Vector3(0f, 0.3f, 0.2f),
            normalizedActivation: 0.5f);

        Assert.InRange(r.EmissiveFactor.X, 0.499f, 0.501f);
        Assert.InRange(r.EmissiveFactor.Y, 0.139f, 0.141f);
        Assert.InRange(r.EmissiveFactor.Z, 0.104f, 0.106f);
        Assert.InRange(r.EmissiveIntensity, 2.99f, 3.01f);
        Assert.InRange(r.SceneEmissionColor.X, 6.10f, 6.11f);
        Assert.InRange(r.SceneEmissionColor.Y, 0.014f, 0.016f);
        Assert.InRange(r.SceneEmissionColor.Z, 0.009f, 0.011f);
    }

    [Fact]
    public void Compose_BelowThreshold_DoesNotForceSceneRedBoost()
    {
        var c = new MorphDrivenEmissionComposer
        {
            SceneActivationThreshold = 0.2f,
            SceneRedAtThreshold = 3.5f,
            SceneRedAtFullActivation = 9f
        };

        var r = c.Compose(
            baseEmissiveFactor: new Vector3(0.1f, 0.1f, 0.1f),
            baseEmissiveIntensity: 1f,
            baseSceneEmissionColor: new Vector3(0.2f, 0.4f, 0.6f),
            normalizedActivation: 0.1f);

        Assert.InRange(r.SceneEmissionColor.X, 0.199f, 0.201f);
        Assert.InRange(r.SceneEmissionColor.Y, 0.035f, 0.037f);
        Assert.InRange(r.SceneEmissionColor.Z, 0.053f, 0.055f);
    }
}

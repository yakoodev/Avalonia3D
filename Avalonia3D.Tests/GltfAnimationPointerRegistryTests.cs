using Avalonia3D.Animation;
using Avalonia3D.Loaders;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class GltfAnimationPointerRegistryTests
{
    [Fact]
    public void PointerRegistry_UnknownPointer_GracefullyReturnsFalse()
    {
        var exception = Record.Exception(() => GltfSceneImporter.TryResolvePointerTargetForTests("/materials/0/extensions/UNKNOWN_ext/unknown", out var registration));

        Assert.Null(exception);
        Assert.False(GltfSceneImporter.TryResolvePointerTargetForTests("/materials/0/extensions/UNKNOWN_ext/unknown", out _));
    }

    [Fact]
    public void PointerRegistry_KnownEmissiveStrengthPointer_ResolvesCorrectBindingMetadata()
    {
        var resolved = GltfSceneImporter.TryResolvePointerTargetForTests(
            "/materials/3/extensions/KHR_materials_emissive_strength/emissiveStrength",
            out var registration);

        Assert.True(resolved);
        Assert.Equal(GltfAnimationPointerTargetKind.Material, registration.TargetKind);
        Assert.Equal(GltfAnimationPointerValueType.Float, registration.ValueType);
        Assert.Equal(AnimationTargetProperty.EmissiveIntensity, registration.RuntimeProperty);
        Assert.Null(registration.TextureSlot);
    }

    [Fact]
    public void PointerRegistry_KnownTextureTransformPointer_ResolvesCorrectBindingMetadata()
    {
        var resolved = GltfSceneImporter.TryResolvePointerTargetForTests(
            "/materials/1/pbrMetallicRoughness/baseColorTexture/extensions/KHR_texture_transform/offset",
            out var registration);

        Assert.True(resolved);
        Assert.Equal(GltfAnimationPointerTargetKind.Texture, registration.TargetKind);
        Assert.Equal(GltfAnimationPointerValueType.Vec3, registration.ValueType);
        Assert.Equal(AnimationTargetProperty.TextureTransformOffset, registration.RuntimeProperty);
        Assert.Equal(TextureSlot.BaseColor, registration.TextureSlot);
    }
}

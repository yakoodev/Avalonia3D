using Avalonia3D.Shaders;
using Xunit;

namespace Avalonia3D.Tests;

[Trait("TestTarget", "DomainLogic")]
public class PbrShaderSourceBuilderTests
{
    [Fact]
    public void Build_UsesHighPrecisionInFragmentShaderSource()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.None, maxLights: 4);

        Assert.Contains("precision highp float;", fragmentSource);
    }

    [Fact]
    public void Build_IncludesHdrSanitizationForOutputSafety()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.None, maxLights: 4);

        Assert.Contains("CompressHighlights", fragmentSource);
        Assert.Contains("SanitizeHdrColor", fragmentSource);
    }

    [Fact]
    public void Build_WithEmissiveMap_IncludesForceWhiteDebugSwitch()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.EmissiveMap, maxLights: 4);

        Assert.Contains("uForceWhiteEmissiveMap", fragmentSource);
        Assert.Contains("uForceWhiteEmissiveMap==1?vec3(1.0)", fragmentSource);
    }

    [Fact]
    public void Build_WithBaseColorAndEmissiveMaps_AppliesPerSemanticUvTransforms()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.BaseColorMap | PbrFeatures.EmissiveMap, maxLights: 4);

        Assert.Contains("uBaseColorUvOffset", fragmentSource);
        Assert.Contains("uBaseColorUvScale", fragmentSource);
        Assert.Contains("uBaseColorUvRotation", fragmentSource);
        Assert.Contains("uEmissiveUvOffset", fragmentSource);
        Assert.Contains("uEmissiveUvScale", fragmentSource);
        Assert.Contains("uEmissiveUvRotation", fragmentSource);
        Assert.Contains("ApplyManualBaseColorDecode(texture(uBaseColorMap, baseColorUv))", fragmentSource);
        Assert.Contains("ApplyManualEmissiveDecode(texture(uEmissiveMap, emissiveUv).rgb)", fragmentSource);
    }
    [Fact]
    public void Build_WithBaseColorAndEmissiveMaps_IncludesManualSrgbDecodeControls()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.BaseColorMap | PbrFeatures.EmissiveMap, maxLights: 4);

        Assert.Contains("uManualBaseColorSrgbDecode", fragmentSource);
        Assert.Contains("uManualEmissiveSrgbDecode", fragmentSource);
        Assert.Contains("uManualSheenColorSrgbDecode", fragmentSource);
        Assert.Contains("uManualSpecularColorSrgbDecode", fragmentSource);
        Assert.Contains("ApplyManualSrgbDecode", fragmentSource);
        Assert.Contains("ApplyManualBaseColorDecode", fragmentSource);
        Assert.Contains("ApplyManualEmissiveDecode", fragmentSource);
    }

    [Fact]
    public void Build_WithSpecularFeatureAndNoSpecularTextures_StillDeclaresSpecularSamples()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.Specular, maxLights: 4);

        Assert.Contains("float specularMapSample=1.0;", fragmentSource);
        Assert.Contains("vec3 specularColorMapSample=vec3(1.0);", fragmentSource);
        Assert.Contains("specularColor*=clamp(uSpecularFactor*specularMapSample", fragmentSource);
    }

    [Fact]
    public void Build_WithSpecularAndBaseColorMap_AppliesSpecularAfterSampleDeclarations()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.Specular | PbrFeatures.BaseColorMap, maxLights: 4);

        var specularMapDecl = fragmentSource.IndexOf("float specularMapSample", StringComparison.Ordinal);
        var specularColorMapDecl = fragmentSource.IndexOf("vec3 specularColorMapSample", StringComparison.Ordinal);
        var specularApply = fragmentSource.IndexOf("specularColor*=clamp(uSpecularFactor*specularMapSample", StringComparison.Ordinal);

        Assert.True(specularMapDecl >= 0);
        Assert.True(specularColorMapDecl >= 0);
        Assert.True(specularApply > specularMapDecl);
        Assert.True(specularApply > specularColorMapDecl);
    }

    [Fact]
    public void Build_WithTextureMaps_IncludesPerTextureTexCoordSetSelection()
    {
        var builder = new PbrShaderSourceBuilder();

        var (vertexSource, fragmentSource) = builder.Build(
            PbrFeatures.BaseColorMap | PbrFeatures.NormalMap | PbrFeatures.MetallicRoughnessMap | PbrFeatures.OcclusionMap | PbrFeatures.EmissiveMap,
            maxLights: 4);

        Assert.Contains("layout(location = 3) in vec2 aTexCoord1;", vertexSource);
        Assert.Contains("out vec2 TexCoord1;", vertexSource);
        Assert.Contains("SelectTexCoord", fragmentSource);
        Assert.Contains("uBaseColorTexCoordSet", fragmentSource);
        Assert.Contains("uNormalTexCoordSet", fragmentSource);
        Assert.Contains("uMetallicRoughnessTexCoordSet", fragmentSource);
        Assert.Contains("uOcclusionTexCoordSet", fragmentSource);
        Assert.Contains("uEmissiveTexCoordSet", fragmentSource);
        Assert.Contains("texture(uMetallicRoughnessMap, metallicRoughnessTexCoord)", fragmentSource);
        Assert.Contains("texture(uOcclusionMap, occlusionTexCoord)", fragmentSource);
    }

    [Fact]
    public void Build_IncludesPbrDebugViewUniformAndBranches()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.ReflectionsIbl, maxLights: 4);

        Assert.Contains("uPbrDebugViewMode", fragmentSource);
        Assert.Contains("debugSurfaceResult", fragmentSource);
        Assert.Contains("baseColor.rgb", fragmentSource);
        Assert.Contains("directLightComponent", fragmentSource);
        Assert.Contains("iblComponent", fragmentSource);
        Assert.Contains("vec3(ao)", fragmentSource);
        Assert.Contains("norm*0.5+0.5", fragmentSource);
    }


    [Fact]
    public void Build_WithSheenAndSpecularColorMaps_UsesCentralizedDecodeFunctions()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.SheenColorMap | PbrFeatures.SpecularColorMap, maxLights: 4);

        Assert.Contains("ApplyManualSheenColorDecode(texture(uSheenColorMap, baseTexCoord).rgb)", fragmentSource);
        Assert.Contains("ApplyManualSpecularColorDecode(texture(uSpecularColorMap, baseTexCoord).rgb)", fragmentSource);
    }

    [Fact]
    public void Build_WithIblReflections_IncludesSplitIntensityAndClampUniforms()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.ReflectionsIbl, maxLights: 4);

        Assert.Contains("uIblDiffuseIntensity", fragmentSource);
        Assert.Contains("uIblSpecularIntensity", fragmentSource);
        Assert.Contains("uReflectionContributionClamp", fragmentSource);
        Assert.Contains("uAmbientStrengthClamp", fragmentSource);
        Assert.Contains("iblDiffuse", fragmentSource);
        Assert.Contains("iblSpecular = reflection * max(specularColor, vec3(0.04))", fragmentSource);
        Assert.Contains("studioBase", fragmentSource);
        Assert.Contains("studioSpec", fragmentSource);
        Assert.Contains("metallicMask = smoothstep(0.55,0.9,metallic)", fragmentSource);
        Assert.Contains("iblSpecular *= metallicMask*mix(1.0,1.4,metallic)", fragmentSource);
        Assert.Contains("min((iblSpecular*mix(1.0,ao,0.2) + iblDiffuse*ao)", fragmentSource);
    }
    [Fact]
    public void Build_IncludesSeparateEmissiveSurfaceControls()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.EmissiveMap, maxLights: 4);

        Assert.Contains("uSeparateEmissiveTarget", fragmentSource);
        Assert.Contains("uSeparateEmissiveSurfaceScale", fragmentSource);
        Assert.Contains("totalEmissiveForSurface", fragmentSource);
    }

    [Fact]
    public void Build_PbrLighting_UsesNonWhiteningAmbientTerm()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.None, maxLights: 4);

        Assert.Contains("ambient=ambientStrength*0.35*diffuseColor*uLightColor[i]", fragmentSource);
        Assert.Contains("if(uLightCount==0){", fragmentSource);
        Assert.Contains("fallbackSpecular=specularColor", fragmentSource);
        Assert.Contains("resultLight=diffuseColor*0.04+fallbackSpecular", fragmentSource);
        Assert.Contains("iblDiffuse = diffuseColor * max(uIblDiffuseIntensity,0.0) * (1.0-metallic)", fragmentSource);
        Assert.Contains("metallicReflectionBoost", fragmentSource);
        Assert.Contains("reflection *= metallicReflectionBoost*(1.0-0.35*clamp(roughness,0.0,1.0))", fragmentSource);
        Assert.Contains("iblSpecular = reflection * max(specularColor, vec3(0.04))", fragmentSource);
        Assert.Contains("metallicMask = smoothstep(0.55,0.9,metallic)", fragmentSource);
        Assert.Contains("iblSpecular *= metallicMask*mix(1.0,1.4,metallic)", fragmentSource);
        Assert.Contains("iblComponent = min((iblSpecular*mix(1.0,ao,0.2) + iblDiffuse*ao)", fragmentSource);
    }

    [Fact]
    public void Build_WithTransmission_UsesTintedSurfaceInsteadOfSyntheticBlueBackground()
    {
        var builder = new PbrShaderSourceBuilder();

        var (_, fragmentSource) = builder.Build(PbrFeatures.Transmission, maxLights: 4);

        Assert.Contains("attenuationTint=mix(vec3(1.0), clamp(uTransmissionAttenuationColor", fragmentSource);
        Assert.Contains("transmittedLight=surfaceResult*attenuationTint", fragmentSource);
        Assert.Contains("mix(surfaceResult, transmittedLight, transmissionWeight)", fragmentSource);
    }

}

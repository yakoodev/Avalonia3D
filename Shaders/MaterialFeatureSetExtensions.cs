namespace Avalonia3D.Shaders;

public static class MaterialFeatureSetExtensions
{
    public static MaterialFeatureSet ToMaterialFeatureSet(this PbrFeatures features)
    {
        return (MaterialFeatureSet)features;
    }

    public static PbrFeatures ToPbrFeatures(this MaterialFeatureSet features)
    {
        return (PbrFeatures)((int)features & (int)PbrFeatureMask);
    }

    public static readonly MaterialFeatureSet PbrFeatureMask =
        MaterialFeatureSet.BaseColorMap |
        MaterialFeatureSet.NormalMap |
        MaterialFeatureSet.MetallicRoughnessMap |
        MaterialFeatureSet.OcclusionMap |
        MaterialFeatureSet.EmissiveMap |
        MaterialFeatureSet.ReflectionsIbl |
        MaterialFeatureSet.Transmission |
        MaterialFeatureSet.Clearcoat |
        MaterialFeatureSet.Sheen |
        MaterialFeatureSet.Specular |
        MaterialFeatureSet.Ior |
        MaterialFeatureSet.EmissiveStrength |
        MaterialFeatureSet.ClearcoatMap |
        MaterialFeatureSet.ClearcoatRoughnessMap |
        MaterialFeatureSet.ClearcoatNormalMap |
        MaterialFeatureSet.SheenColorMap |
        MaterialFeatureSet.SheenRoughnessMap |
        MaterialFeatureSet.SpecularMap |
        MaterialFeatureSet.SpecularColorMap |
        MaterialFeatureSet.TransmissionMap |
        MaterialFeatureSet.VolumeThicknessMap;
}

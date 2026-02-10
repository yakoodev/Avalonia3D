namespace Avalonia3D.Shaders;

public static class ShaderIds
{
    public const string PbrVariantPrefix = "pbr-variant-";
    public const string Pbr = "pbr";
    public const string PbrBaseColor = "pbr-base-color";
    public const string PbrBaseColorNormal = "pbr-base-color-normal";
    public const string PbrBaseColorNormalMetallicRoughness = "pbr-base-color-normal-metallic-roughness";
    public const string PbrBaseColorNormalMetallicRoughnessAoEmissive = "pbr-base-color-normal-metallic-roughness-ao-emissive";
    public const string PbrFull = "pbr-full";
    public const string PbrTransmission = "pbr-transmission";
    public const string PbrFullTransmission = "pbr-full-transmission";

    public const string Unlit = "unlit";
    public const string NormalsDebug = "normals-debug";

    public static string CreatePbrVariantId(PbrFeatures features)
    {
        return $"{PbrVariantPrefix}{(int)features}";
    }

    public static bool TryParsePbrVariantId(string? shaderId, out PbrFeatures features)
    {
        features = PbrFeatures.None;
        if (string.IsNullOrWhiteSpace(shaderId) || !shaderId.StartsWith(PbrVariantPrefix, System.StringComparison.Ordinal))
        {
            return false;
        }

        var value = shaderId[PbrVariantPrefix.Length..];
        if (!int.TryParse(value, out var mask))
        {
            return false;
        }

        features = (PbrFeatures)mask;
        return true;
    }
}

namespace Avalonia3D.Shaders;

public static class ShaderColorManagement
{
    public const string UniformBlock = "uniform int uManualBaseColorSrgbDecode; uniform int uManualEmissiveSrgbDecode;";

    public const string FunctionBlock = @"vec3 DecodeSrgbToLinear(vec3 color){ return pow(max(color, vec3(0.0)), vec3(2.2)); }
vec4 ApplyManualBaseColorDecode(vec4 sampleColor){ return uManualBaseColorSrgbDecode==1 ? vec4(DecodeSrgbToLinear(sampleColor.rgb), sampleColor.a) : sampleColor; }
vec3 ApplyManualEmissiveDecode(vec3 sampleColor){ return uManualEmissiveSrgbDecode==1 ? DecodeSrgbToLinear(sampleColor) : sampleColor; }";
}

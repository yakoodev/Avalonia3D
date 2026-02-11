namespace Avalonia3D.Shaders;

public static class ShaderColorManagement
{
    public const string UniformBlock = "uniform int uManualBaseColorSrgbDecode; uniform int uManualEmissiveSrgbDecode; uniform int uManualSheenColorSrgbDecode; uniform int uManualSpecularColorSrgbDecode;";

    public const string FunctionBlock = @"vec3 DecodeSrgbToLinear(vec3 color){ return pow(max(color, vec3(0.0)), vec3(2.2)); }
vec3 ApplyManualSrgbDecode(vec3 sampleColor, int decodeEnabled){ return decodeEnabled==1 ? DecodeSrgbToLinear(sampleColor) : sampleColor; }
vec4 ApplyManualSrgbDecode(vec4 sampleColor, int decodeEnabled){ return decodeEnabled==1 ? vec4(DecodeSrgbToLinear(sampleColor.rgb), sampleColor.a) : sampleColor; }
vec4 ApplyManualBaseColorDecode(vec4 sampleColor){ return ApplyManualSrgbDecode(sampleColor, uManualBaseColorSrgbDecode); }
vec3 ApplyManualEmissiveDecode(vec3 sampleColor){ return ApplyManualSrgbDecode(sampleColor, uManualEmissiveSrgbDecode); }
vec3 ApplyManualSheenColorDecode(vec3 sampleColor){ return ApplyManualSrgbDecode(sampleColor, uManualSheenColorSrgbDecode); }
vec3 ApplyManualSpecularColorDecode(vec3 sampleColor){ return ApplyManualSrgbDecode(sampleColor, uManualSpecularColorSrgbDecode); }";
}

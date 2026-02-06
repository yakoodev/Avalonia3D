using System.Text;

namespace Avalonia3D.Shaders;

public sealed class PbrShaderSourceBuilder
{
    public (string VertexSource, string FragmentSource) Build(PbrFeatures features, int maxLights)
    {
        return (BuildVertexShaderSource(), BuildFragmentShaderSource(features, maxLights));
    }

    private static string BuildVertexShaderSource()
    {
        return @"#version 300 es
precision mediump float;
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 uMVP;
uniform mat4 uModel;
uniform mat4 uLightSpaceMatrix;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;
out vec4 FragPosLightSpace;

void main()
{
    gl_Position = uMVP * vec4(aPosition, 1.0);
    FragPos = vec3(uModel * vec4(aPosition, 1.0));
    mat3 normalMatrix = transpose(inverse(mat3(uModel)));
    Normal = normalize(mat3(uModel) * aNormal);
    TexCoord = aTexCoord;
    FragPosLightSpace = uLightSpaceMatrix * vec4(FragPos, 1.0);
}";
    }

    private static string BuildFragmentShaderSource(PbrFeatures features, int maxLights)
    {
        var sb = new StringBuilder();

        sb.AppendLine("#version 300 es");
        sb.AppendLine("precision mediump float;");
        sb.AppendLine();
        sb.AppendLine("in vec2 TexCoord;");
        sb.AppendLine("in vec3 Normal;");
        sb.AppendLine("in vec3 FragPos;");
        sb.AppendLine("in vec4 FragPosLightSpace;");
        sb.AppendLine();
        sb.AppendLine("out vec4 FragColor;");
        sb.AppendLine();
        AppendUniforms(sb, maxLights);
        AppendShadowFunction(sb);

        if (features.HasFlag(PbrFeatures.NormalMap))
        {
            AppendNormalMappingSection(sb);
        }
        else
        {
            sb.AppendLine("vec3 GetNormal() { return normalize(Normal); }");
            sb.AppendLine();
        }

        if (features.HasFlag(PbrFeatures.ReflectionsIbl))
        {
            AppendReflectionsSection(sb);
        }
        else
        {
            sb.AppendLine("vec3 ComputeEnvironmentReflection(vec3 normal, vec3 viewDir, float roughness) { return vec3(0.0); }");
            sb.AppendLine();
        }

        AppendMainShaderBody(sb, features, maxLights);

        return sb.ToString();
    }

    private static void AppendUniforms(StringBuilder sb, int maxLights)
    {
        sb.AppendLine("uniform sampler2D uBaseColorMap;");
        sb.AppendLine("uniform sampler2D uNormalMap;");
        sb.AppendLine("uniform sampler2D uMetallicRoughnessMap;");
        sb.AppendLine("uniform sampler2D uOcclusionMap;");
        sb.AppendLine("uniform sampler2D uEmissiveMap;");
        sb.AppendLine();
        sb.AppendLine("uniform int uHasBaseColorMap;");
        sb.AppendLine("uniform int uHasNormalMap;");
        sb.AppendLine("uniform int uHasMetallicRoughnessMap;");
        sb.AppendLine("uniform int uHasOcclusionMap;");
        sb.AppendLine("uniform int uHasEmissiveMap;");
        sb.AppendLine();
        sb.AppendLine("uniform sampler2D uShadowMap;");
        sb.AppendLine("uniform int uHasShadowMap;");
        sb.AppendLine($"uniform vec3 uLightPos[{maxLights}];");
        sb.AppendLine($"uniform vec3 uLightColor[{maxLights}];");
        sb.AppendLine($"uniform float uIntensity[{maxLights}];");
        sb.AppendLine("uniform int uLightCount;");
        sb.AppendLine();
        sb.AppendLine("uniform vec3 uViewPos;");
        sb.AppendLine();
        sb.AppendLine("uniform float uAmbientStrength;");
        sb.AppendLine("uniform float uSpecularStrength;");
        sb.AppendLine("uniform int uShininess;");
        sb.AppendLine();
        sb.AppendLine("uniform vec3 uModelColor;");
        sb.AppendLine("uniform vec3 uEmissionColor;");
        sb.AppendLine();
        sb.AppendLine("uniform vec4 uBaseColorFactor;");
        sb.AppendLine("uniform float uMetallicFactor;");
        sb.AppendLine("uniform float uRoughnessFactor;");
        sb.AppendLine("uniform float uOcclusionStrength;");
        sb.AppendLine("uniform vec3 uEmissiveFactor;");
        sb.AppendLine();
        sb.AppendLine("uniform sampler2D uEnvironmentMap;");
        sb.AppendLine("uniform float uReflectionIntensity;");
        sb.AppendLine("uniform int uHasEnvironmentMap;");
        sb.AppendLine();
        sb.AppendLine("uniform float uAlpha;");
        sb.AppendLine();
    }

    private static void AppendShadowFunction(StringBuilder sb)
    {
        sb.AppendLine(@"float ShadowCalculation(vec4 fragPosLightSpace, vec3 normal, vec3 lightDir)
{
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    projCoords = projCoords * 0.5 + 0.5;

    float closestDepth = texture(uShadowMap, projCoords.xy).r;
    float currentDepth = projCoords.z;

    float bias = max(0.005 * (1.0 - dot(normal, lightDir)), 0.0005);
    float shadow = 0.0;
    vec2 texelSize = 1.0 / vec2(textureSize(uShadowMap, 0));

    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float pcfDepth = texture(uShadowMap, projCoords.xy + vec2(x, y) * texelSize).r;
            shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0;
        }
    }
    shadow /= 9.0;

    if(projCoords.z > 1.0)
        shadow = 0.0;

    return shadow;
}");
        sb.AppendLine();
    }

    private static void AppendNormalMappingSection(StringBuilder sb)
    {
        sb.AppendLine("// normal mapping");
        sb.AppendLine(@"vec3 GetNormal()
{
    vec3 norm = normalize(Normal);
    if (uHasNormalMap == 0)
    {
        return norm;
    }

    vec3 tangentNormal = texture(uNormalMap, TexCoord).xyz * 2.0 - 1.0;

    vec3 Q1 = dFdx(FragPos);
    vec3 Q2 = dFdy(FragPos);
    vec2 st1 = dFdx(TexCoord);
    vec2 st2 = dFdy(TexCoord);

    vec3 T = normalize(Q1 * st2.t - Q2 * st1.t);
    vec3 B = -normalize(cross(norm, T));
    mat3 TBN = mat3(T, B, norm);

    return normalize(TBN * tangentNormal);
}");
        sb.AppendLine();
    }

    private static void AppendReflectionsSection(StringBuilder sb)
    {
        sb.AppendLine("// reflections/IBL");
        sb.AppendLine(@"vec3 ComputeEnvironmentReflection(vec3 normal, vec3 viewDir, float roughness)
{
    if (uHasEnvironmentMap == 0)
    {
        return vec3(0.0);
    }

    vec3 reflectionDir = reflect(-viewDir, normal);
    vec2 envUv = vec2(atan(reflectionDir.z, reflectionDir.x) / (2.0 * 3.14159265) + 0.5, acos(clamp(reflectionDir.y, -1.0, 1.0)) / 3.14159265);
    vec3 reflectedColor = texture(uEnvironmentMap, envUv).rgb;
    float roughnessFade = 1.0 - clamp(roughness, 0.0, 1.0);
    return reflectedColor * roughnessFade * uReflectionIntensity;
}");
        sb.AppendLine();
    }

    private static void AppendMainShaderBody(StringBuilder sb, PbrFeatures features, int maxLights)
    {
        sb.AppendLine("void main()");
        sb.AppendLine("{");
        sb.AppendLine("    vec3 norm = GetNormal();");
        sb.AppendLine("    vec3 viewDir = normalize(uViewPos - FragPos);");
        sb.AppendLine();

        sb.AppendLine("    // base color/albedo");
        sb.AppendLine("    vec4 baseColor = uBaseColorFactor;");
        if (features.HasFlag(PbrFeatures.BaseColorMap))
        {
            sb.AppendLine("    if (uHasBaseColorMap == 1) { baseColor *= texture(uBaseColorMap, TexCoord); }");
        }
        sb.AppendLine();

        sb.AppendLine("    // metallic-roughness");
        sb.AppendLine("    float metallic = uMetallicFactor;");
        sb.AppendLine("    float roughness = uRoughnessFactor;");
        if (features.HasFlag(PbrFeatures.MetallicRoughnessMap))
        {
            sb.AppendLine("    if (uHasMetallicRoughnessMap == 1) { vec4 mrSample = texture(uMetallicRoughnessMap, TexCoord); metallic *= mrSample.b; roughness *= mrSample.g; }");
        }
        sb.AppendLine();

        sb.AppendLine("    // ao/emissive");
        sb.AppendLine("    float ao = 1.0;");
        if (features.HasFlag(PbrFeatures.OcclusionMap))
        {
            sb.AppendLine("    if (uHasOcclusionMap == 1) { float aoSample = texture(uOcclusionMap, TexCoord).r; ao = mix(1.0, aoSample, uOcclusionStrength); }");
        }
        sb.AppendLine("    vec3 emissive = uEmissiveFactor;");
        if (features.HasFlag(PbrFeatures.EmissiveMap))
        {
            sb.AppendLine("    if (uHasEmissiveMap == 1) { emissive *= texture(uEmissiveMap, TexCoord).rgb; }");
        }
        sb.AppendLine();

        sb.AppendLine($@"    vec3 albedo = baseColor.rgb;
    vec3 diffuseColor = albedo * (1.0 - metallic);
    vec3 specularColor = mix(vec3(0.04), albedo, metallic);

    vec3 resultLight = vec3(0.0);

    float smoothness = clamp(1.0 - roughness, 0.04, 1.0);
    float shininess = mix(2.0, float(uShininess), smoothness);

    for (int i = 0; i < {maxLights}; i++)
    {{
        if (i >= uLightCount)
            break;

        vec3 ambient = uAmbientStrength * uLightColor[i];

        vec3 lightDir = normalize(uLightPos[i] - FragPos);
        float diff = max(dot(norm, lightDir), 0.0);
        vec3 diffuse = diff * diffuseColor * uLightColor[i];

        vec3 reflectDir = reflect(-lightDir, norm);
        float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
        vec3 specular = uSpecularStrength * spec * specularColor * uLightColor[i];

        float shadow = uHasShadowMap == 1 ? ShadowCalculation(FragPosLightSpace, norm, lightDir) : 0.0;
        resultLight += (ambient + (1.0 - shadow) * (diffuse + specular)) * uIntensity[i];
    }}

    if (uLightCount == 0)
    {{
        resultLight = albedo * 0.65;
    }}

    vec3 reflection = ComputeEnvironmentReflection(norm, viewDir, roughness);
    vec3 result = resultLight * ao + emissive + uEmissionColor + reflection;
    float alpha = baseColor.a * uAlpha;
    FragColor = vec4(result, alpha);
}}");
    }
}

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
        sb.AppendLine("in vec2 TexCoord; in vec3 Normal; in vec3 FragPos; in vec4 FragPosLightSpace;");
        sb.AppendLine("layout(location = 0) out vec4 FragColor; layout(location = 1) out vec4 EmissiveColor;");
        AppendUniforms(sb, maxLights);
        AppendShadowFunction(sb);
        sb.AppendLine(features.HasFlag(PbrFeatures.NormalMap)
            ? @"vec3 GetNormal(){ vec3 norm=normalize(Normal); if(uHasNormalMap==0) return norm; vec3 t=texture(uNormalMap, TexCoord).xyz*2.0-1.0; vec3 Q1=dFdx(FragPos); vec3 Q2=dFdy(FragPos); vec2 st1=dFdx(TexCoord); vec2 st2=dFdy(TexCoord); vec3 T=normalize(Q1*st2.t-Q2*st1.t); vec3 B=-normalize(cross(norm,T)); return normalize(mat3(T,B,norm)*t);}"
            : "vec3 GetNormal(){ return normalize(Normal); }");
        sb.AppendLine(features.HasFlag(PbrFeatures.ReflectionsIbl)
            ? @"vec3 ComputeEnvironmentReflection(vec3 n, vec3 v, float r){ if(uHasEnvironmentMap==0) return vec3(0.0); vec3 rd=reflect(-v,n); vec2 uv=vec2(atan(rd.z, rd.x)/(2.0*3.14159265)+0.5, acos(clamp(rd.y,-1.0,1.0))/3.14159265); return texture(uEnvironmentMap, uv).rgb*(1.0-clamp(r,0.0,1.0))*uReflectionIntensity; }"
            : "vec3 ComputeEnvironmentReflection(vec3 n, vec3 v, float r){ return vec3(0.0); }");

        sb.AppendLine("void main(){ vec3 norm=GetNormal(); vec3 viewDir=normalize(uViewPos-FragPos); vec4 baseColor=uBaseColorFactor;");
        if (features.HasFlag(PbrFeatures.BaseColorMap)) sb.AppendLine("if(uHasBaseColorMap==1) baseColor*=texture(uBaseColorMap, TexCoord);");
        sb.AppendLine("float metallic=uMetallicFactor; float roughness=uRoughnessFactor;");
        if (features.HasFlag(PbrFeatures.MetallicRoughnessMap)) sb.AppendLine("if(uHasMetallicRoughnessMap==1){ vec4 mr=texture(uMetallicRoughnessMap, TexCoord); metallic*=mr.b; roughness*=mr.g; }");
        sb.AppendLine("float ao=1.0;");
        if (features.HasFlag(PbrFeatures.OcclusionMap)) sb.AppendLine("if(uHasOcclusionMap==1){ float a=texture(uOcclusionMap, TexCoord).r; ao=mix(1.0,a,uOcclusionStrength);} ");
        sb.AppendLine("vec3 emissive=uEmissiveFactor*max(uEmissiveIntensity,0.0);");
        if (features.HasFlag(PbrFeatures.EmissiveMap)) sb.AppendLine("if(uHasEmissiveMap==1) emissive*=texture(uEmissiveMap, TexCoord).rgb;");

        if (features.HasFlag(PbrFeatures.EmissiveStrength)) sb.AppendLine("emissive*=max(uMaterialEmissiveStrength,0.0);");
        if (features.HasFlag(PbrFeatures.Ior)) sb.AppendLine("float materialIor=max(uMaterialIor,1.0);"); else sb.AppendLine("float materialIor=1.5;");

        sb.AppendLine("vec3 albedo=baseColor.rgb; vec3 diffuseColor=albedo*(1.0-metallic); vec3 specularColor=mix(vec3(0.04), albedo, metallic);");
        if (features.HasFlag(PbrFeatures.Specular)) sb.AppendLine("specularColor*=clamp(uSpecularFactor*specularMapSample,0.0,1.0)*max(uSpecularColorFactor*specularColorMapSample, vec3(0.0));");

        if (features.HasFlag(PbrFeatures.ClearcoatMap)) sb.AppendLine("float clearcoatMapSample=uHasClearcoatMap==1?texture(uClearcoatMap, TexCoord).r:1.0;"); else sb.AppendLine("float clearcoatMapSample=1.0;");
        if (features.HasFlag(PbrFeatures.ClearcoatRoughnessMap)) sb.AppendLine("float clearcoatRoughnessMapSample=uHasClearcoatRoughnessMap==1?texture(uClearcoatRoughnessMap, TexCoord).g:1.0;"); else sb.AppendLine("float clearcoatRoughnessMapSample=1.0;");
        if (features.HasFlag(PbrFeatures.ClearcoatNormalMap)) sb.AppendLine("float clearcoatNormalInfluence=uHasClearcoatNormalMap==1?clamp(texture(uClearcoatNormalMap, TexCoord).z,0.0,1.0):1.0;"); else sb.AppendLine("float clearcoatNormalInfluence=1.0;");
        if (features.HasFlag(PbrFeatures.SheenColorMap)) sb.AppendLine("vec3 sheenColorMapSample=uHasSheenColorMap==1?texture(uSheenColorMap, TexCoord).rgb:vec3(1.0);"); else sb.AppendLine("vec3 sheenColorMapSample=vec3(1.0);");
        if (features.HasFlag(PbrFeatures.SheenRoughnessMap)) sb.AppendLine("float sheenRoughnessMapSample=uHasSheenRoughnessMap==1?texture(uSheenRoughnessMap, TexCoord).a:1.0;"); else sb.AppendLine("float sheenRoughnessMapSample=1.0;");
        if (features.HasFlag(PbrFeatures.SpecularMap)) sb.AppendLine("float specularMapSample=uHasSpecularMap==1?texture(uSpecularMap, TexCoord).a:1.0;"); else sb.AppendLine("float specularMapSample=1.0;");
        if (features.HasFlag(PbrFeatures.SpecularColorMap)) sb.AppendLine("vec3 specularColorMapSample=uHasSpecularColorMap==1?texture(uSpecularColorMap, TexCoord).rgb:vec3(1.0);"); else sb.AppendLine("vec3 specularColorMapSample=vec3(1.0);");
        if (features.HasFlag(PbrFeatures.TransmissionMap)) sb.AppendLine("float transmissionMapSample=uHasTransmissionMap==1?texture(uTransmissionMap, TexCoord).r:1.0;"); else sb.AppendLine("float transmissionMapSample=1.0;");
        if (features.HasFlag(PbrFeatures.VolumeThicknessMap)) sb.AppendLine("float thicknessMapSample=uHasVolumeThicknessMap==1?texture(uVolumeThicknessMap, TexCoord).g:1.0;"); else sb.AppendLine("float thicknessMapSample=1.0;");

        sb.AppendLine($"vec3 resultLight=vec3(0.0); float smoothness=clamp(1.0-roughness,0.04,1.0); float shininess=mix(2.0,float(uShininess),smoothness); for(int i=0;i<{maxLights};i++){{ if(i>=uLightCount) break; vec3 ambient=uAmbientStrength*uLightColor[i]; vec3 lightDir=normalize(uLightPos[i]-FragPos); float diff=max(dot(norm,lightDir),0.0); vec3 diffuse=diff*diffuseColor*uLightColor[i]; vec3 reflectDir=reflect(-lightDir,norm); float spec=pow(max(dot(viewDir,reflectDir),0.0),shininess); vec3 specular=uSpecularStrength*spec*specularColor*uLightColor[i]; float shadow=uHasShadowMap==1?ShadowCalculation(FragPosLightSpace,norm,lightDir):0.0; resultLight+=(ambient+(1.0-shadow)*(diffuse+specular))*uIntensity[i]; }} if(uLightCount==0) resultLight=albedo*0.65;");

        sb.AppendLine("vec3 reflection=ComputeEnvironmentReflection(norm,viewDir,roughness);");
        if (features.HasFlag(PbrFeatures.Clearcoat)) sb.AppendLine("reflection += ComputeEnvironmentReflection(norm, viewDir, clamp(uClearcoatRoughness*clearcoatRoughnessMapSample,0.0,1.0))*clamp(uClearcoatFactor*clearcoatMapSample*clearcoatNormalInfluence,0.0,1.0);");
        if (features.HasFlag(PbrFeatures.Sheen)) sb.AppendLine("vec3 sheenContribution = max(uSheenColorFactor * sheenColorMapSample, vec3(0.0)) * (1.0 - clamp(uSheenRoughnessFactor*sheenRoughnessMapSample,0.0,1.0));"); else sb.AppendLine("vec3 sheenContribution = vec3(0.0);");

        sb.AppendLine("vec3 totalEmissive = emissive + uEmissionColor; vec3 surfaceResult = resultLight*ao + totalEmissive + reflection + sheenContribution; float sampledAlpha = baseColor.a*uAlpha; if(uAlphaMode==1 && sampledAlpha<uAlphaCutoff){ discard; }");

        if (features.HasFlag(PbrFeatures.Transmission))
        {
            sb.AppendLine("vec3 transmittedLight=vec3(0.0); if(uHasTransmission==1){ float transmission=clamp(uTransmissionFactor*transmissionMapSample,0.0,1.0); vec3 refractedDir=refract(-viewDir,norm,1.0/max(materialIor,1.0)); float frontLighting=clamp(dot(-refractedDir,norm),0.0,1.0); float mappedThickness=max(uTransmissionThickness*thicknessMapSample,0.0); float thicknessFade=exp(-mappedThickness); float attenuationDistance=max(uTransmissionAttenuationDistance,0.0001); vec3 attenuation=exp(-uTransmissionAttenuationColor*(mappedThickness/attenuationDistance)); vec3 backgroundEstimate=vec3(0.5+0.5*frontLighting)*attenuation*thicknessFade; transmittedLight=backgroundEstimate*transmission;} vec3 result=mix(surfaceResult, transmittedLight+totalEmissive, uHasTransmission==1?clamp(uTransmissionFactor,0.0,1.0):0.0); float alphaBase=uAlphaMode==2?sampledAlpha:1.0; float transmissionAlpha=max(alphaBase,sampledAlpha); float alpha=mix(alphaBase, transmissionAlpha, float(uHasTransmission)); FragColor=vec4(result, alpha); EmissiveColor=vec4(max(totalEmissive, vec3(0.0)), alpha);");
        }
        else
        {
            sb.AppendLine("float alpha=uAlphaMode==2?sampledAlpha:1.0; FragColor=vec4(surfaceResult, alpha); EmissiveColor=vec4(max(totalEmissive, vec3(0.0)), alpha);");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendUniforms(StringBuilder sb, int maxLights)
    {
        sb.AppendLine("uniform sampler2D uBaseColorMap; uniform sampler2D uNormalMap; uniform sampler2D uMetallicRoughnessMap; uniform sampler2D uOcclusionMap; uniform sampler2D uEmissiveMap; uniform sampler2D uClearcoatMap; uniform sampler2D uClearcoatRoughnessMap; uniform sampler2D uClearcoatNormalMap; uniform sampler2D uSheenColorMap; uniform sampler2D uSheenRoughnessMap; uniform sampler2D uSpecularMap; uniform sampler2D uSpecularColorMap; uniform sampler2D uTransmissionMap; uniform sampler2D uVolumeThicknessMap;");
        sb.AppendLine("uniform int uHasBaseColorMap; uniform int uHasNormalMap; uniform int uHasMetallicRoughnessMap; uniform int uHasOcclusionMap; uniform int uHasEmissiveMap; uniform int uHasClearcoatMap; uniform int uHasClearcoatRoughnessMap; uniform int uHasClearcoatNormalMap; uniform int uHasSheenColorMap; uniform int uHasSheenRoughnessMap; uniform int uHasSpecularMap; uniform int uHasSpecularColorMap; uniform int uHasTransmissionMap; uniform int uHasVolumeThicknessMap;");
        sb.AppendLine($"uniform sampler2D uShadowMap; uniform int uHasShadowMap; uniform vec3 uLightPos[{maxLights}]; uniform vec3 uLightColor[{maxLights}]; uniform float uIntensity[{maxLights}]; uniform int uLightCount;");
        sb.AppendLine("uniform vec3 uViewPos; uniform float uAmbientStrength; uniform float uSpecularStrength; uniform int uShininess;");
        sb.AppendLine("uniform vec3 uModelColor; uniform vec3 uEmissionColor; uniform vec4 uBaseColorFactor; uniform float uMetallicFactor; uniform float uRoughnessFactor; uniform float uOcclusionStrength; uniform vec3 uEmissiveFactor;");
        sb.AppendLine("uniform sampler2D uEnvironmentMap; uniform float uReflectionIntensity; uniform int uHasEnvironmentMap; uniform float uAlpha; uniform float uAlphaCutoff; uniform int uAlphaMode; uniform float uEmissiveIntensity;");
        sb.AppendLine("uniform float uTransmissionFactor; uniform float uTransmissionThickness; uniform float uTransmissionIor; uniform float uTransmissionAttenuationDistance; uniform vec3 uTransmissionAttenuationColor; uniform int uHasTransmission;");
        sb.AppendLine("uniform float uClearcoatFactor; uniform float uClearcoatRoughness; uniform vec3 uSheenColorFactor; uniform float uSheenRoughnessFactor; uniform float uSpecularFactor; uniform vec3 uSpecularColorFactor; uniform float uMaterialIor; uniform float uMaterialEmissiveStrength;");
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
    for(int x = -1; x <= 1; ++x){ for(int y = -1; y <= 1; ++y){ float pcfDepth = texture(uShadowMap, projCoords.xy + vec2(x, y) * texelSize).r; shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0; }}
    shadow /= 9.0;
    if(projCoords.z > 1.0) shadow = 0.0;
    return shadow;
}");
    }
}

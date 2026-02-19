using Avalonia3D.Interfaces;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering;

public sealed class RuntimePbrShaderFactory : IRuntimePbrShaderFactory
{
    public IShader3D Create(GL? gl, MaterialFeatureSet features, int maxLights, IRenderCapabilities capabilities)
    {
        if (gl == null)
        {
            throw new InvalidOperationException("GL context is required for runtime PBR shader creation.");
        }

        if (!capabilities.Supports(features))
        {
            throw new InvalidOperationException($"Requested runtime feature set '{features}' is not supported by current render capabilities '{capabilities.SupportedMaterialFeatures}'.");
        }

        return GLShader.Create(gl, features.ToPbrFeatures(), maxLights);
    }
}

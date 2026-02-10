using Avalonia3D.Interfaces;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;
using System;

namespace Avalonia3D.Rendering;

public sealed class RuntimePbrShaderFactory : IRuntimePbrShaderFactory
{
    public IShader3D Create(GL? gl, PbrFeatures features, int maxLights)
    {
        if (gl == null)
        {
            throw new InvalidOperationException("GL context is required for runtime PBR shader creation.");
        }

        return GLShader.Create(gl, features, maxLights);
    }
}

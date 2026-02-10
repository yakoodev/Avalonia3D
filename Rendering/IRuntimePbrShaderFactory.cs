using Avalonia3D.Interfaces;
using Avalonia3D.Shaders;
using Silk.NET.OpenGL;

namespace Avalonia3D.Rendering;

public interface IRuntimePbrShaderFactory
{
    IShader3D Create(GL? gl, PbrFeatures features, int maxLights);
}

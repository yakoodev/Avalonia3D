using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Silk.NET.OpenGL;

namespace Avalonia3D.Rendering;

public sealed class ShaderSelectionPolicy
{
    public IShader3D? Select(Material? material, Scene3D scene, GL? gl)
    {
        if (material?.Shader is IShader3D explicitShader)
        {
            return explicitShader;
        }

        if (material?.ShaderId is { Length: > 0 } materialShaderId)
        {
            var byMaterialId = scene.ShaderRegistry.Get(materialShaderId, gl);
            if (byMaterialId != null)
            {
                return byMaterialId;
            }
        }

        if (!string.IsNullOrWhiteSpace(scene.ActiveShaderId))
        {
            var bySceneId = scene.ShaderRegistry.Get(scene.ActiveShaderId, gl);
            if (bySceneId != null)
            {
                return bySceneId;
            }
        }

        if (scene.RenderMode != ShaderRenderMode.Default)
        {
            var modeShaderId = scene.GetShaderIdForMode(scene.RenderMode);
            if (modeShaderId != null)
            {
                var byMode = scene.ShaderRegistry.Get(modeShaderId, gl);
                if (byMode != null)
                {
                    return byMode;
                }
            }
        }

        return scene.ShaderRegistry.GetDefault(gl);
    }
}

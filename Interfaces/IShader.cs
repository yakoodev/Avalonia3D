using System;
using System.Numerics;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;

namespace Avalonia3D.Interfaces
{
    public interface IShader : IDisposable
    {
        uint Handle { get; }
        void Use();
    }

    public interface IShader3D : IShader
    {
        void BindMaterial(Rendering.RenderResources resources, Model.Material? material, uint? shadowMapId = default);
        void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default);
    }
}

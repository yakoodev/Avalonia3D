using System;
using System.Numerics;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Interfaces
{
    public interface IShader : IDisposable
    {
        uint Handle { get; }
        void Use();
    }

    public interface IShader3D : IShader
    {
        void BindTexture(uint textureId, uint? shadowMapId = default);
        void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default);
    }
}

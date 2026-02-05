using Avalonia3D.Model;

namespace Avalonia3D.Interfaces
{
    public interface ISceneModule
    {
        void Attach(Scene3D scene);
        void Detach(Scene3D scene);
    }
}

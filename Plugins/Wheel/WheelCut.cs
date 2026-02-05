using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Plugins.Wheel
{
    public class WheelCut : MeshGroup
    {
        public Scene3D Context { get; private set; }
        public WheelCut(Scene3D scene3D)
        {
            Context = scene3D;
        }
    }
}

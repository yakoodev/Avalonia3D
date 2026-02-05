
namespace Avalonia3D.Model.StandObjects
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

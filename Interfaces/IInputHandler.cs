using Avalonia.Input;
using System.Numerics;

namespace Avalonia3D.Interfaces
{
    public interface IInputHandler
    {
        void OnMouseMove(Vector2 position);
        void OnMouseDown(Vector2 position, MouseButton button);
        void OnMouseUp(Vector2 position, MouseButton button);
        void OnMouseWheel(float delta);
        void OnKeyDown(Key key);
        void OnKeyUp(Key key);
    }
}

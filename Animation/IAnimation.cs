namespace Avalonia3D.Animation
{
    public interface IAnimation
    {
        bool IsSingtone { get; }
        bool Update(float deltaTime); // возвращает false если анимация закончилась
    }
}

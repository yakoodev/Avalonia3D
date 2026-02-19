namespace Avalonia3D.Animation;

public interface IAnimationBlendNode
{
    string ClipName { get; }
    bool Loop { get; }
    float PlaybackSpeed { get; }
}

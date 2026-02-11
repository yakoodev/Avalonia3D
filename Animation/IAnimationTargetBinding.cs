using Avalonia3D.Model;

namespace Avalonia3D.Animation
{
    public interface IAnimationTargetBinding
    {
        void Rebind(SceneGraph sceneGraph);
        void Apply(AnimationChannel channel, float time);
    }
}

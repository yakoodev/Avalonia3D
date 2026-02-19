using System.Collections.Generic;

namespace Avalonia3D.Animation;

public interface IAnimationStateMachine
{
    string CurrentState { get; }
    bool IsInTransition { get; }
    void SetParameter(AnimationParameter parameter);
    IReadOnlyDictionary<string, AnimationParameter> GetParameters();
    void Update(float deltaTime);
}

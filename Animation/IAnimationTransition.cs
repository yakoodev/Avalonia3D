using System.Collections.Generic;

namespace Avalonia3D.Animation;

public interface IAnimationTransition
{
    string FromState { get; }
    string ToState { get; }
    int Priority { get; }
    bool CanInterrupt { get; }
    float CrossFadeDuration { get; }
    bool CanTransition(IReadOnlyDictionary<string, AnimationParameter> parameters, bool sourceCompleted);
}

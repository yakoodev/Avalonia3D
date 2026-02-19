namespace Avalonia3D.Animation;

public enum AnimationParameterType
{
    Bool,
    Float,
    Trigger
}

public sealed record AnimationParameter(string Name, AnimationParameterType Type, bool BoolValue = false, float FloatValue = 0f)
{
    public static AnimationParameter Bool(string name, bool value) => new(name, AnimationParameterType.Bool, BoolValue: value);
    public static AnimationParameter Float(string name, float value) => new(name, AnimationParameterType.Float, FloatValue: value);
    public static AnimationParameter Trigger(string name) => new(name, AnimationParameterType.Trigger, BoolValue: true);
}

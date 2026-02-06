namespace Avalonia3D.Interaction.Behaviors;

public enum SceneCommandAction
{
    Open,
    Close,
    Toggle
}

public readonly record struct SceneCommand(string TargetSemanticId, SceneCommandAction Action, string? Payload = null)
{
    public static SceneCommand Open(string semanticId) => new(semanticId, SceneCommandAction.Open);
    public static SceneCommand Close(string semanticId) => new(semanticId, SceneCommandAction.Close);
    public static SceneCommand Toggle(string semanticId) => new(semanticId, SceneCommandAction.Toggle);
}

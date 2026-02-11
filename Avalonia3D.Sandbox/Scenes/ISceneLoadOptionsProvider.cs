namespace Avalonia3D.Sandbox.Scenes;

public interface ISceneLoadOptionsProvider
{
    SceneLoadOptions LoadOptions { get; }
}

public readonly record struct SceneLoadOptions(bool AutoFrameCamera)
{
    public static SceneLoadOptions Default { get; } = new(AutoFrameCamera: true);
}

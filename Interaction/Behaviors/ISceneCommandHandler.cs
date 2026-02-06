namespace Avalonia3D.Interaction.Behaviors;

public interface ISceneCommandHandler
{
    bool CanHandle(SceneCommand command);

    bool Handle(SceneCommand command);
}

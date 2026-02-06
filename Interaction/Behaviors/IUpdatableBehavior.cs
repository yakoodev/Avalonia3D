namespace Avalonia3D.Interaction.Behaviors;

public interface IUpdatableBehavior : ISceneBehavior
{
    void Update(float deltaTime);
}

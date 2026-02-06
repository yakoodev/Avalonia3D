using Avalonia3D.Model;

namespace Avalonia3D.Interaction.Behaviors;

public interface ISceneBehavior
{
    string Id { get; }

    void Attach(Scene3D scene);

    void Detach(Scene3D scene);
}

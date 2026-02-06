using System.Windows.Input;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class SceneItemViewModel
{
    public SceneItemViewModel(string title, string description, ICommand loadCommand)
    {
        Title = title;
        Description = description;
        LoadCommand = loadCommand;
    }

    public string Title { get; }
    public string Description { get; }
    public ICommand LoadCommand { get; }
}

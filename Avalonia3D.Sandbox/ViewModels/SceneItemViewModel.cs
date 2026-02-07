using System.Windows.Input;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class SceneItemViewModel
{
    public SceneItemViewModel(string title, string description, string fileNameBadge, string directoryBadge, string extensionBadge, ICommand loadCommand)
    {
        Title = title;
        Description = description;
        FileNameBadge = fileNameBadge;
        DirectoryBadge = directoryBadge;
        ExtensionBadge = extensionBadge;
        LoadCommand = loadCommand;
    }

    public string Title { get; }
    public string Description { get; }
    public string FileNameBadge { get; }
    public string DirectoryBadge { get; }
    public string ExtensionBadge { get; }
    public ICommand LoadCommand { get; }
}

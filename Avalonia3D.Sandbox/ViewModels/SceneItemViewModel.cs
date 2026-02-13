using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class SceneItemViewModel
{
    public SceneItemViewModel(string id, string title, string description, string fileNameBadge, string directoryBadge, string extensionBadge, string group, IReadOnlyList<string> tags, ICommand loadCommand)
    {
        Id = id;
        Title = title;
        Description = description;
        FileNameBadge = fileNameBadge;
        DirectoryBadge = directoryBadge;
        ExtensionBadge = extensionBadge;
        Group = group;
        Tags = tags;
        TagsBadge = tags.Count == 0 ? "-" : string.Join(", ", tags);
        LoadCommand = loadCommand;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string FileNameBadge { get; }
    public string DirectoryBadge { get; }
    public string ExtensionBadge { get; }
    public string Group { get; }
    public IReadOnlyList<string> Tags { get; }
    public string TagsBadge { get; }
    public ICommand LoadCommand { get; }
}

public sealed class SceneGroupViewModel
{
    public SceneGroupViewModel(string name, IReadOnlyList<SceneItemViewModel> scenes)
    {
        Name = name;
        Scenes = scenes;
    }

    public string Name { get; }
    public IReadOnlyList<SceneItemViewModel> Scenes { get; }
    public string Header => $"{Name} ({Scenes.Count})";
}

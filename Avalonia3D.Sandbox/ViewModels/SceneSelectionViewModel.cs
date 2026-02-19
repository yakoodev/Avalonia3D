using Avalonia3D.Sandbox.Scenes;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox.Utilities;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class SceneSelectionViewModel : BindableBase
{
    private readonly ISceneRuntimeController _sceneRuntimeController;
    private readonly Action<string> _requestLoadScene;
    private readonly HashSet<string> _loadedSceneIds = new(StringComparer.OrdinalIgnoreCase);
    private string _assetsRoot;
    private string? _selectedSceneId;
    private bool _isLoading;
    private bool _isRendererReady;
    private string? _lastLoadError;
    private string _currentSceneTitle = "Сцена не выбрана";

    public SceneSelectionViewModel(ISceneRuntimeController sceneRuntimeController, string assetsRoot, Action<string> requestLoadScene)
    {
        _sceneRuntimeController = sceneRuntimeController;
        _assetsRoot = assetsRoot;
        _requestLoadScene = requestLoadScene;

        Scenes = new ObservableCollection<SceneItemViewModel>(
            _sceneRuntimeController.Scenes.Select(CreateSceneItem));

        LoadSceneCommand = new RelayCommand(sceneId => RequestLoad(sceneId as string ?? SelectedSceneId));
    }

    public ObservableCollection<SceneItemViewModel> Scenes { get; }
    public RelayCommand LoadSceneCommand { get; }

    public string? SelectedSceneId
    {
        get => _selectedSceneId;
        set => SetProperty(ref _selectedSceneId, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RaisePropertyChanged(nameof(LoadStatusText));
            }
        }
    }

    public bool IsRendererReady
    {
        get => _isRendererReady;
        set
        {
            if (SetProperty(ref _isRendererReady, value))
            {
                RaisePropertyChanged(nameof(LoadStatusText));
            }
        }
    }

    public string? LastLoadError
    {
        get => _lastLoadError;
        set
        {
            if (SetProperty(ref _lastLoadError, value))
            {
                RaisePropertyChanged(nameof(LoadStatusText));
            }
        }
    }

    public string CurrentSceneTitle
    {
        get => _currentSceneTitle;
        private set => SetProperty(ref _currentSceneTitle, value);
    }

    public string LoadStatusText =>
        LastLoadError is not null ? $"Ошибка загрузки: {LastLoadError}" :
        IsLoading ? "Загрузка сцены..." :
        IsRendererReady ? "Renderer готов" : "Renderer не готов";

    public string CacheStatusText => $"Cache: загружено {_loadedSceneIds.Count} сцен(ы) | source: {_assetsRoot}";

    public string ResolveStartupSceneId(string preferredSceneId) => _sceneRuntimeController.ResolveStartupSceneId(preferredSceneId);

    public bool TryResolveScene(string? sceneId, out ISandboxScene? scene, out string? error)
    {
        return _sceneRuntimeController.TryGetScene(sceneId, out scene, out error);
    }

    public void MarkSceneLoaded(string sceneId, string title)
    {
        IsLoading = false;
        LastLoadError = null;
        SelectedSceneId = sceneId;
        CurrentSceneTitle = title;
        _loadedSceneIds.Add(sceneId);
        RaisePropertyChanged(nameof(CacheStatusText));
    }

    private void RequestLoad(string? sceneId)
    {
        if (!_sceneRuntimeController.TryGetScene(sceneId, out var scene, out var error) || scene is null)
        {
            LastLoadError = error;
            return;
        }

        LastLoadError = null;
        IsLoading = true;
        SelectedSceneId = scene.Id;
        _requestLoadScene(scene.Id);
    }

    private SceneItemViewModel CreateSceneItem(ISandboxScene sceneInfo)
    {
        return new SceneItemViewModel(
            sceneInfo.Id,
            sceneInfo.Title,
            sceneInfo.Description,
            sceneInfo is GltfFileScene gltfScene ? gltfScene.FileName : sceneInfo.Title,
            sceneInfo is GltfFileScene gltfSceneDir ? gltfSceneDir.Directory : "n/a",
            sceneInfo is GltfFileScene gltfSceneExt ? gltfSceneExt.Extension : "internal",
            new RelayCommand(_ => RequestLoad(sceneInfo.Id)));
    }
}

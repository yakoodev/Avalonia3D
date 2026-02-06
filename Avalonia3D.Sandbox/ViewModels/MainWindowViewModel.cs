using Avalonia3D.Model;
using Avalonia3D.Sandbox.Scenes;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly SceneLoader _sceneLoader;
    private string _currentSceneTitle = "Сцена не выбрана";

    public MainWindowViewModel(Scene3D scene, string assetsRoot, IRenderThreadScheduler renderThreadScheduler)
    {
        _sceneLoader = new SceneLoader(scene, assetsRoot, renderThreadScheduler);
        _sceneLoader.SceneChanged += sceneInfo => CurrentSceneTitle = sceneInfo.Title;

        var scenes = SceneCatalog.CreateDefault();
        Scenes = new ObservableCollection<SceneItemViewModel>(
            scenes.Select(sceneInfo =>
                new SceneItemViewModel(
                    sceneInfo.Title,
                    sceneInfo.Description,
                    new RelayCommand(_ => _sceneLoader.Load(sceneInfo)))));

        if (scenes.Count > 0)
        {
            _sceneLoader.Load(scenes[0]);
        }
    }

    public ObservableCollection<SceneItemViewModel> Scenes { get; }

    public string CurrentSceneTitle
    {
        get => _currentSceneTitle;
        private set
        {
            if (_currentSceneTitle != value)
            {
                _currentSceneTitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSceneTitle)));
            }
        }
    }

    public void MarkRendererReady() => _sceneLoader.MarkRendererReady();

    public event PropertyChangedEventHandler? PropertyChanged;
}

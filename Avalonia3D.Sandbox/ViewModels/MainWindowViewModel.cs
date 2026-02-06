using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Avalonia3D.Sandbox.Scenes;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox.Utilities;
using Avalonia3D.Shaders;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly SceneLoader _sceneLoader;
    private readonly Scene3D _scene;
    private string _currentSceneTitle = "Сцена не выбрана";
    private ShaderRenderMode _selectedRenderMode;

    public MainWindowViewModel(Scene3D scene, string assetsRoot, IRenderThreadScheduler renderThreadScheduler)
    {
        _scene = scene;
        _sceneLoader = new SceneLoader(scene, assetsRoot, renderThreadScheduler);
        _sceneLoader.SceneChanged += sceneInfo => CurrentSceneTitle = sceneInfo.Title;

        var scenes = SceneCatalog.CreateDefault(assetsRoot);
        Scenes = new ObservableCollection<SceneItemViewModel>(
            scenes.Select(sceneInfo =>
                new SceneItemViewModel(
                    sceneInfo.Title,
                    sceneInfo.Description,
                    new RelayCommand(_ => _sceneLoader.Load(sceneInfo)))));

        ShaderModes = new ObservableCollection<ShaderRenderMode>(new[]
        {
            ShaderRenderMode.Pbr,
            ShaderRenderMode.Unlit,
            ShaderRenderMode.NormalsDebug
        });

        SwitchShaderModeCommand = new RelayCommand(mode =>
        {
            if (mode is ShaderRenderMode renderMode)
            {
                ApplyShaderMode(renderMode);
            }
        });

        _scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        _scene.BindRenderMode(ShaderRenderMode.Unlit, ShaderIds.Unlit);
        _scene.BindRenderMode(ShaderRenderMode.NormalsDebug, ShaderIds.NormalsDebug);
        ApplyShaderMode(ShaderRenderMode.Pbr);

        if (scenes.Count > 0)
        {
            _sceneLoader.Load(scenes[0]);
        }
    }

    public ObservableCollection<SceneItemViewModel> Scenes { get; }
    public ObservableCollection<ShaderRenderMode> ShaderModes { get; }
    public RelayCommand SwitchShaderModeCommand { get; }

    public ShaderRenderMode SelectedRenderMode
    {
        get => _selectedRenderMode;
        set
        {
            if (_selectedRenderMode == value)
            {
                return;
            }

            ApplyShaderMode(value);
        }
    }

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

    private void ApplyShaderMode(ShaderRenderMode mode)
    {
        _selectedRenderMode = mode;
        _scene.RenderMode = mode;
        _scene.ActiveShaderId = _scene.GetShaderIdForMode(mode) ?? ShaderIds.Pbr;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRenderMode)));
    }
}

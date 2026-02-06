using Avalonia3D.Animation;
using Avalonia3D.Interaction.CameraController;
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
    private readonly CameraController _cameraController;
    private string _currentSceneTitle = "Сцена не выбрана";
    private ShaderRenderMode _selectedRenderMode;
    private string? _selectedClipName;
    private bool _isLoopEnabled;
    private double _playbackSpeed = 1.0;
    private ClipPlaybackState _selectedClipState;

    public MainWindowViewModel(Scene3D scene, CameraController cameraController, string assetsRoot, IRenderThreadScheduler renderThreadScheduler)
    {
        _scene = scene;
        _cameraController = cameraController;
        _sceneLoader = new SceneLoader(scene, assetsRoot, renderThreadScheduler);
        _sceneLoader.SceneChanged += sceneInfo =>
        {
            CurrentSceneTitle = sceneInfo.Title;
            RefreshClips();
            _cameraController.CaptureHomeView();
        };

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

        AvailableClips = new ObservableCollection<string>();

        SwitchShaderModeCommand = new RelayCommand(mode =>
        {
            if (mode is ShaderRenderMode renderMode)
            {
                ApplyShaderMode(renderMode);
            }
        });

        PlayClipCommand = new RelayCommand(_ => Play());
        PauseClipCommand = new RelayCommand(_ => Pause());
        StopClipCommand = new RelayCommand(_ => Stop());
        TogglePlayPauseCommand = new RelayCommand(_ => TogglePlayPause());

        FrameAllCommand = new RelayCommand(_ => _cameraController.FrameAll());
        ResetViewCommand = new RelayCommand(_ => _cameraController.ResetView());
        ToggleCameraModeCommand = new RelayCommand(_ =>
        {
            _cameraController.ToggleControlMode();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCameraMode)));
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
    public ObservableCollection<string> AvailableClips { get; }
    public RelayCommand SwitchShaderModeCommand { get; }
    public RelayCommand PlayClipCommand { get; }
    public RelayCommand PauseClipCommand { get; }
    public RelayCommand StopClipCommand { get; }
    public RelayCommand TogglePlayPauseCommand { get; }
    public RelayCommand FrameAllCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand ToggleCameraModeCommand { get; }

    public string CurrentCameraMode => _cameraController.ControlMode.ToString();

    public double OrbitSensitivity
    {
        get => _cameraController.OrbitSensitivity;
        set
        {
            _cameraController.OrbitSensitivity = (float)value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OrbitSensitivity)));
        }
    }

    public double PanSensitivity
    {
        get => _cameraController.PanSensitivity;
        set
        {
            _cameraController.PanSensitivity = (float)value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanSensitivity)));
        }
    }

    public double DollySensitivity
    {
        get => _cameraController.DollySensitivity;
        set
        {
            _cameraController.DollySensitivity = (float)value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DollySensitivity)));
        }
    }

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

    public string? SelectedClipName
    {
        get => _selectedClipName;
        set
        {
            if (_selectedClipName == value)
            {
                return;
            }

            _selectedClipName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedClipName)));
            UpdateSelectedClipState();
        }
    }

    public bool IsLoopEnabled
    {
        get => _isLoopEnabled;
        set
        {
            if (_isLoopEnabled == value)
            {
                return;
            }

            _isLoopEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoopEnabled)));
        }
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (Math.Abs(_playbackSpeed - value) < 0.0001)
            {
                return;
            }

            _playbackSpeed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaybackSpeed)));
        }
    }

    public string ClipStateText => SelectedClipState.IsRegistered
        ? $"{SelectedClipState.ClipName}: {(SelectedClipState.IsPlaying ? (SelectedClipState.IsPaused ? "Paused" : "Playing") : "Stopped")}, t={SelectedClipState.Time:0.00}/{SelectedClipState.Duration:0.00}, speed={SelectedClipState.Speed:0.00}, loop={SelectedClipState.Loop}"
        : "Клип не выбран";

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

    public void HandleShortcutFrame() => _cameraController.FrameAll();
    public void HandleShortcutReset() => _cameraController.ResetView();
    public void HandleShortcutPlayPause() => TogglePlayPause();

    public event PropertyChangedEventHandler? PropertyChanged;

    private ClipPlaybackState SelectedClipState
    {
        get => _selectedClipState;
        set
        {
            _selectedClipState = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClipStateText)));
        }
    }

    private void ApplyShaderMode(ShaderRenderMode mode)
    {
        _selectedRenderMode = mode;
        _scene.RenderMode = mode;
        _scene.ActiveShaderId = _scene.GetShaderIdForMode(mode) ?? ShaderIds.Pbr;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRenderMode)));
    }

    private void RefreshClips()
    {
        AvailableClips.Clear();
        foreach (var clip in _scene.AnimatorComponent.GetClipNames())
        {
            AvailableClips.Add(clip);
        }

        SelectedClipName = AvailableClips.FirstOrDefault();
    }

    private void UpdateSelectedClipState()
    {
        SelectedClipState = string.IsNullOrWhiteSpace(SelectedClipName)
            ? default
            : _scene.AnimatorComponent.GetClipState(SelectedClipName);
    }

    private void Play()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        _scene.AnimatorComponent.PlayClip(SelectedClipName, IsLoopEnabled, (float)PlaybackSpeed);
        UpdateSelectedClipState();
    }

    private void Pause()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        _scene.AnimatorComponent.PauseClip(SelectedClipName);
        UpdateSelectedClipState();
    }

    private void Stop()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        _scene.AnimatorComponent.StopClip(SelectedClipName);
        UpdateSelectedClipState();
    }

    private void TogglePlayPause()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        var state = _scene.AnimatorComponent.GetClipState(SelectedClipName);
        if (state.IsPlaying && !state.IsPaused)
        {
            Pause();
            return;
        }

        Play();
    }
}

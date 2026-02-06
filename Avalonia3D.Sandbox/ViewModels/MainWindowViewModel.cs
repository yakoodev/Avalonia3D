using Avalonia.Media;
using Avalonia.Threading;
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
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private readonly Action<GraphicsProfile> _applyGraphicsProfile;
    private string _currentSceneTitle = "Сцена не выбрана";
    private ShaderRenderMode _selectedRenderMode;
    private string? _selectedClipName;
    private bool _isLoopEnabled;
    private double _playbackSpeed = 1.0;
    private ClipPlaybackState _selectedClipState;
    private RenderQualityPreset _selectedQualityPreset = RenderQualityPreset.Medium;
    private GraphicsProfile _graphicsProfile = GraphicsProfile.Medium;
    private Color _selectedBackgroundColor = Color.FromRgb(15, 15, 20);
    private string _profileJsonEditor = string.Empty;
    private string _profileStatusMessage = "";

    public MainWindowViewModel(Scene3D scene, CameraController cameraController, string assetsRoot, IRenderThreadScheduler renderThreadScheduler, Action<GraphicsProfile> applyGraphicsProfile)
    {
        _scene = scene;
        _cameraController = cameraController;
        _renderThreadScheduler = renderThreadScheduler;
        _applyGraphicsProfile = applyGraphicsProfile;
        _sceneLoader = new SceneLoader(scene, assetsRoot, renderThreadScheduler);
        _sceneLoader.SceneChanged += sceneInfo =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentSceneTitle = sceneInfo.Title;
                RefreshClips();
                ExecuteOnRenderThread(() => _cameraController.CaptureHomeView());
            });
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
        QualityPresets = new ObservableCollection<RenderQualityPreset>(new[]
        {
            RenderQualityPreset.Low,
            RenderQualityPreset.Medium,
            RenderQualityPreset.High,
            RenderQualityPreset.Custom
        });

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

        FrameAllCommand = new RelayCommand(_ => ExecuteOnRenderThread(() => _cameraController.FrameAll()));
        ResetViewCommand = new RelayCommand(_ => ExecuteOnRenderThread(() => _cameraController.ResetView()));
        ToggleCameraModeCommand = new RelayCommand(_ =>
        {
            ExecuteOnRenderThread(() => _cameraController.ToggleControlMode());
            Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCameraMode))));
        });

        ApplyProfileJsonCommand = new RelayCommand(_ => ApplyProfileJson());
        ResetProfileJsonCommand = new RelayCommand(_ => ResetProfileJson());

        ApplyRenderQualityPreset(RenderQualityPreset.Medium);

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
    public ObservableCollection<RenderQualityPreset> QualityPresets { get; }
    public RelayCommand SwitchShaderModeCommand { get; }
    public RelayCommand PlayClipCommand { get; }
    public RelayCommand PauseClipCommand { get; }
    public RelayCommand StopClipCommand { get; }
    public RelayCommand TogglePlayPauseCommand { get; }
    public RelayCommand FrameAllCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand ToggleCameraModeCommand { get; }
    public RelayCommand ApplyProfileJsonCommand { get; }
    public RelayCommand ResetProfileJsonCommand { get; }

    public string CurrentCameraMode => _cameraController.ControlMode.ToString();

    public RenderQualityPreset SelectedQualityPreset
    {
        get => _selectedQualityPreset;
        set
        {
            if (_selectedQualityPreset == value)
            {
                return;
            }

            ApplyRenderQualityPreset(value);
        }
    }

    public string ActiveQualitySummary => _graphicsProfile.ToSummary();

    public string ActiveProfileJson => _graphicsProfile.ToJson();

    public string ProfileJsonEditor
    {
        get => _profileJsonEditor;
        set
        {
            if (_profileJsonEditor == value)
            {
                return;
            }

            _profileJsonEditor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProfileJsonEditor)));
        }
    }

    public string ProfileStatusMessage
    {
        get => _profileStatusMessage;
        private set
        {
            if (_profileStatusMessage == value)
            {
                return;
            }

            _profileStatusMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProfileStatusMessage)));
        }
    }

    public Color SelectedBackgroundColor
    {
        get => _selectedBackgroundColor;
        set
        {
            if (_selectedBackgroundColor == value)
            {
                return;
            }

            _selectedBackgroundColor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBackgroundColor)));
            ApplyBackgroundColor(value);
        }
    }

    public string GraphicsTuningHint =>
        "Подсказка: High + тени 4096 + отражения IBL дают лучшую картинку, но дороже по FPS. " +
        "Если картинка темная — поднимайте Exposure и IBL Intensity в JSON, если шум/лесенка — увеличьте MSAA и тени.";

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

    public void HandleShortcutFrame() => ExecuteOnRenderThread(() => _cameraController.FrameAll());
    public void HandleShortcutReset() => ExecuteOnRenderThread(() => _cameraController.ResetView());
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

    private void ApplyRenderQualityPreset(RenderQualityPreset preset)
    {
        _selectedQualityPreset = preset;
        _graphicsProfile = GraphicsProfile.FromPreset(preset, _graphicsProfile) with
        {
            Name = preset == RenderQualityPreset.Custom ? _graphicsProfile.Name : preset.ToString()
        };

        _graphicsProfile = _graphicsProfile.Validate();
        ApplyProfile(_graphicsProfile, $"Применен профиль: {_graphicsProfile.Name}");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedQualityPreset)));
    }

    public void ImportProfileFromJson(string json)
    {
        var profile = GraphicsProfile.FromJson(json);
        _selectedQualityPreset = profile.QualityPreset;
        ApplyProfile(profile, $"JSON профиль загружен: {profile.Name}");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedQualityPreset)));
    }

    private void ApplyProfileJson()
    {
        if (string.IsNullOrWhiteSpace(ProfileJsonEditor))
        {
            ProfileStatusMessage = "JSON пуст. Вставьте профиль и нажмите 'Применить JSON'.";
            return;
        }

        try
        {
            ImportProfileFromJson(ProfileJsonEditor);
        }
        catch (Exception ex)
        {
            ProfileStatusMessage = $"Ошибка JSON: {ex.Message}";
        }
    }

    private void ResetProfileJson()
    {
        ProfileJsonEditor = ActiveProfileJson;
        ProfileStatusMessage = "JSON редактор синхронизирован с активным профилем.";
    }

    private void ApplyBackgroundColor(Color color)
    {
        var updated = _graphicsProfile with
        {
            QualityPreset = RenderQualityPreset.Custom,
            Name = "Custom",
            Background = _graphicsProfile.Background with
            {
                Red = color.R / 255f,
                Green = color.G / 255f,
                Blue = color.B / 255f
            }
        };

        _selectedQualityPreset = RenderQualityPreset.Custom;
        ApplyProfile(updated, "Цвет фона обновлен.");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedQualityPreset)));
    }

    private void ApplyProfile(GraphicsProfile profile, string statusMessage)
    {
        _graphicsProfile = profile.Validate();
        _applyGraphicsProfile(_graphicsProfile);

        if (_selectedBackgroundColor != ToColor(_graphicsProfile.Background))
        {
            _selectedBackgroundColor = ToColor(_graphicsProfile.Background);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBackgroundColor)));
        }

        ProfileJsonEditor = ActiveProfileJson;
        ProfileStatusMessage = statusMessage;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveQualitySummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveProfileJson)));
    }

    private static Color ToColor(BackgroundProfile background)
    {
        return Color.FromRgb(
            (byte)Math.Clamp((int)(background.Red * 255f), 0, 255),
            (byte)Math.Clamp((int)(background.Green * 255f), 0, 255),
            (byte)Math.Clamp((int)(background.Blue * 255f), 0, 255));
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

    private void ExecuteOnRenderThread(Action action)
    {
        _renderThreadScheduler.Enqueue(action);
    }

    private void Play()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        ExecuteOnRenderThread(() =>
        {
            _scene.AnimatorComponent.PlayClip(SelectedClipName, IsLoopEnabled, (float)PlaybackSpeed);
            var state = _scene.AnimatorComponent.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
    }

    private void Pause()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        ExecuteOnRenderThread(() =>
        {
            _scene.AnimatorComponent.PauseClip(SelectedClipName);
            var state = _scene.AnimatorComponent.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
    }

    private void Stop()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        ExecuteOnRenderThread(() =>
        {
            _scene.AnimatorComponent.StopClip(SelectedClipName);
            var state = _scene.AnimatorComponent.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
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

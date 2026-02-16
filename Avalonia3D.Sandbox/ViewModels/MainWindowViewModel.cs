using Avalonia.Media;
using Avalonia.Threading;
using Avalonia3D.Animation;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Interaction.Behaviors;
using Avalonia3D.Model;
using Avalonia3D.Rendering;
using Avalonia3D.Rendering.Diagnostics;
using Avalonia3D.Sandbox.Scenes;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox;
using Avalonia3D.Sandbox.Utilities;
using Avalonia3D.Shaders;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const string PreferredStartupSceneId = "gltf:car2/scene";
    private readonly Scene3D _scene;
    private readonly CameraController _cameraController;
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private readonly Action<GraphicsProfile> _applyGraphicsProfile;
    private readonly SandboxModel3DControl _viewport;
    private readonly string _assetsRoot;
    private readonly Dictionary<string, ISandboxScene> _scenesById;
    private string _currentSceneTitle = "Сцена не выбрана";
    private ShaderRenderMode _selectedRenderMode;
    private string? _selectedClipName;
    private bool _isLoopEnabled;
    private double _playbackSpeed = 1.0;
    private ClipPlaybackState _selectedClipState;
    private RenderQualityPreset _selectedQualityPreset = RenderQualityPreset.Medium;
    private GraphicsProfile _graphicsProfile = GraphicsProfile.Medium;
    private bool _isSyncingBackgroundChannels;
    private double _backgroundRed = 15;
    private double _backgroundGreen = 15;
    private double _backgroundBlue = 20;
    private string _environmentMapPathEditor = string.Empty;
    private string _profileJsonEditor = string.Empty;
    private string _profileStatusMessage = "";
    private string _importStatusText = "Import: OK";
    private bool _isImportDegraded;
    private string _behaviorTargetSemanticId = "door.main";
    private EmissiveTextureDebugMode _selectedEmissiveTextureDebugMode = EmissiveTextureDebugMode.Normal;
    private PbrDebugViewMode _selectedPbrDebugViewMode = PbrDebugViewMode.None;
    private bool _dumpPbrMaterialDiagnostics;
    private bool _isLoading;
    private bool _isRendererReady;
    private string? _lastLoadError;
    private string? _selectedSceneId;
    private readonly HashSet<string> _loadedSceneIds = new(StringComparer.OrdinalIgnoreCase);
    private string _cameraPreviewText = "camera: n/a";

    public MainWindowViewModel(Scene3D scene, CameraController cameraController, string assetsRoot, IRenderThreadScheduler renderThreadScheduler, Action<GraphicsProfile> applyGraphicsProfile, SandboxModel3DControl viewport)
    {
        _viewport = viewport;
        _scene = scene;
        _cameraController = cameraController;
        _renderThreadScheduler = renderThreadScheduler;
        _applyGraphicsProfile = applyGraphicsProfile;
        _assetsRoot = assetsRoot;
        _viewport.SceneLoaded += sceneInfo =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsLoading = false;
                LastLoadError = null;
                IsRendererReady = _viewport.IsRendererReady;
                _loadedSceneIds.Add(sceneInfo.Id);
                SelectedSceneId = sceneInfo.Id;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CacheStatusText)));
                CurrentSceneTitle = sceneInfo.Title;
                UpdateImportStatus();
                RefreshClips();
                AutoPlayFirstClipIfAvailable();
                UpdateCameraPreview();
                ExecuteOnRenderThread(() => _cameraController.CaptureHomeView());
            });
        };

        var scenes = SceneCatalog.CreateDefault(assetsRoot);
        _scenesById = scenes.ToDictionary(scene => scene.Id, scene => scene, StringComparer.OrdinalIgnoreCase);
        Scenes = new ObservableCollection<SceneItemViewModel>(
            scenes.Select(sceneInfo =>
                new SceneItemViewModel(
                    sceneInfo.Id,
                    sceneInfo.Title,
                    sceneInfo.Description,
                    sceneInfo is GltfFileScene gltfScene ? gltfScene.FileName : sceneInfo.Title,
                    sceneInfo is GltfFileScene gltfSceneDir ? gltfSceneDir.Directory : "n/a",
                    sceneInfo is GltfFileScene gltfSceneExt ? gltfSceneExt.Extension : "internal",
                    new RelayCommand(_ => LoadScene(sceneInfo.Id)))));

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
            RenderQualityPreset.Ultra,
            RenderQualityPreset.PbrDebugNeutral,
            RenderQualityPreset.Custom
        });

        EmissiveTextureDebugModes = new ObservableCollection<EmissiveTextureDebugMode>(new[]
        {
            EmissiveTextureDebugMode.Normal,
            EmissiveTextureDebugMode.IgnoreTexture,
            EmissiveTextureDebugMode.ForceWhite
        });

        PbrDebugViewModes = new ObservableCollection<PbrDebugViewMode>(new[]
        {
            PbrDebugViewMode.None,
            PbrDebugViewMode.BaseColorOnly,
            PbrDebugViewMode.DirectLightOnly,
            PbrDebugViewMode.IblOnly,
            PbrDebugViewMode.EmissiveOnly,
            PbrDebugViewMode.AoOnly,
            PbrDebugViewMode.NormalsOnly
        });

        SwitchShaderModeCommand = new RelayCommand(mode =>
        {
            if (mode is ShaderRenderMode renderMode)
            {
                ApplyShaderMode(renderMode);
            }
        });

        TogglePbrUnlitCommand = new RelayCommand(_ =>
        {
            var targetMode = SelectedRenderMode == ShaderRenderMode.Pbr
                ? ShaderRenderMode.Unlit
                : ShaderRenderMode.Pbr;
            ApplyShaderMode(targetMode);
        });

        PlayClipCommand = new RelayCommand(_ => Play());
        PauseClipCommand = new RelayCommand(_ => Pause());
        StopClipCommand = new RelayCommand(_ => Stop());
        TogglePlayPauseCommand = new RelayCommand(_ => TogglePlayPause());

        OpenSemanticTargetCommand = new RelayCommand(_ => DispatchBehaviorCommand(SceneCommandAction.Open));
        CloseSemanticTargetCommand = new RelayCommand(_ => DispatchBehaviorCommand(SceneCommandAction.Close));
        ToggleSemanticTargetCommand = new RelayCommand(_ => DispatchBehaviorCommand(SceneCommandAction.Toggle));

        FrameAllCommand = new RelayCommand(_ => ExecuteOnRenderThread(() => _cameraController.FrameAll()));
        ResetViewCommand = new RelayCommand(_ => ExecuteOnRenderThread(() => _cameraController.ResetView()));
        ToggleCameraModeCommand = new RelayCommand(_ =>
        {
            ExecuteOnRenderThread(() => _cameraController.ToggleControlMode());
            Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCameraMode))));
        });

        ApplyProfileJsonCommand = new RelayCommand(_ => ApplyProfileJson());
        ResetProfileJsonCommand = new RelayCommand(_ => ResetProfileJson());
        ApplyEnvironmentMapPathCommand = new RelayCommand(_ => ApplyEnvironmentMapPath());
        LoadSceneCommand = new RelayCommand(sceneId => LoadScene(sceneId as string ?? SelectedSceneId));

        ApplyRenderQualityPreset(RenderQualityPreset.Medium);

        _scene.BindRenderMode(ShaderRenderMode.Pbr, ShaderIds.Pbr);
        _scene.BindRenderMode(ShaderRenderMode.Unlit, ShaderIds.Unlit);
        _scene.BindRenderMode(ShaderRenderMode.NormalsDebug, ShaderIds.NormalsDebug);
        ApplyShaderMode(ShaderRenderMode.Pbr);
        EmissionUniformResolver.EmissiveTextureMode = _selectedEmissiveTextureDebugMode;
        _scene.PbrDebugViewMode = _selectedPbrDebugViewMode;

        IsRendererReady = _viewport.IsRendererReady;

        if (scenes.Count > 0)
        {
            var startupSceneId = _scenesById.ContainsKey(PreferredStartupSceneId)
                ? PreferredStartupSceneId
                : scenes[0].Id;

            SelectedSceneId = startupSceneId;
            LoadScene(startupSceneId);
        }
    }

    public ObservableCollection<SceneItemViewModel> Scenes { get; }
    public ObservableCollection<ShaderRenderMode> ShaderModes { get; }
    public ObservableCollection<string> AvailableClips { get; }
    public ObservableCollection<RenderQualityPreset> QualityPresets { get; }
    public ObservableCollection<EmissiveTextureDebugMode> EmissiveTextureDebugModes { get; }
    public ObservableCollection<PbrDebugViewMode> PbrDebugViewModes { get; }
    public RelayCommand SwitchShaderModeCommand { get; }
    public RelayCommand TogglePbrUnlitCommand { get; }
    public RelayCommand PlayClipCommand { get; }
    public RelayCommand PauseClipCommand { get; }
    public RelayCommand StopClipCommand { get; }
    public RelayCommand TogglePlayPauseCommand { get; }
    public RelayCommand OpenSemanticTargetCommand { get; }
    public RelayCommand CloseSemanticTargetCommand { get; }
    public RelayCommand ToggleSemanticTargetCommand { get; }
    public RelayCommand FrameAllCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand ToggleCameraModeCommand { get; }
    public RelayCommand ApplyProfileJsonCommand { get; }
    public RelayCommand ResetProfileJsonCommand { get; }
    public RelayCommand ApplyEnvironmentMapPathCommand { get; }
    public RelayCommand LoadSceneCommand { get; }

    public string CurrentCameraMode => _cameraController.ControlMode.ToString();


    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadStatusText)));
        }
    }

    public bool IsRendererReady
    {
        get => _isRendererReady;
        private set
        {
            if (_isRendererReady == value)
            {
                return;
            }

            _isRendererReady = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRendererReady)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadStatusText)));
        }
    }

    public string? LastLoadError
    {
        get => _lastLoadError;
        private set
        {
            if (_lastLoadError == value)
            {
                return;
            }

            _lastLoadError = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastLoadError)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadStatusText)));
        }
    }

    public string? SelectedSceneId
    {
        get => _selectedSceneId;
        set
        {
            if (_selectedSceneId == value)
            {
                return;
            }

            _selectedSceneId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSceneId)));
        }
    }

    public string LoadStatusText =>
        LastLoadError is not null ? $"Ошибка загрузки: {LastLoadError}" :
        IsLoading ? "Загрузка сцены..." :
        IsRendererReady ? "Renderer готов" : "Renderer не готов";

    public string CacheStatusText => $"Cache: загружено {_loadedSceneIds.Count} сцен(ы) | source: {_assetsRoot}";

    public string CameraPreviewText => _cameraPreviewText;


    public string BehaviorTargetSemanticId
    {
        get => _behaviorTargetSemanticId;
        set
        {
            if (_behaviorTargetSemanticId == value)
            {
                return;
            }

            _behaviorTargetSemanticId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BehaviorTargetSemanticId)));
        }
    }

    public bool IsImportDegraded
    {
        get => _isImportDegraded;
        private set
        {
            if (_isImportDegraded == value)
            {
                return;
            }

            _isImportDegraded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsImportDegraded)));
        }
    }

    public string ImportStatusText
    {
        get => _importStatusText;
        private set
        {
            if (_importStatusText == value)
            {
                return;
            }

            _importStatusText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportStatusText)));
        }
    }

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

    public bool ReflectionsEnabled
    {
        get => _graphicsProfile.Reflections.Enabled;
        set
        {
            if (_graphicsProfile.Reflections.Enabled == value)
            {
                return;
            }

            ApplyGraphicsProfileOverride(
                profile => profile with
                {
                    Reflections = profile.Reflections with
                    {
                        Enabled = value,
                        Mode = value
                            ? (profile.Reflections.Mode == ReflectionMode.Off ? ReflectionMode.IBL : profile.Reflections.Mode)
                            : ReflectionMode.Off
                    }
                },
                value ? "Отражения включены." : "Отражения выключены.");
        }
    }

    public double ReflectionIntensity
    {
        get => _graphicsProfile.Reflections.Intensity;
        set => ApplyGraphicsProfileOverride(
            profile => profile with { Reflections = profile.Reflections with { Intensity = (float)Math.Clamp(value, 0d, 2d) } },
            $"Интенсивность отражений: {value:0.00}");
    }

    public double IblSpecularIntensity
    {
        get => _graphicsProfile.PbrTuning.IblSpecularIntensity;
        set => ApplyGraphicsProfileOverride(
            profile => profile with { PbrTuning = profile.PbrTuning with { IblSpecularIntensity = (float)Math.Clamp(value, 0d, 8d) } },
            $"Зеркальный IBL: {value:0.00}");
    }

    public double IblDiffuseIntensity
    {
        get => _graphicsProfile.PbrTuning.IblDiffuseIntensity;
        set => ApplyGraphicsProfileOverride(
            profile => profile with { PbrTuning = profile.PbrTuning with { IblDiffuseIntensity = (float)Math.Clamp(value, 0d, 4d) } },
            $"Диффузный IBL: {value:0.00}");
    }

    public double ReflectionClamp
    {
        get => _graphicsProfile.PbrTuning.ReflectionContributionClamp;
        set => ApplyGraphicsProfileOverride(
            profile => profile with { PbrTuning = profile.PbrTuning with { ReflectionContributionClamp = (float)Math.Clamp(value, 0d, 8d) } },
            $"Кламп отражений: {value:0.00}");
    }

    public double Exposure
    {
        get => _graphicsProfile.PbrTuning.Exposure;
        set => ApplyGraphicsProfileOverride(
            profile => profile with { PbrTuning = profile.PbrTuning with { Exposure = (float)Math.Clamp(value, 0.1d, 8d) } },
            $"Экспозиция: {value:0.00}");
    }

    public double WhitePoint
    {
        get => _graphicsProfile.PbrTuning.PbrWhitePoint;
        set => ApplyGraphicsProfileOverride(
            profile => profile with { PbrTuning = profile.PbrTuning with { PbrWhitePoint = (float)Math.Clamp(value, 0.5d, 16d) } },
            $"Белая точка: {value:0.00}");
    }

    public string EnvironmentMapPathEditor
    {
        get => _environmentMapPathEditor;
        set
        {
            if (_environmentMapPathEditor == value)
            {
                return;
            }

            _environmentMapPathEditor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnvironmentMapPathEditor)));
        }
    }

    public double BackgroundRed
    {
        get => _backgroundRed;
        set => SetBackgroundChannel(nameof(BackgroundRed), ref _backgroundRed, value);
    }

    public double BackgroundGreen
    {
        get => _backgroundGreen;
        set => SetBackgroundChannel(nameof(BackgroundGreen), ref _backgroundGreen, value);
    }

    public double BackgroundBlue
    {
        get => _backgroundBlue;
        set => SetBackgroundChannel(nameof(BackgroundBlue), ref _backgroundBlue, value);
    }

    public SolidColorBrush BackgroundPreviewBrush => new(Color.FromRgb((byte)_backgroundRed, (byte)_backgroundGreen, (byte)_backgroundBlue));

    public string BackgroundHex => $"#{(byte)_backgroundRed:X2}{(byte)_backgroundGreen:X2}{(byte)_backgroundBlue:X2}";

    public string GraphicsTuningHint =>
        "Подсказка: отражения сильнее всего зависят от зеркального IBL, клампа отражений и экспозиции. " +
        "Если картинка плоская, сначала поднимайте интенсивность отражений и зеркальный IBL; если слишком ярко — снижайте экспозицию или кламп.";

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


    public EmissiveTextureDebugMode SelectedEmissiveTextureDebugMode
    {
        get => _selectedEmissiveTextureDebugMode;
        set
        {
            if (_selectedEmissiveTextureDebugMode == value)
            {
                return;
            }

            _selectedEmissiveTextureDebugMode = value;
            EmissionUniformResolver.EmissiveTextureMode = value;
            Log.Information("Emissive texture debug mode changed: {Mode}", value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedEmissiveTextureDebugMode)));
        }
    }


    public PbrDebugViewMode SelectedPbrDebugViewMode
    {
        get => _selectedPbrDebugViewMode;
        set
        {
            if (_selectedPbrDebugViewMode == value)
            {
                return;
            }

            _selectedPbrDebugViewMode = value;
            _scene.PbrDebugViewMode = value;
            Log.Information("PBR debug view mode changed: {Mode}", value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPbrDebugViewMode)));
        }
    }

    public bool DumpPbrMaterialDiagnostics
    {
        get => _dumpPbrMaterialDiagnostics;
        set
        {
            if (_dumpPbrMaterialDiagnostics == value)
            {
                return;
            }

            _dumpPbrMaterialDiagnostics = value;
            MaterialRenderDiagnostics.SetEnabled(value);
            Log.Information("PBR material diagnostics dump changed: {Enabled}", value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DumpPbrMaterialDiagnostics)));
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


    public event PropertyChangedEventHandler? PropertyChanged;

    private void LoadScene(string? sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId) || !_scenesById.TryGetValue(sceneId, out var scene))
        {
            LastLoadError = $"Scene '{sceneId}' not found";
            return;
        }

        LastLoadError = null;
        IsLoading = true;
        SelectedSceneId = sceneId;

        _viewport.SelectedSceneId = sceneId;

        if (!IsRendererReady)
        {
            IsRendererReady = _viewport.IsRendererReady;
        }
    }

    private void UpdateCameraPreview()
    {
        var pos = _scene.Camera.Position;
        var target = _scene.Camera.Target;
        _cameraPreviewText = $"pos=({pos.X:0.00}, {pos.Y:0.00}, {pos.Z:0.00}) | target=({target.X:0.00}, {target.Y:0.00}, {target.Z:0.00})";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraPreviewText)));
    }


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

    private void ApplyBackgroundChannels()
    {
        if (_isSyncingBackgroundChannels)
        {
            return;
        }

        var updated = _graphicsProfile with
        {
            QualityPreset = RenderQualityPreset.Custom,
            Name = "Custom",
            Background = _graphicsProfile.Background with
            {
                Red = (float)_backgroundRed / 255f,
                Green = (float)_backgroundGreen / 255f,
                Blue = (float)_backgroundBlue / 255f
            }
        };

        _selectedQualityPreset = RenderQualityPreset.Custom;
        ApplyProfile(updated, "Цвет фона обновлен.");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedQualityPreset)));
    }

    private void SetBackgroundChannel(string propertyName, ref double channel, double value)
    {
        var clamped = Math.Clamp(Math.Round(value), 0d, 255d);
        if (Math.Abs(channel - clamped) < 0.1)
        {
            return;
        }

        channel = clamped;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundPreviewBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundHex)));
        ApplyBackgroundChannels();
    }

    private void ApplyProfile(GraphicsProfile profile, string statusMessage)
    {
        _graphicsProfile = profile.Validate();
        _applyGraphicsProfile(_graphicsProfile);

        SyncBackgroundChannelsFromProfile(_graphicsProfile.Background);
        _environmentMapPathEditor = _graphicsProfile.Reflections.EnvironmentMapPath ?? string.Empty;

        ProfileJsonEditor = ActiveProfileJson;
        ProfileStatusMessage = statusMessage;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveQualitySummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveProfileJson)));
        NotifyGraphicsControlsChanged();
    }

    private void ApplyEnvironmentMapPath()
    {
        var normalized = string.IsNullOrWhiteSpace(EnvironmentMapPathEditor)
            ? GraphicsProfile.DefaultEnvironmentMapPath
            : EnvironmentMapPathEditor.Trim();

        ApplyGraphicsProfileOverride(
            profile => profile with { Reflections = profile.Reflections with { EnvironmentMapPath = normalized } },
            $"Карта окружения: {normalized}");
    }

    private void ApplyGraphicsProfileOverride(Func<GraphicsProfile, GraphicsProfile> mutator, string statusMessage)
    {
        var candidate = mutator(_graphicsProfile) with
        {
            QualityPreset = RenderQualityPreset.Custom,
            Name = "Custom"
        };

        var validatedCandidate = candidate.Validate();

        _selectedQualityPreset = RenderQualityPreset.Custom;
        ApplyProfile(validatedCandidate, statusMessage);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedQualityPreset)));
    }

    private void NotifyGraphicsControlsChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReflectionsEnabled)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReflectionIntensity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IblSpecularIntensity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IblDiffuseIntensity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReflectionClamp)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Exposure)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WhitePoint)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnvironmentMapPathEditor)));
    }

    private void SyncBackgroundChannelsFromProfile(BackgroundProfile background)
    {
        _isSyncingBackgroundChannels = true;

        try
        {
            var red = Math.Clamp((int)(background.Red * 255f), 0, 255);
            var green = Math.Clamp((int)(background.Green * 255f), 0, 255);
            var blue = Math.Clamp((int)(background.Blue * 255f), 0, 255);

            if (Math.Abs(_backgroundRed - red) > 0.1)
            {
                _backgroundRed = red;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundRed)));
            }

            if (Math.Abs(_backgroundGreen - green) > 0.1)
            {
                _backgroundGreen = green;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundGreen)));
            }

            if (Math.Abs(_backgroundBlue - blue) > 0.1)
            {
                _backgroundBlue = blue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBlue)));
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundPreviewBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundHex)));
        }
        finally
        {
            _isSyncingBackgroundChannels = false;
        }
    }

    private void RefreshClips()
    {
        AvailableClips.Clear();
        foreach (var clip in _scene.AnimatorComponent.GetClipNames())
        {
            AvailableClips.Add(clip);
        }

        SelectedClipName = AvailableClips.FirstOrDefault(c => string.Equals(c, "Start_Liftoff", StringComparison.Ordinal))
            ?? AvailableClips.FirstOrDefault();
    }


    private void AutoPlayFirstClipIfAvailable()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        ExecuteOnRenderThread(() =>
        {
            var started = _scene.AnimatorComponent.PlayClip(SelectedClipName, loop: true, speed: (float)PlaybackSpeed);
            Log.Information("AutoPlay clip attempt. Clip={Clip}, Started={Started}", SelectedClipName, started);

            if (!started)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Dispatcher.UIThread.Post(() => ExecuteOnRenderThread(() =>
                    {
                        var retryStarted = _scene.AnimatorComponent.PlayClip(SelectedClipName, loop: true, speed: (float)PlaybackSpeed);
                        Log.Information("AutoPlay clip retry. Clip={Clip}, Started={Started}", SelectedClipName, retryStarted);
                        if (!retryStarted)
                        {
                            return;
                        }

                        var retryState = _scene.AnimatorComponent.GetClipState(SelectedClipName);
                        Dispatcher.UIThread.Post(() => SelectedClipState = retryState);
                    }), DispatcherPriority.Background);
                });
                return;
            }

            var state = _scene.AnimatorComponent.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
    }

    private void UpdateSelectedClipState()
    {
        SelectedClipState = string.IsNullOrWhiteSpace(SelectedClipName)
            ? default
            : _scene.AnimatorComponent.GetClipState(SelectedClipName);
    }

    private void UpdateImportStatus()
    {
        var report = _scene.LastImportReport;
        IsImportDegraded = report.IsDegraded;

        if (!report.IsDegraded)
        {
            var unsupportedSuffix = report.UnsupportedAnimationChannels.Count == 0
                ? string.Empty
                : $" | Unsupported animation channels: {report.UnsupportedAnimationChannels.Count}";

            ImportStatusText = $"Import: OK{unsupportedSuffix}";
            return;
        }

        var issuesPreview = report.Issues.Count == 0
            ? "validation issues"
            : string.Join("; ", report.Issues.Take(3));

        ImportStatusText = $"Import: DEGRADED ({issuesPreview})";
    }

    private void ExecuteOnRenderThread(Action action)
    {
        _renderThreadScheduler.Enqueue(action);
    }


    private void DispatchBehaviorCommand(SceneCommandAction action)
    {
        if (string.IsNullOrWhiteSpace(BehaviorTargetSemanticId))
        {
            return;
        }

        ExecuteOnRenderThread(() =>
        {
            _scene.DispatchCommand(new SceneCommand(BehaviorTargetSemanticId, action));
        });
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

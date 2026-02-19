using Avalonia.Threading;
using Avalonia3D.Interaction.CameraController;
using Avalonia3D.Rendering;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia3D.Model;
using Avalonia3D.Sandbox;
using System;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const string PreferredStartupSceneId = "gltf:car2/scene";

    private readonly Scene3D _scene;
    private readonly CameraController _cameraController;
    private readonly SandboxModel3DControl _viewport;
    private string _importStatusText = "Import: OK";
    private bool _isImportDegraded;
    private string _cameraPreviewText = "camera: n/a";

    public MainWindowViewModel(Scene3D scene, CameraController cameraController, string assetsRoot, IRenderThreadScheduler renderThreadScheduler, Action<GraphicsProfile> applyGraphicsProfile, SandboxModel3DControl viewport)
    {
        _scene = scene;
        _cameraController = cameraController;
        _viewport = viewport;

        var sceneRuntimeController = new SceneRuntimeController(assetsRoot);
        var animationRuntimeController = new AnimationRuntimeController(scene);

        SceneSelection = new SceneSelectionViewModel(sceneRuntimeController, assetsRoot, RequestSceneLoad);
        GraphicsSettings = new GraphicsSettingsViewModel(applyGraphicsProfile);
        AnimationPanel = new AnimationPanelViewModel(animationRuntimeController, renderThreadScheduler);
        Car2Control = new Car2ControlViewModel(animationRuntimeController, renderThreadScheduler);

        SubscribeChild(SceneSelection);
        SubscribeChild(GraphicsSettings);
        SubscribeChild(AnimationPanel);
        SubscribeChild(Car2Control);

        FrameAllCommand = new RelayCommand(_ => ExecuteOnRenderThread(() => _cameraController.FrameAll()));
        ResetViewCommand = new RelayCommand(_ => ExecuteOnRenderThread(() => _cameraController.ResetView()));
        ToggleCameraModeCommand = new RelayCommand(_ =>
        {
            ExecuteOnRenderThread(() => _cameraController.ToggleControlMode());
            Dispatcher.UIThread.Post(() => RaisePropertyChanged(nameof(CurrentCameraMode)));
        });

        _viewport.SceneLoaded += info => Dispatcher.UIThread.Post(() =>
        {
            SceneSelection.IsRendererReady = _viewport.IsRendererReady;
            SceneSelection.MarkSceneLoaded(info.Id, info.Title);
            Car2Control.SetSelectedScene(info.Id);
            UpdateImportStatus();
            AnimationPanel.RefreshClips();
            UpdateCameraPreview();
            ExecuteOnRenderThread(() => _cameraController.CaptureHomeView());
            RaisePropertyChanged(nameof(CurrentSceneTitle));
            RaisePropertyChanged(nameof(ImportStatusText));
        });

        SceneSelection.IsRendererReady = _viewport.IsRendererReady;

        var startup = SceneSelection.ResolveStartupSceneId(PreferredStartupSceneId);
        if (!string.IsNullOrWhiteSpace(startup))
        {
            SceneSelection.SelectedSceneId = startup;
            SceneSelection.LoadSceneCommand.Execute(startup);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SceneSelectionViewModel SceneSelection { get; }
    public GraphicsSettingsViewModel GraphicsSettings { get; }
    public AnimationPanelViewModel AnimationPanel { get; }
    public Car2ControlViewModel Car2Control { get; }

    public RelayCommand FrameAllCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand ToggleCameraModeCommand { get; }

    // proxy for existing XAML bindings
    public ObservableCollection<SceneItemViewModel> Scenes => SceneSelection.Scenes;
    public string LoadStatusText => SceneSelection.LoadStatusText;
    public bool IsLoading => SceneSelection.IsLoading;
    public string CacheStatusText => SceneSelection.CacheStatusText;
    public string CurrentSceneTitle => SceneSelection.CurrentSceneTitle;
    public string ImportStatusText => _importStatusText;
    public string ActiveQualitySummary => GraphicsSettings.ActiveQualitySummary;

    public ObservableCollection<RenderQualityPreset> QualityPresets => GraphicsSettings.QualityPresets;
    public RenderQualityPreset SelectedQualityPreset { get => GraphicsSettings.SelectedQualityPreset; set => GraphicsSettings.SelectedQualityPreset = value; }
    public bool ReflectionsEnabled { get => GraphicsSettings.ReflectionsEnabled; set => GraphicsSettings.ReflectionsEnabled = value; }
    public double ReflectionIntensity { get => GraphicsSettings.ReflectionIntensity; set => GraphicsSettings.ReflectionIntensity = value; }
    public double IblSpecularIntensity { get => GraphicsSettings.IblSpecularIntensity; set => GraphicsSettings.IblSpecularIntensity = value; }
    public double IblDiffuseIntensity { get => GraphicsSettings.IblDiffuseIntensity; set => GraphicsSettings.IblDiffuseIntensity = value; }
    public double ReflectionClamp { get => GraphicsSettings.ReflectionClamp; set => GraphicsSettings.ReflectionClamp = value; }
    public double Exposure { get => GraphicsSettings.Exposure; set => GraphicsSettings.Exposure = value; }
    public double WhitePoint { get => GraphicsSettings.WhitePoint; set => GraphicsSettings.WhitePoint = value; }
    public string EnvironmentMapPathEditor { get => GraphicsSettings.EnvironmentMapPathEditor; set => GraphicsSettings.EnvironmentMapPathEditor = value; }
    public RelayCommand ApplyEnvironmentMapPathCommand => GraphicsSettings.ApplyEnvironmentMapPathCommand;
    public string ProfileStatusMessage => GraphicsSettings.ProfileStatusMessage;
    public string GraphicsTuningHint => GraphicsSettings.GraphicsTuningHint;

    public ObservableCollection<string> AvailableClips => AnimationPanel.AvailableClips;
    public string? SelectedClipName { get => AnimationPanel.SelectedClipName; set => AnimationPanel.SelectedClipName = value; }
    public RelayCommand PlayClipCommand => AnimationPanel.PlayClipCommand;
    public RelayCommand PauseClipCommand => AnimationPanel.PauseClipCommand;
    public RelayCommand StopClipCommand => AnimationPanel.StopClipCommand;
    public RelayCommand TogglePlayPauseCommand => AnimationPanel.TogglePlayPauseCommand;
    public bool IsLoopEnabled { get => AnimationPanel.IsLoopEnabled; set => AnimationPanel.IsLoopEnabled = value; }
    public double PlaybackSpeed { get => AnimationPanel.PlaybackSpeed; set => AnimationPanel.PlaybackSpeed = value; }
    public string ClipStateText => AnimationPanel.ClipStateText;

    public bool IsCar2AnimatorAvailable => Car2Control.IsCar2AnimatorAvailable;
    public RelayCommand Car2RotateWheelsForwardCommand => Car2Control.Car2RotateWheelsForwardCommand;
    public RelayCommand Car2RotateWheelsBackwardCommand => Car2Control.Car2RotateWheelsBackwardCommand;
    public RelayCommand Car2RotateWheelsQuarterForwardCommand => Car2Control.Car2RotateWheelsQuarterForwardCommand;
    public RelayCommand Car2RotateWheelsQuarterBackwardCommand => Car2Control.Car2RotateWheelsQuarterBackwardCommand;
    public RelayCommand Car2RotateWheelsFullForwardCommand => Car2Control.Car2RotateWheelsFullForwardCommand;
    public RelayCommand Car2RotateWheelsFullBackwardCommand => Car2Control.Car2RotateWheelsFullBackwardCommand;
    public RelayCommand Car2StartWheelAutoSpinForwardCommand => Car2Control.Car2StartWheelAutoSpinForwardCommand;
    public RelayCommand Car2StartWheelAutoSpinBackwardCommand => Car2Control.Car2StartWheelAutoSpinBackwardCommand;
    public RelayCommand Car2StopWheelAutoSpinCommand => Car2Control.Car2StopWheelAutoSpinCommand;
    public RelayCommand Car2MoveForwardCommand => Car2Control.Car2MoveForwardCommand;
    public RelayCommand Car2MoveBackwardCommand => Car2Control.Car2MoveBackwardCommand;
    public RelayCommand Car2TurnLeftCommand => Car2Control.Car2TurnLeftCommand;
    public RelayCommand Car2TurnRightCommand => Car2Control.Car2TurnRightCommand;
    public RelayCommand Car2ResetPoseCommand => Car2Control.Car2ResetPoseCommand;
    public string Car2WheelAutoSpinState => Car2Control.Car2WheelAutoSpinState;
    public string Car2AnimatorStatus => Car2Control.Car2AnimatorStatus;

    public string CurrentCameraMode => _cameraController.ControlMode.ToString();
    public string CameraPreviewText => _cameraPreviewText;

    public double OrbitSensitivity
    {
        get => _cameraController.OrbitSensitivity;
        set
        {
            _cameraController.OrbitSensitivity = (float)value;
            RaisePropertyChanged(nameof(OrbitSensitivity));
        }
    }

    public double PanSensitivity
    {
        get => _cameraController.PanSensitivity;
        set
        {
            _cameraController.PanSensitivity = (float)value;
            RaisePropertyChanged(nameof(PanSensitivity));
        }
    }

    public double DollySensitivity
    {
        get => _cameraController.DollySensitivity;
        set
        {
            _cameraController.DollySensitivity = (float)value;
            RaisePropertyChanged(nameof(DollySensitivity));
        }
    }

    private void RequestSceneLoad(string sceneId)
    {
        _viewport.SelectedSceneId = sceneId;
        if (!SceneSelection.IsRendererReady)
        {
            SceneSelection.IsRendererReady = _viewport.IsRendererReady;
        }
    }

    private void UpdateCameraPreview()
    {
        var pos = _scene.Camera.Position;
        var target = _scene.Camera.Target;
        _cameraPreviewText = $"pos=({pos.X:0.00}, {pos.Y:0.00}, {pos.Z:0.00}) | target=({target.X:0.00}, {target.Y:0.00}, {target.Z:0.00})";
        RaisePropertyChanged(nameof(CameraPreviewText));
    }

    private void UpdateImportStatus()
    {
        var report = _scene.LastImportReport;
        _isImportDegraded = report.IsDegraded;
        _importStatusText = _isImportDegraded
            ? $"Import: degraded ({string.Join(" | ", report.Issues)})"
            : "Import: OK";
    }

    private static void ExecuteOnRenderThread(Action action)
    {
        action();
    }

    private void SubscribeChild(INotifyPropertyChanged child)
    {
        child.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.PropertyName))
            {
                return;
            }

            RaisePropertyChanged(args.PropertyName);
            if (ReferenceEquals(child, Car2Control) && args.PropertyName is nameof(Car2ControlViewModel.IsCar2AnimatorAvailable))
            {
                RaisePropertyChanged(nameof(IsCar2AnimatorAvailable));
            }
        };
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

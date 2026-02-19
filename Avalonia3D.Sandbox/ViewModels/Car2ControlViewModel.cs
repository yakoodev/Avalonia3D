using Avalonia.Threading;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox.Utilities;
using System.Numerics;
using System;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class Car2ControlViewModel : BindableBase
{
    private const string Car2SceneId = "gltf:car2/scene";
    private const float WheelStepRadians = 1.20f;
    private const float WheelQuarterTurnRadians = MathF.PI / 2f;
    private const float WheelFullTurnRadians = MathF.PI * 2f;
    private const float WheelAutoSpinRadiansPerSecond = 10f;
    private const float MoveStep = 0.35f;
    private const float TurnStepRadians = MathF.PI / 24f;

    private readonly IAnimationRuntimeController _animationRuntimeController;
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private readonly DispatcherTimer _wheelAutoSpinTimer;
    private bool _wheelAutoSpinEnabled;
    private float _wheelAutoSpinDirection = 1f;
    private string? _selectedSceneId;
    private string _car2AnimatorStatus = "Car2 animator: load scene 'gltf:car2/scene'.";

    public Car2ControlViewModel(IAnimationRuntimeController animationRuntimeController, IRenderThreadScheduler renderThreadScheduler)
    {
        _animationRuntimeController = animationRuntimeController;
        _renderThreadScheduler = renderThreadScheduler;

        Car2RotateWheelsForwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => RotateWheels(WheelStepRadians)));
        Car2RotateWheelsBackwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => RotateWheels(-WheelStepRadians)));
        Car2RotateWheelsQuarterForwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => RotateWheels(WheelQuarterTurnRadians)));
        Car2RotateWheelsQuarterBackwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => RotateWheels(-WheelQuarterTurnRadians)));
        Car2RotateWheelsFullForwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => RotateWheels(WheelFullTurnRadians)));
        Car2RotateWheelsFullBackwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => RotateWheels(-WheelFullTurnRadians)));
        Car2StartWheelAutoSpinForwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => SetWheelAutoSpin(true, 1f)));
        Car2StartWheelAutoSpinBackwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => SetWheelAutoSpin(true, -1f)));
        Car2StopWheelAutoSpinCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => SetWheelAutoSpin(false, _wheelAutoSpinDirection)));
        Car2MoveForwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => MoveCar2(new Vector3(MoveStep, 0f, 0f))));
        Car2MoveBackwardCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => MoveCar2(new Vector3(-MoveStep, 0f, 0f))));
        Car2TurnLeftCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => TurnCar2(TurnStepRadians)));
        Car2TurnRightCommand = new RelayCommand(_ => ExecuteAnimatorAction(() => TurnCar2(-TurnStepRadians)));
        Car2ResetPoseCommand = new RelayCommand(_ => ExecuteAnimatorAction(ResetCar2Pose));

        _wheelAutoSpinTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, OnWheelAutoSpinTick);
        _wheelAutoSpinTimer.Start();
    }

    public RelayCommand Car2RotateWheelsForwardCommand { get; }
    public RelayCommand Car2RotateWheelsBackwardCommand { get; }
    public RelayCommand Car2RotateWheelsQuarterForwardCommand { get; }
    public RelayCommand Car2RotateWheelsQuarterBackwardCommand { get; }
    public RelayCommand Car2RotateWheelsFullForwardCommand { get; }
    public RelayCommand Car2RotateWheelsFullBackwardCommand { get; }
    public RelayCommand Car2StartWheelAutoSpinForwardCommand { get; }
    public RelayCommand Car2StartWheelAutoSpinBackwardCommand { get; }
    public RelayCommand Car2StopWheelAutoSpinCommand { get; }
    public RelayCommand Car2MoveForwardCommand { get; }
    public RelayCommand Car2MoveBackwardCommand { get; }
    public RelayCommand Car2TurnLeftCommand { get; }
    public RelayCommand Car2TurnRightCommand { get; }
    public RelayCommand Car2ResetPoseCommand { get; }

    public bool IsCar2AnimatorAvailable => string.Equals(_selectedSceneId, Car2SceneId, StringComparison.OrdinalIgnoreCase);

    public string Car2WheelAutoSpinState => _wheelAutoSpinEnabled
        ? (_wheelAutoSpinDirection > 0 ? "Auto spin: ON (forward)" : "Auto spin: ON (reverse)")
        : "Auto spin: OFF";

    public string Car2AnimatorStatus
    {
        get => _car2AnimatorStatus;
        private set => SetProperty(ref _car2AnimatorStatus, value);
    }

    public void SetSelectedScene(string? sceneId)
    {
        _selectedSceneId = sceneId;
        RaisePropertyChanged(nameof(IsCar2AnimatorAvailable));

        if (!IsCar2AnimatorAvailable)
        {
            _wheelAutoSpinEnabled = false;
            Car2AnimatorStatus = "Car2 animator: load scene 'gltf:car2/scene'.";
            RaisePropertyChanged(nameof(Car2WheelAutoSpinState));
            return;
        }

        _renderThreadScheduler.Enqueue(() => _animationRuntimeController.CaptureCar2Pose());
    }

    private void ExecuteAnimatorAction(Func<string> action)
    {
        if (!IsCar2AnimatorAvailable)
        {
            Car2AnimatorStatus = "Car2 animator доступен только для сцены gltf:car2/scene.";
            return;
        }

        _renderThreadScheduler.Enqueue(() =>
        {
            var status = action();
            Dispatcher.UIThread.Post(() => Car2AnimatorStatus = status);
        });
    }

    private string RotateWheels(float radians)
    {
        var count = _animationRuntimeController.RotateCar2Wheels(radians);
        return count == 0
            ? "Car2 animator: wheel nodes were not found."
            : $"Car2 animator: rotated {count} wheel nodes by {radians:0.00} rad.";
    }

    private string SetWheelAutoSpin(bool enabled, float direction)
    {
        _wheelAutoSpinEnabled = enabled;
        _wheelAutoSpinDirection = direction >= 0f ? 1f : -1f;
        RaisePropertyChanged(nameof(Car2WheelAutoSpinState));

        if (!_wheelAutoSpinEnabled)
        {
            return "Car2 animator: wheel auto spin stopped.";
        }

        var label = _wheelAutoSpinDirection > 0f ? "forward" : "reverse";
        return $"Car2 animator: wheel auto spin started ({label}).";
    }

    private void OnWheelAutoSpinTick(object? sender, EventArgs e)
    {
        if (!_wheelAutoSpinEnabled || !IsCar2AnimatorAvailable)
        {
            return;
        }

        _renderThreadScheduler.Enqueue(() =>
        {
            var radians = _wheelAutoSpinDirection * WheelAutoSpinRadiansPerSecond * 0.033f;
            _animationRuntimeController.RotateCar2Wheels(radians);
        });
    }

    private string MoveCar2(Vector3 delta)
    {
        return _animationRuntimeController.TrySetCar2RootPositionDelta(delta)
            ? $"Car2 animator: moved root by ({delta.X:0.00}, {delta.Y:0.00}, {delta.Z:0.00})."
            : "Car2 animator: root node was not found.";
    }

    private string TurnCar2(float radians)
    {
        return _animationRuntimeController.TrySetCar2RootYaw(radians)
            ? $"Car2 animator: yaw turn {radians:0.00} rad."
            : "Car2 animator: root node was not found.";
    }

    private string ResetCar2Pose()
    {
        var restored = _animationRuntimeController.ResetCar2Pose();
        return restored == 0
            ? "Car2 animator: snapshot is empty, nothing to reset."
            : $"Car2 animator: reset {restored} nodes.";
    }
}

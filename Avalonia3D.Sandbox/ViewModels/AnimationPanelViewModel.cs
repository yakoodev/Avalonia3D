using Avalonia.Threading;
using Avalonia3D.Animation;
using Avalonia3D.Sandbox.Services;
using Avalonia3D.Sandbox.Utilities;
using System.Collections.ObjectModel;
using System;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class AnimationPanelViewModel : BindableBase
{
    private readonly IAnimationRuntimeController _animationRuntimeController;
    private readonly IRenderThreadScheduler _renderThreadScheduler;
    private string? _selectedClipName;
    private bool _isLoopEnabled;
    private double _playbackSpeed = 1.0;
    private ClipPlaybackState _selectedClipState;

    public AnimationPanelViewModel(IAnimationRuntimeController animationRuntimeController, IRenderThreadScheduler renderThreadScheduler)
    {
        _animationRuntimeController = animationRuntimeController;
        _renderThreadScheduler = renderThreadScheduler;

        AvailableClips = new ObservableCollection<string>();
        PlayClipCommand = new RelayCommand(_ => Play());
        PauseClipCommand = new RelayCommand(_ => Pause());
        StopClipCommand = new RelayCommand(_ => Stop());
        TogglePlayPauseCommand = new RelayCommand(_ => TogglePlayPause());
    }

    public ObservableCollection<string> AvailableClips { get; }
    public RelayCommand PlayClipCommand { get; }
    public RelayCommand PauseClipCommand { get; }
    public RelayCommand StopClipCommand { get; }
    public RelayCommand TogglePlayPauseCommand { get; }

    public string? SelectedClipName
    {
        get => _selectedClipName;
        set
        {
            if (SetProperty(ref _selectedClipName, value))
            {
                UpdateSelectedClipState();
            }
        }
    }

    public bool IsLoopEnabled
    {
        get => _isLoopEnabled;
        set => SetProperty(ref _isLoopEnabled, value);
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetProperty(ref _playbackSpeed, value);
    }

    public string ClipStateText => SelectedClipState.IsRegistered
        ? $"{SelectedClipState.ClipName}: {(SelectedClipState.IsPlaying ? (SelectedClipState.IsPaused ? "Paused" : "Playing") : "Stopped")}, t={SelectedClipState.Time:0.00}/{SelectedClipState.Duration:0.00}, speed={SelectedClipState.Speed:0.00}, loop={SelectedClipState.Loop}"
        : "Клип не выбран";

    public void RefreshClips()
    {
        var clips = _animationRuntimeController.GetAvailableClips();
        AvailableClips.Clear();
        foreach (var clip in clips)
        {
            AvailableClips.Add(clip);
        }

        if (AvailableClips.Count == 0)
        {
            SelectedClipName = null;
            SelectedClipState = default;
            return;
        }

        if (!AvailableClips.Contains(SelectedClipName))
        {
            SelectedClipName = AvailableClips[0];
        }
    }

    private ClipPlaybackState SelectedClipState
    {
        get => _selectedClipState;
        set
        {
            _selectedClipState = value;
            RaisePropertyChanged(nameof(ClipStateText));
        }
    }

    private void UpdateSelectedClipState()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            SelectedClipState = default;
            return;
        }

        _renderThreadScheduler.Enqueue(() =>
        {
            var state = _animationRuntimeController.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
    }

    private void Play()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        _renderThreadScheduler.Enqueue(() =>
        {
            _animationRuntimeController.PlayClip(SelectedClipName, IsLoopEnabled, (float)PlaybackSpeed);
            var state = _animationRuntimeController.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
    }

    private void Pause()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        _renderThreadScheduler.Enqueue(() =>
        {
            _animationRuntimeController.PauseClip(SelectedClipName);
            var state = _animationRuntimeController.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
    }

    private void Stop()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        _renderThreadScheduler.Enqueue(() =>
        {
            _animationRuntimeController.StopClip(SelectedClipName);
            var state = _animationRuntimeController.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = state);
        });
    }

    private void TogglePlayPause()
    {
        if (string.IsNullOrWhiteSpace(SelectedClipName))
        {
            return;
        }

        _renderThreadScheduler.Enqueue(() =>
        {
            var state = _animationRuntimeController.GetClipState(SelectedClipName);
            if (state.IsPlaying && !state.IsPaused)
            {
                _animationRuntimeController.PauseClip(SelectedClipName);
            }
            else
            {
                _animationRuntimeController.PlayClip(SelectedClipName, IsLoopEnabled, (float)PlaybackSpeed);
            }

            var updatedState = _animationRuntimeController.GetClipState(SelectedClipName);
            Dispatcher.UIThread.Post(() => SelectedClipState = updatedState);
        });
    }
}

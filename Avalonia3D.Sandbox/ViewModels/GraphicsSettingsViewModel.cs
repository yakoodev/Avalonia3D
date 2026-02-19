using Avalonia.Media;
using Avalonia3D.Rendering;
using Avalonia3D.Sandbox.Utilities;
using System.Collections.ObjectModel;
using System;

namespace Avalonia3D.Sandbox.ViewModels;

public sealed class GraphicsSettingsViewModel : BindableBase
{
    private readonly Action<GraphicsProfile> _applyGraphicsProfile;
    private GraphicsProfile _graphicsProfile = GraphicsProfile.Medium;
    private RenderQualityPreset _selectedQualityPreset = RenderQualityPreset.Medium;
    private bool _isSyncingBackgroundChannels;
    private double _backgroundRed = 15;
    private double _backgroundGreen = 15;
    private double _backgroundBlue = 20;
    private string _environmentMapPathEditor = string.Empty;
    private string _profileJsonEditor = string.Empty;
    private string _profileStatusMessage = string.Empty;

    public GraphicsSettingsViewModel(Action<GraphicsProfile> applyGraphicsProfile)
    {
        _applyGraphicsProfile = applyGraphicsProfile;
        QualityPresets = new ObservableCollection<RenderQualityPreset>(new[]
        {
            RenderQualityPreset.Low,
            RenderQualityPreset.Medium,
            RenderQualityPreset.High,
            RenderQualityPreset.Custom
        });

        ApplyProfileJsonCommand = new RelayCommand(_ => ApplyProfileJson());
        ResetProfileJsonCommand = new RelayCommand(_ => ResetProfileJson());
        ApplyEnvironmentMapPathCommand = new RelayCommand(_ => ApplyEnvironmentMapPath());

        ApplyRenderQualityPreset(RenderQualityPreset.Medium);
    }

    public ObservableCollection<RenderQualityPreset> QualityPresets { get; }
    public RelayCommand ApplyProfileJsonCommand { get; }
    public RelayCommand ResetProfileJsonCommand { get; }
    public RelayCommand ApplyEnvironmentMapPathCommand { get; }

    public RenderQualityPreset SelectedQualityPreset
    {
        get => _selectedQualityPreset;
        set
        {
            if (SetProperty(ref _selectedQualityPreset, value))
            {
                ApplyRenderQualityPreset(value);
            }
        }
    }

    public bool ReflectionsEnabled
    {
        get => _graphicsProfile.Reflections.Enabled;
        set => ApplyGraphicsProfileOverride(p => p with { Reflections = p.Reflections with { Enabled = value } }, value ? "Отражения включены." : "Отражения отключены.");
    }

    public double ReflectionIntensity
    {
        get => _graphicsProfile.Reflections.Intensity;
        set => ApplyGraphicsProfileOverride(p => p with { Reflections = p.Reflections with { Intensity = (float)value } }, $"Reflection intensity: {value:0.00}");
    }

    public double IblSpecularIntensity
    {
        get => _graphicsProfile.PbrTuning.IblSpecularIntensity;
        set => ApplyGraphicsProfileOverride(p => p with { PbrTuning = p.PbrTuning with { IblSpecularIntensity = (float)value } }, $"Specular IBL: {value:0.00}");
    }

    public double IblDiffuseIntensity
    {
        get => _graphicsProfile.PbrTuning.IblDiffuseIntensity;
        set => ApplyGraphicsProfileOverride(p => p with { PbrTuning = p.PbrTuning with { IblDiffuseIntensity = (float)value } }, $"Diffuse IBL: {value:0.00}");
    }

    public double ReflectionClamp
    {
        get => _graphicsProfile.PbrTuning.ReflectionContributionClamp;
        set => ApplyGraphicsProfileOverride(p => p with { PbrTuning = p.PbrTuning with { ReflectionContributionClamp = (float)value } }, $"Reflection clamp: {value:0.00}");
    }

    public double Exposure
    {
        get => _graphicsProfile.PbrTuning.Exposure;
        set => ApplyGraphicsProfileOverride(p => p with { PbrTuning = p.PbrTuning with { Exposure = (float)value } }, $"Exposure: {value:0.00}");
    }

    public double WhitePoint
    {
        get => _graphicsProfile.PbrTuning.PbrWhitePoint;
        set => ApplyGraphicsProfileOverride(p => p with { PbrTuning = p.PbrTuning with { PbrWhitePoint = (float)value } }, $"White point: {value:0.00}");
    }

    public string EnvironmentMapPathEditor
    {
        get => _environmentMapPathEditor;
        set => SetProperty(ref _environmentMapPathEditor, value);
    }

    public string ProfileJsonEditor
    {
        get => _profileJsonEditor;
        set => SetProperty(ref _profileJsonEditor, value);
    }

    public string ProfileStatusMessage
    {
        get => _profileStatusMessage;
        set => SetProperty(ref _profileStatusMessage, value);
    }

    public string ActiveQualitySummary => _graphicsProfile.ToSummary();
    public string ActiveProfileJson => _graphicsProfile.ToJson();
    public SolidColorBrush BackgroundPreviewBrush => new(Color.FromRgb((byte)_backgroundRed, (byte)_backgroundGreen, (byte)_backgroundBlue));
    public string BackgroundHex => $"#{(byte)_backgroundRed:X2}{(byte)_backgroundGreen:X2}{(byte)_backgroundBlue:X2}";
    public string GraphicsTuningHint => "Подсказка: отражения сильнее всего зависят от зеркального IBL, клампа отражений и экспозиции.";

    private void ApplyRenderQualityPreset(RenderQualityPreset preset)
    {
        _graphicsProfile = GraphicsProfile.FromPreset(preset, _graphicsProfile) with
        {
            Name = preset == RenderQualityPreset.Custom ? _graphicsProfile.Name : preset.ToString()
        };

        ApplyProfile(_graphicsProfile, $"Применен профиль: {_graphicsProfile.Name}");
        RaisePropertyChanged(nameof(SelectedQualityPreset));
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
            var profile = GraphicsProfile.FromJson(ProfileJsonEditor);
            _selectedQualityPreset = profile.QualityPreset;
            ApplyProfile(profile, $"JSON профиль загружен: {profile.Name}");
            RaisePropertyChanged(nameof(SelectedQualityPreset));
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

        _selectedQualityPreset = RenderQualityPreset.Custom;
        ApplyProfile(candidate, statusMessage);
        RaisePropertyChanged(nameof(SelectedQualityPreset));
    }

    private void ApplyProfile(GraphicsProfile profile, string statusMessage)
    {
        _graphicsProfile = profile.Validate();
        _applyGraphicsProfile(_graphicsProfile);
        SyncBackgroundChannelsFromProfile(_graphicsProfile.Background);
        _environmentMapPathEditor = _graphicsProfile.Reflections.EnvironmentMapPath ?? string.Empty;

        ProfileJsonEditor = ActiveProfileJson;
        ProfileStatusMessage = statusMessage;
        RaisePropertyChanged(nameof(ActiveQualitySummary));
        RaisePropertyChanged(nameof(ActiveProfileJson));
        RaiseControlsChanged();
    }

    private void SyncBackgroundChannelsFromProfile(BackgroundProfile background)
    {
        _isSyncingBackgroundChannels = true;
        _backgroundRed = Math.Clamp(Math.Round(background.Red * 255f), 0d, 255d);
        _backgroundGreen = Math.Clamp(Math.Round(background.Green * 255f), 0d, 255d);
        _backgroundBlue = Math.Clamp(Math.Round(background.Blue * 255f), 0d, 255d);
        _isSyncingBackgroundChannels = false;
    }

    private void RaiseControlsChanged()
    {
        RaisePropertyChanged(nameof(ReflectionsEnabled));
        RaisePropertyChanged(nameof(ReflectionIntensity));
        RaisePropertyChanged(nameof(IblSpecularIntensity));
        RaisePropertyChanged(nameof(IblDiffuseIntensity));
        RaisePropertyChanged(nameof(ReflectionClamp));
        RaisePropertyChanged(nameof(Exposure));
        RaisePropertyChanged(nameof(WhitePoint));
        RaisePropertyChanged(nameof(EnvironmentMapPathEditor));
        RaisePropertyChanged(nameof(BackgroundPreviewBrush));
        RaisePropertyChanged(nameof(BackgroundHex));
    }
}

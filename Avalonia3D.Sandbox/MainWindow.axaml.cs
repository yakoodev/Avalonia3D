using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia3D.Sandbox.ViewModels;
using System;
using System.IO;

namespace Avalonia3D.Sandbox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BootstrapViewModel();
    }

    private void BootstrapViewModel()
    {
        var viewport = this.FindControl<SandboxModel3DControl>("Viewport");
        if (viewport == null)
        {
            return;
        }

        var assetsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "TestScenes");
        DataContext = new MainWindowViewModel(
            viewport.Scene,
            viewport.CameraController,
            assetsRoot,
            viewport.RenderThreadScheduler,
            viewport.ApplyGraphicsProfile,
            viewport)
        {
            OrbitSensitivity = viewport.RotationSensitivity,
            PanSensitivity = viewport.PanSensitivity,
            DollySensitivity = viewport.ZoomSensitivity
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

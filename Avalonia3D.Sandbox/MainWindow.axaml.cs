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
        var viewport = this.FindControl<SandboxModel3DControl>("Viewport");
        if (viewport != null)
        {
            var assetsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "TestScenes");
            var viewModel = new MainWindowViewModel(viewport.Scene, viewport.CameraController, assetsRoot, viewport.RenderThreadScheduler)
            {
                OrbitSensitivity = viewport.RotationSensitivity,
                PanSensitivity = viewport.PanSensitivity,
                DollySensitivity = viewport.ZoomSensitivity
            };
            DataContext = viewModel;
            viewport.RendererInitialized += (_, _) => viewModel.MarkRendererReady();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

using Avalonia.Controls;
using Avalonia.Input;
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
        var inputOverlay = this.FindControl<Border>("InputOverlay");
        if (viewport != null)
        {
            if (inputOverlay != null)
            {
                AttachPointerForwarding(inputOverlay, viewport);
            }
            var assetsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "TestScenes");
            var viewModel = new MainWindowViewModel(viewport.Scene, viewport.CameraController, assetsRoot, viewport.RenderThreadScheduler, viewport.ApplyRenderQuality)
            {
                OrbitSensitivity = viewport.RotationSensitivity,
                PanSensitivity = viewport.PanSensitivity,
                DollySensitivity = viewport.ZoomSensitivity
            };
            DataContext = viewModel;
            viewport.RendererInitialized += (_, _) => viewModel.MarkRendererReady();

            if (viewport.IsRendererInitialized)
            {
                viewModel.MarkRendererReady();
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }


    private static void AttachPointerForwarding(Border inputOverlay, SandboxModel3DControl viewport)
    {
        inputOverlay.PointerPressed += (_, e) => viewport.HandlePointerPressed(e);
        inputOverlay.PointerReleased += (_, e) => viewport.HandlePointerReleased(e);
        inputOverlay.PointerMoved += (_, e) => viewport.HandlePointerMoved(e);
        inputOverlay.PointerWheelChanged += (_, e) => viewport.HandlePointerWheelChanged(e);
    }

}

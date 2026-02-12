using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia3D.Sandbox.Controls;

public partial class ModelViewportPanel : UserControl
{
    public ModelViewportPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

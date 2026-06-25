using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigHWCANView : UserControl {
  public ConfigHWCANView() {
    InitializeComponent();
  }

  private void InitializeComponent() {
    AvaloniaXamlLoader.Load(this);
  }
}

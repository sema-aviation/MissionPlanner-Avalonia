using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigPlannerAdvView : UserControl {
  public ConfigPlannerAdvView() {
    InitializeComponent();
  }

  private void InitializeComponent() {
    AvaloniaXamlLoader.Load(this);
  }
}

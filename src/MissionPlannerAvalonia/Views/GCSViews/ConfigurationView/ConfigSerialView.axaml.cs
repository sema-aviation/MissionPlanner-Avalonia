using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigSerialView : UserControl {
  public ConfigSerialView() {
    InitializeComponent();
  }

  private void InitializeComponent() {
    AvaloniaXamlLoader.Load(this);
  }
}

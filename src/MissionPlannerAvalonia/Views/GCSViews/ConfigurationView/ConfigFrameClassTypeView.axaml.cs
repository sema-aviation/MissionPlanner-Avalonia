using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigFrameClassTypeView : UserControl {
  public ConfigFrameClassTypeView() {
    InitializeComponent();
  }

  private void InitializeComponent() {
    AvaloniaXamlLoader.Load(this);
  }
}

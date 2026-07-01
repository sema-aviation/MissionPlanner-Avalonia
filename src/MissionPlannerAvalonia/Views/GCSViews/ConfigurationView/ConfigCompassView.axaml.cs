using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigCompassView : UserControl {
  public ConfigCompassView() {
    AvaloniaXamlLoader.Load(this);

    DetachedFromVisualTree += (_, _) => {
      if (DataContext is ConfigCompassViewModel vm) {
        vm.Deactivate();
      }
    };
  }
}

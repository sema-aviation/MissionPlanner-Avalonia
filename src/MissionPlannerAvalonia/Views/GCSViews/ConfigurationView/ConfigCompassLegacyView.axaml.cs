using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigCompassLegacyView : UserControl {
  public ConfigCompassLegacyView() {
    AvaloniaXamlLoader.Load(this);

    DetachedFromVisualTree += (_, _) => {
      if (DataContext is ConfigCompassLegacyViewModel vm) {
        vm.Deactivate();
      }
    };
  }
}

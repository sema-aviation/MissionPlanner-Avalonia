using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigAccelCalibrationView : UserControl {
  public ConfigAccelCalibrationView() {
    AvaloniaXamlLoader.Load(this);
    DetachedFromVisualTree += (_, _) => {
      if (DataContext is ConfigAccelCalibrationViewModel vm) {
        vm.Deactivate();
      }
    };
  }
}

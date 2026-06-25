using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.Controls;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigCompassMotView : UserControl {
  public ConfigCompassMotView() {
    AvaloniaXamlLoader.Load(this);
    DataContextChanged += (_, _) => Attach();
  }

  private void Attach() {
    if (DataContext is ConfigCompassMotViewModel vm
        && this.FindControl<LivePlot>("Plot") is { } plot) {
      vm.AttachPlot(plot);
    }
  }
}

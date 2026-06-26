using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.Controls;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigCompassView : UserControl {
  private MagCalSphere? _sphere;
  private ConfigCompassViewModel? _vm;

  public ConfigCompassView() {
    AvaloniaXamlLoader.Load(this);
    _sphere = this.FindControl<MagCalSphere>("MagSphere");

    DataContextChanged += (_, _) => Hook(DataContext as ConfigCompassViewModel);

    DetachedFromVisualTree += (_, _) => {
      if (DataContext is ConfigCompassViewModel vm) {
        vm.Deactivate();
      }

      Unhook();
    };
  }

  private void Hook(ConfigCompassViewModel? vm) {
    if (ReferenceEquals(vm, _vm)) {
      return;
    }

    Unhook();
    _vm = vm;
    if (_vm == null) {
      return;
    }

    _vm.OnMagSample += AddSample;
    _vm.OnMagSphereClear += ClearSphere;
  }

  private void Unhook() {
    if (_vm == null) {
      return;
    }

    _vm.OnMagSample -= AddSample;
    _vm.OnMagSphereClear -= ClearSphere;
    _vm = null;
  }

  private void AddSample(double x, double y, double z) => _sphere?.AddPoint(x, y, z);

  private void ClearSphere() => _sphere?.Clear();
}

using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.Controls;
using MissionPlannerAvalonia.ViewModels.Setup;

namespace MissionPlannerAvalonia.Views.Setup;

public partial class SikRadioView : UserControl {
  private SikRadioViewModel? _vm;
  private readonly LivePlot? _plot;

  public SikRadioView() {
    InitializeComponent();
    _plot = this.FindControl<LivePlot>("RssiPlot");
    DataContextChanged += OnDataContextChanged;
  }

  private void InitializeComponent() {
    AvaloniaXamlLoader.Load(this);
  }

  private void OnDataContextChanged(object? sender, EventArgs e) {
    if (_vm != null) {
      _vm.RssiSample -= OnRssiSample;
      _vm.RssiReset -= OnRssiReset;
    }

    _vm = DataContext as SikRadioViewModel;

    if (_vm != null) {
      _vm.RssiSample += OnRssiSample;
      _vm.RssiReset += OnRssiReset;
      _plot?.SetAxisLabels("Time (s)", "RSSI / Noise", "Live RSSI");
    }
  }

  private void OnRssiSample(double t, double rssiL, double rssiR, double noiseL, double noiseR) {
    if (_plot == null) {
      return;
    }
    _plot.AppendPoint("RSSI Local", t, rssiL);
    _plot.AppendPoint("RSSI Remote", t, rssiR);
    _plot.AppendPoint("Noise Local", t, noiseL);
    _plot.AppendPoint("Noise Remote", t, noiseR);
  }

  private void OnRssiReset() {
    _plot?.ClearAll();
    _plot?.SetAxisLabels("Time (s)", "RSSI / Noise", "Live RSSI");
  }
}

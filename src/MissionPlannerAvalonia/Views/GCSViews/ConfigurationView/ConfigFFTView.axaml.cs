using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.Controls;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigFFTView : UserControl {
  public ConfigFFTView() {
    AvaloniaXamlLoader.Load(this);
    this.FindControl<Button>("FftBtn")!.Click += OnFft;
  }

  private async void OnFft(object? sender, RoutedEventArgs e) {
    var status = this.FindControl<TextBlock>("FftStatus")!;
    var plot = this.FindControl<LivePlot>("Plot")!;
    if (DataContext is not ConfigFFTViewModel vm) {
      return;
    }

    status.Text = "FFT runs on batch-logged IMU data. Pick a .bin to graph its spectrum.";

    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Open dataflash log",
      AllowMultiple = false,
      FileTypeFilter = new[] {
          new FilePickerFileType("Dataflash log") { Patterns = new[] { "*.bin", "*.BIN" } },
          new FilePickerFileType("All files") { Patterns = new[] { "*" } },
      },
    });
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path is null) {
      status.Text = "No log selected.";
      return;
    }

    status.Text = "Computing FFT…";
    var result = await Task.Run(() => vm.ComputeFft(path));
    if (result.Series.Count == 0) {
      status.Text = "No batch (ISBH/ISBD) or IMU data found in log.";
      return;
    }

    plot.ClearAll();
    plot.SetAxisLabels("Frequency (Hz)", vm.Magnitude ? "Magnitude" : "Amplitude (dB)",
        $"FFT: {result.Source} - {result.SampleRate}hz input");
    for (int i = 0; i < result.Series.Count; i++) {
      var s = result.Series[i];
      plot.SetSeries(s.Label, s.Freq, s.Mag, _palette[i % _palette.Length]);
    }

    var notch = result.SuggestedNotchHz > 0
        ? $" Suggested INS_HNTCH_FREQ ≈ {result.SuggestedNotchHz:0} Hz."
        : "";
    status.Text = $"Plotted {result.Series.Count} curves @ {result.SampleRate}hz.{notch}";
  }

  private static readonly ScottPlot.Color[] _palette = {
      ScottPlot.Colors.Red, ScottPlot.Colors.Green, ScottPlot.Colors.Blue,
      ScottPlot.Colors.Black, ScottPlot.Colors.Violet, ScottPlot.Colors.Orange,
  };
}

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
    var (freq, mag, label) = await Task.Run(() => vm.ComputeFft(path));
    if (freq.Length == 0) {
      status.Text = "No IMU data found in log.";
      return;
    }
    plot.ClearAll();
    plot.SetAxisLabels("Frequency (Hz)", "Magnitude", "FFT: " + label);
    plot.SetSeries(label, freq, mag);
    status.Text = $"Plotted {label} ({freq.Length} bins).";
  }
}

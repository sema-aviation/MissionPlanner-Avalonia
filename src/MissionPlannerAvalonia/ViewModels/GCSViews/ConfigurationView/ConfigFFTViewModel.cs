using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigFFTViewModel : ParamPageBase {
  [ObservableProperty]
  private string _info =
      "FFT runs on batch-logged IMU data. Set INS_LOG_BAT_MASK/CNT, enable the IMU batch " +
      "sampler in LOG_BITMASK, fly, then pick the resulting .bin to graph a frequency spectrum.";

  public ConfigFFTViewModel() {
    Title = "FFT Setup";
    Setup();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
  }

  private void Setup() {
    F("INS_LOG_BAT_CNT");
    F("INS_LOG_BAT_MASK");
    F("LOG_BITMASK");
  }

  // Divergence from upstream: Mission Planner opens its dedicated fftui plot window
  // (not ported). Here we read the configured IMU field from a picked .bin via
  // DataFlashLog.ReadField and graph a simple DFT magnitude on a LivePlot instead.
  public (double[] freq, double[] magnitude, string label) ComputeFft(string binPath) {
    foreach (var (msg, field) in new[] { ("IMU", "GyrX"), ("IMU", "AccX"), ("ISBD", "x") }) {
      var samples = DataFlashLog.ReadField(binPath, msg, field);
      if (samples.Count >= 8) {
        return Dft(samples, $"{msg}.{field}");
      }
    }
    return (Array.Empty<double>(), Array.Empty<double>(), "no IMU data");
  }

  private static (double[], double[], string) Dft(
      System.Collections.Generic.IReadOnlyList<(double time, double value)> samples, string label) {
    int n = Math.Min(samples.Count, 2048);
    var values = new double[n];
    double mean = 0;
    for (int i = 0; i < n; i++) {
      values[i] = samples[i].value;
      mean += values[i];
    }
    mean /= n;

    double span = samples[n - 1].time - samples[0].time;
    double fs = span > 0 ? (n - 1) / span : 1.0;

    int bins = n / 2;
    var freq = new double[bins];
    var mag = new double[bins];
    for (int k = 0; k < bins; k++) {
      double re = 0, im = 0;
      for (int t = 0; t < n; t++) {
        double angle = -2.0 * Math.PI * k * t / n;
        double v = values[t] - mean;
        re += v * Math.Cos(angle);
        im += v * Math.Sin(angle);
      }
      freq[k] = k * fs / n;
      mag[k] = Math.Sqrt(re * re + im * im) / n;
    }
    return (freq, mag, label);
  }
}

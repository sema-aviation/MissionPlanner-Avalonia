using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigFFTViewModel : ParamPageBase {
  [ObservableProperty]
  private string _info =
      "FFT runs on batch-logged IMU data. Set INS_LOG_BAT_MASK/CNT, enable the IMU batch " +
      "sampler in LOG_BITMASK, fly, then pick the resulting .bin to graph a frequency spectrum.";

  [ObservableProperty]
  private int _bins = 10;

  [ObservableProperty]
  private int _startFreq = 5;

  [ObservableProperty]
  private bool _magnitude;

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

  public sealed class FftSeries {
    public string Label { get; init; } = "";
    public double[] Freq { get; init; } = Array.Empty<double>();
    public double[] Mag { get; init; } = Array.Empty<double>();
  }

  public sealed class FftResult {
    public List<FftSeries> Series { get; } = new();
    public string Source { get; set; } = "";
    public double SampleRate { get; set; }
    public double SuggestedNotchHz { get; set; }
  }

  public FftResult ComputeFft(string binPath) {
    int bins = Math.Clamp(Bins, 4, 14);
    int n = 1 << bins;
    bool indB = !Magnitude;
    var fft = new FFT2();
    var result = new FftResult();

    using var file = new DFLogBuffer(binPath);

    if (!ComputeIsbh(file, fft, bins, n, indB, result)) {
      ComputeImu(file, fft, bins, n, indB, result);
    }

    ComputeSuggestedNotch(result);
    return result;
  }

  private bool ComputeIsbh(DFLogBuffer file, FFT2 fft, int bins, int n, bool indB, FftResult result) {
    var alldata = new FFT2.datastate[6 * 2];
    for (int a = 0; a < alldata.Length; a++) {
      alldata[a] = new FFT2.datastate();
    }

    int ns = 0, type = 0, instance = 0, sensorno = 0;
    double multiplier = -1;
    int offsetX = 0, offsetY = 0, offsetZ = 0, offsetTime = 0;

    foreach (var item in file.GetEnumeratorType(new[] { "ISBH", "ISBD" })) {
      if (item.msgtype == null) {
        continue;
      }

      if (item.msgtype.StartsWith("ISBH", StringComparison.Ordinal)) {
        ns = int.Parse(item.items[file.dflog.FindMessageOffset(item.msgtype, "N")], CultureInfo.InvariantCulture);
        type = int.Parse(item.items[file.dflog.FindMessageOffset(item.msgtype, "type")], CultureInfo.InvariantCulture);
        instance = int.Parse(item.items[file.dflog.FindMessageOffset(item.msgtype, "instance")], CultureInfo.InvariantCulture);
        sensorno = type * 6 + instance;
        if (sensorno >= alldata.Length) {
          continue;
        }
        alldata[sensorno].sample_rate = double.Parse(
            item.items[file.dflog.FindMessageOffset(item.msgtype, "smp_rate")], CultureInfo.InvariantCulture);
        multiplier = double.Parse(
            item.items[file.dflog.FindMessageOffset(item.msgtype, "mul")], CultureInfo.InvariantCulture);
        alldata[sensorno].type = (type == 0 ? "ACC" : "GYR") + instance.ToString(CultureInfo.InvariantCulture);
      } else if (item.msgtype.StartsWith("ISBD", StringComparison.Ordinal)) {
        if (sensorno >= alldata.Length) {
          continue;
        }
        if (Convert.ToInt32(item.GetRaw("N"), CultureInfo.InvariantCulture) != ns) {
          continue;
        }
        if (offsetX == 0) {
          offsetX = file.dflog.FindMessageOffset(item.msgtype, "x");
        }
        if (offsetY == 0) {
          offsetY = file.dflog.FindMessageOffset(item.msgtype, "y");
        }
        if (offsetZ == 0) {
          offsetZ = file.dflog.FindMessageOffset(item.msgtype, "z");
        }
        if (offsetTime == 0) {
          offsetTime = file.dflog.FindMessageOffset(item.msgtype, "TimeUS");
        }

        double time = Convert.ToDouble(item.raw[offsetTime], CultureInfo.InvariantCulture) / 1000.0;
        if (time < alldata[sensorno].lasttime) {
          continue;
        }
        alldata[sensorno].lasttime = time;

        AddShorts(alldata[sensorno].datax, item.raw[offsetX], multiplier);
        AddShorts(alldata[sensorno].datay, item.raw[offsetY], multiplier);
        AddShorts(alldata[sensorno].dataz, item.raw[offsetZ], multiplier);
      }
    }

    bool any = false;
    foreach (var sensordata in alldata) {
      if (sensordata.datax.Count <= n) {
        continue;
      }
      BuildSensorSeries(fft, sensordata, sensordata.sample_rate, bins, n, indB, result);
      any = true;
    }
    return any;
  }

  private void ComputeImu(DFLogBuffer file, FFT2 fft, int bins, int n, bool indB, FftResult result) {
    var alldata = new FFT2.datastate[3 * 2];
    for (int a = 0; a < alldata.Length; a++) {
      alldata[a] = new FFT2.datastate();
    }

    foreach (var item in file.GetEnumeratorType(new[] { "IMU", "IMU2", "IMU3" })) {
      if (item.msgtype == null || !item.msgtype.StartsWith("IMU", StringComparison.Ordinal)) {
        continue;
      }

      int sensorno = item.msgtype == "IMU2" ? 1 : item.msgtype == "IMU3" ? 2 : 0;
      int offsetTime = file.dflog.FindMessageOffset(item.msgtype, "TimeUS");
      double time = double.Parse(item.items[offsetTime], CultureInfo.InvariantCulture) / 1000.0;

      AddImuSample(alldata[sensorno + 3], item, file, "AccX", "AccY", "AccZ", time, item.msgtype + " ACC");
      AddImuSample(alldata[sensorno], item, file, "GyrX", "GyrY", "GyrZ", time, item.msgtype + " GYR");
    }

    foreach (var sensordata in alldata) {
      if (sensordata.datax.Count <= n) {
        continue;
      }
      double samplerate = Math.Round(1000 / sensordata.timedelta, 1);
      BuildSensorSeries(fft, sensordata, samplerate, bins, n, indB, result);
    }
  }

  private static void AddImuSample(FFT2.datastate state, DFLog.DFItem item, DFLogBuffer file,
                                   string fx, string fy, string fz, double time, string type) {
    state.type = type;
    if (time != state.lasttime) {
      state.timedelta = state.timedelta * 0.99 + (time - state.lasttime) * 0.01;
    }
    state.lasttime = time;
    state.datax.Add(double.Parse(item.items[file.dflog.FindMessageOffset(item.msgtype, fx)], CultureInfo.InvariantCulture));
    state.datay.Add(double.Parse(item.items[file.dflog.FindMessageOffset(item.msgtype, fy)], CultureInfo.InvariantCulture));
    state.dataz.Add(double.Parse(item.items[file.dflog.FindMessageOffset(item.msgtype, fz)], CultureInfo.InvariantCulture));
  }

  private void BuildSensorSeries(FFT2 fft, FFT2.datastate sensordata, double samplerate,
                                 int bins, int n, bool indB, FftResult result) {
    if (samplerate <= 0) {
      return;
    }
    double[] freqt = fft.FreqTable(n, (int)samplerate);
    var avgx = new double[n / 2];
    var avgy = new double[n / 2];
    var avgz = new double[n / 2];

    int count = sensordata.datax.Count / n;
    int done = 0;
    var chunk = new double[n];
    while (count > 1) {
      Accumulate(fft, sensordata.datax, done, n, bins, indB, freqt, avgx, done + count, chunk);
      Accumulate(fft, sensordata.datay, done, n, bins, indB, freqt, avgy, done + count, chunk);
      Accumulate(fft, sensordata.dataz, done, n, bins, indB, freqt, avgz, done + count, chunk);
      count--;
      done++;
    }

    result.SampleRate = samplerate;
    result.Source = string.IsNullOrEmpty(result.Source) ? sensordata.type : result.Source;
    result.Series.Add(new FftSeries { Label = sensordata.type + " x", Freq = freqt, Mag = avgx });
    result.Series.Add(new FftSeries { Label = sensordata.type + " y", Freq = freqt, Mag = avgy });
    result.Series.Add(new FftSeries { Label = sensordata.type + " z", Freq = freqt, Mag = avgz });
  }

  private void Accumulate(FFT2 fft, List<double> data, int done, int n, int bins, bool indB,
                          double[] freqt, double[] avg, int divisor, double[] chunk) {
    data.CopyTo(n * done, chunk, 0, n);
    var answer = fft.rin(chunk, (uint)bins, indB);
    for (int b = 0; b < n / 2; b++) {
      if (freqt[b] < StartFreq) {
        continue;
      }
      avg[b] += answer[b] / divisor;
    }
  }

  private static void AddShorts(List<double> dest, object raw, double multiplier) {
    var ua = (BinaryLog.UnionArray)raw;
    var shorts = ua.Shorts;
    for (int i = 0; i < shorts.Length; i++) {
      dest.Add(shorts[i] / multiplier);
    }
  }

  private void ComputeSuggestedNotch(FftResult result) {
    double bestMag = double.NegativeInfinity;
    double bestFreq = 0;
    foreach (var series in result.Series) {
      if (series.Label.IndexOf("GYR", StringComparison.OrdinalIgnoreCase) < 0) {
        continue;
      }
      for (int b = 0; b < series.Mag.Length; b++) {
        if (series.Freq[b] < StartFreq) {
          continue;
        }
        if (series.Mag[b] > bestMag) {
          bestMag = series.Mag[b];
          bestFreq = series.Freq[b];
        }
      }
    }
    result.SuggestedNotchHz = bestFreq;
  }
}

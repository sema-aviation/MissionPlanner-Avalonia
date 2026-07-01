using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionPlannerAvalonia.Services;

public enum LogTestStatus { Good, Warn, Fail, NA }

public readonly record struct LogTestResult(string Name, LogTestStatus Status, string Message);

public static class LogAnalyzer {

  public static LogTestStatus Classify(double value, double warn, double fail, bool higherWorse) {
    if (double.IsNaN(value)) {
      return LogTestStatus.NA;
    }
    if (higherWorse) {
      return value >= fail ? LogTestStatus.Fail : value >= warn ? LogTestStatus.Warn : LogTestStatus.Good;
    }
    return value <= fail ? LogTestStatus.Fail : value <= warn ? LogTestStatus.Warn : LogTestStatus.Good;
  }

  public static List<LogTestResult> Analyze(string binPath) {
    var results = new List<LogTestResult> {
      TestVibration(binPath),
      TestGps(binPath),
      TestVcc(binPath),
      TestCompass(binPath),
      TestMotorBalance(binPath),
      TestNaN(binPath),
    };
    return results;
  }

  public static string Format(IEnumerable<LogTestResult> results) =>
      string.Join("\n", results.Select(r => $"[{r.Status.ToString().ToUpper()}] {r.Name}: {r.Message}"));

  private static IReadOnlyList<double> Vals(string bin, string type, string field) =>
      DataFlashLog.ReadField(bin, type, field).Select(v => v.value).ToList();

  private static LogTestResult TestVibration(string bin) {
    double max = new[] { "VibeX", "VibeY", "VibeZ" }
        .SelectMany(f => Vals(bin, "VIBE", f)).DefaultIfEmpty(double.NaN).Max();
    if (double.IsNaN(max)) {
      return new("Vibration", LogTestStatus.NA, "no VIBE data");
    }

    return new("Vibration", Classify(max, 30, 60, true), $"max {max:0.0} m/s/s");
  }

  private static LogTestResult TestGps(string bin) {
    var sats = Vals(bin, "GPS", "NSats");
    if (sats.Count == 0) {
      return new("GPS", LogTestStatus.NA, "no GPS data");
    }
    double minSats = sats.Min();
    return new("GPS", Classify(minSats, 7, 5, false), $"min {minSats:0} sats");
  }

  private static LogTestResult TestVcc(string bin) {
    var vcc = Vals(bin, "POWR", "Vcc");
    if (vcc.Count == 0) {
      return new("VCC", LogTestStatus.NA, "no POWR data");
    }
    double min = vcc.Min();
    double max = vcc.Max();

    var status = min < 4.3 || max - min > 0.5 ? LogTestStatus.Fail
        : min < 4.6 || max - min > 0.3 ? LogTestStatus.Warn
        : LogTestStatus.Good;
    return new("VCC", status, $"min {min:0.00} V, spread {max - min:0.00} V");
  }

  private static LogTestResult TestCompass(string bin) {
    var x = Vals(bin, "MAG", "OfsX");
    var y = Vals(bin, "MAG", "OfsY");
    var z = Vals(bin, "MAG", "OfsZ");
    if (x.Count == 0) {
      return new("Compass", LogTestStatus.NA, "no MAG data");
    }
    double ofs = Math.Sqrt(Sq(x.Last()) + Sq(y.LastOrDefault()) + Sq(z.LastOrDefault()));

    return new("Compass", Classify(ofs, 350, 600, true), $"offset magnitude {ofs:0}");
  }

  private static LogTestResult TestMotorBalance(string bin) {
    var chans = new[] { "C1", "C2", "C3", "C4" }.Select(c => Vals(bin, "RCOU", c)).ToList();
    if (chans.Any(c => c.Count == 0)) {
      return new("Motor balance", LogTestStatus.NA, "no RCOU C1-C4 data");
    }
    var means = chans.Select(c => c.Average()).ToList();
    double spread = means.Max() - means.Min();

    return new("Motor balance", Classify(spread, 150, 300, true), $"output spread {spread:0} us");
  }

  private static LogTestResult TestNaN(string bin) {
    foreach (var (type, field) in new[] { ("ATT", "Roll"), ("ATT", "Pitch"), ("POS", "Alt") }) {
      if (Vals(bin, type, field).Any(double.IsNaN)) {
        return new("NaN", LogTestStatus.Fail, $"NaN found in {type}.{field}");
      }
    }
    return new("NaN", LogTestStatus.Good, "no NaNs in ATT/POS");
  }

  private static double Sq(double v) => v * v;
}

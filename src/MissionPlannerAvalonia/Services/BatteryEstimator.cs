using System;

namespace MissionPlannerAvalonia.Services;

public static class BatteryEstimator {

  private static readonly (double V, double Pct)[] _curve = {
    (4.20, 100), (4.15, 95), (4.11, 90), (4.08, 85), (4.02, 80), (3.98, 75), (3.95, 70),
    (3.91, 65), (3.87, 60), (3.85, 55), (3.84, 50), (3.82, 45), (3.80, 40), (3.79, 35),
    (3.77, 30), (3.75, 25), (3.73, 20), (3.71, 15), (3.69, 10), (3.61, 5), (3.27, 0),
  };

  public static int InferCells(double referenceVoltage) =>
      Math.Max(1, (int)Math.Ceiling(referenceVoltage / 4.2 - 0.05));

  public static double? FromCapacity(double usedMah, double capacityMah) {
    if (capacityMah <= 0 || usedMah < 0) {
      return null;
    }

    return Math.Clamp((capacityMah - usedMah) / capacityMah * 100.0, 0, 100);
  }

  public static double EstimatePercent(double packVoltage, double firmwarePercent) =>
      EstimatePercent(packVoltage, InferCells(packVoltage), firmwarePercent);

  public static double EstimatePercent(double packVoltage, int cells, double firmwarePercent) {
    if (packVoltage < 3.0) {
      return firmwarePercent;
    }

    double v = packVoltage / Math.Max(1, cells);
    if (v >= _curve[0].V) {
      return 100;
    }

    for (int i = 0; i < _curve.Length - 1; i++) {
      var hi = _curve[i];
      var lo = _curve[i + 1];
      if (v <= hi.V && v >= lo.V) {
        double t = (v - lo.V) / (hi.V - lo.V);
        return lo.Pct + t * (hi.Pct - lo.Pct);
      }
    }

    return 0;
  }
}

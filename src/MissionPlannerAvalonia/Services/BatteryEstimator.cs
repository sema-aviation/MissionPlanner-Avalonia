using System;

namespace MissionPlannerAvalonia.Services;

// Battery charge estimate. MP passes through the firmware's SYS_STATUS.battery_remaining; we
// diverge because a misconfigured monitor reports bogus % (e.g. 99% on a half-empty 6S).
// Most accurate -> least: coulomb counting (capacity - used mAh), then voltage curve with a
// cell count latched from the peak voltage seen this session.
public static class BatteryEstimator {
  // Resting voltage -> charge %, per cell, from a standard LiPo discharge curve.
  private static readonly (double V, double Pct)[] Curve = {
    (4.20, 100), (4.15, 95), (4.11, 90), (4.08, 85), (4.02, 80), (3.98, 75), (3.95, 70),
    (3.91, 65), (3.87, 60), (3.85, 55), (3.84, 50), (3.82, 45), (3.80, 40), (3.79, 35),
    (3.77, 30), (3.75, 25), (3.73, 20), (3.71, 15), (3.69, 10), (3.61, 5), (3.27, 0),
  };

  // Cell count from a reference voltage, assuming <=4.2 V/cell (ceil, epsilon dodges the
  // exact-multiple float trap). Feed it the PEAK voltage seen so a sagging pack can't shrink
  // the count mid-session — that's what makes the voltage estimate stable.
  public static int InferCells(double referenceVoltage) =>
      Math.Max(1, (int)Math.Ceiling(referenceVoltage / 4.2 - 0.05));

  // Coulomb counting — the accurate path when a current sensor + capacity are configured.
  // Returns null when unusable so the caller can fall back to voltage.
  public static double? FromCapacity(double usedMah, double capacityMah) {
    if (capacityMah <= 0 || usedMah < 0) {
      return null;
    }

    return Math.Clamp((capacityMah - usedMah) / capacityMah * 100.0, 0, 100);
  }

  // Voltage curve, inferring cell count from the pack voltage itself.
  public static double EstimatePercent(double packVoltage, double firmwarePercent) =>
      EstimatePercent(packVoltage, InferCells(packVoltage), firmwarePercent);

  // Voltage curve with an explicit (latched) cell count.
  public static double EstimatePercent(double packVoltage, int cells, double firmwarePercent) {
    if (packVoltage < 3.0) {
      return firmwarePercent; // no/disconnected sense -> don't fabricate a number
    }

    double v = packVoltage / Math.Max(1, cells);
    if (v >= Curve[0].V) {
      return 100;
    }

    for (int i = 0; i < Curve.Length - 1; i++) {
      var hi = Curve[i];
      var lo = Curve[i + 1];
      if (v <= hi.V && v >= lo.V) {
        double t = (v - lo.V) / (hi.V - lo.V);
        return lo.Pct + t * (hi.Pct - lo.Pct);
      }
    }

    return 0;
  }
}
